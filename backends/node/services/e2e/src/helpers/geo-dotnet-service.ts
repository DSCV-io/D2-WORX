import { spawn, type ChildProcess } from "node:child_process";
import { resolve } from "node:path";
import net from "node:net";
import pg from "pg";

let geoProcess: ChildProcess | undefined;
let geoPort: number | undefined;

// No URI→.NET format conversion helpers needed — ConnectionStringHelper.cs
// on the .NET side handles parsing standard URIs to StackExchange/ADO.NET format.

/**
 * Finds a random available port by binding to port 0.
 */
async function getAvailablePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = net.createServer();
    server.listen(0, () => {
      const addr = server.address();
      if (!addr || typeof addr === "string") {
        server.close();
        reject(new Error("Failed to get port"));
        return;
      }
      const port = addr.port;
      server.close(() => resolve(port));
    });
    server.on("error", reject);
  });
}

/**
 * Polls the Geo service health endpoint until it responds.
 */
async function waitForGeoReady(port: number, timeoutMs = 60_000): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  const url = `http://localhost:${port}/health`;

  while (Date.now() < deadline) {
    try {
      const res = await fetch(url, { signal: AbortSignal.timeout(2_000) });
      if (res.ok) return;
    } catch {
      // Not ready yet
    }
    await new Promise((r) => setTimeout(r, 500));
  }

  throw new Error(`Geo .NET service failed to start within ${timeoutMs}ms on port ${port}`);
}

/**
 * Starts the .NET Geo.API as a child process.
 *
 * @returns The gRPC address (host:port) for the Geo service
 */
export async function startGeoService(opts: {
  pgUrl: string;
  redisUrl: string;
  rabbitUrl: string;
  apiKey: string;
}): Promise<string> {
  // Find the Geo.API project relative to the repo root
  const projectDir = resolve(
    import.meta.dirname,
    "../../../../../../backends/dotnet/services/Geo/Geo.API",
  );

  const httpPort = await getAvailablePort();
  const grpcPort = await getAvailablePort();

  geoPort = grpcPort;

  const env: Record<string, string> = {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: "Development",
    // Kestrel: separate HTTP/1.1 (health) and HTTP/2 (gRPC) endpoints
    Kestrel__Endpoints__Http1__Url: `http://+:${httpPort}`,
    Kestrel__Endpoints__Http1__Protocols: "Http1",
    Kestrel__Endpoints__Grpc__Url: `http://+:${grpcPort}`,
    Kestrel__Endpoints__Grpc__Protocols: "Http2",
    // Infrastructure URLs — ConnectionStringHelper.cs parses URIs to .NET formats.
    GEO_DATABASE_URL: opts.pgUrl,
    REDIS_URL: opts.redisUrl,
    RABBITMQ_URL: opts.rabbitUrl,
    // API key mapping: allow auth context keys for contacts.
    // Config section is "GEO_APP" (see Geo.App/Extensions.cs).
    "GEO_APP__ApiKeyMappings__e2e-test-key__0": "auth_user",
    "GEO_APP__ApiKeyMappings__e2e-test-key__1": "auth_org_contact",
    "GEO_APP__ApiKeyMappings__e2e-test-key__2": "auth_org_invitation",
    // Run EF Core migrations on startup (creates tables in test container)
    AUTO_MIGRATE: "true",
    // Disable OTel in tests
    OTEL_SDK_DISABLED: "true",
  } as Record<string, string>;

  geoProcess = spawn(
    "dotnet",
    ["run", "--project", projectDir, "--no-build", "--no-launch-profile"],
    {
      env,
      stdio: ["ignore", "pipe", "pipe"],
    },
  );

  // Log stdout/stderr for debugging
  geoProcess.stdout?.on("data", (data: Buffer) => {
    const msg = data.toString().trim();
    if (msg) console.log(`[Geo.API] ${msg}`);
  });
  geoProcess.stderr?.on("data", (data: Buffer) => {
    const msg = data.toString().trim();
    if (msg) console.error(`[Geo.API] ${msg}`);
  });

  geoProcess.on("exit", (code) => {
    if (code !== null && code !== 0) {
      console.error(`[Geo.API] Process exited with code ${code}`);
    }
  });

  // Wait for the HTTP health endpoint to be ready
  await waitForGeoReady(httpPort);

  // Defensive: verify the timezones table is populated and seed any rows the
  // contact FK relies on. EF Core migrations DO include InsertData for the full
  // IANA list (incl. "America/New_York"), and the migration history shows
  // `20260326190046_AddTimezones` applied — but in CI the seed rows aren't
  // observable at contact-insert time, causing every browser-flow sign-up to
  // fail with `FK_contacts_timezones_iana_identifier`. Root cause is being
  // investigated separately; this seeds the minimum set of timezones that
  // production code defaults to so tests can proceed. ON CONFLICT DO NOTHING
  // keeps it idempotent if the migration's seed eventually shows up.
  await ensureMinimumTimezoneSeed(opts.pgUrl);

  return `localhost:${grpcPort}`;
}

/**
 * Inserts the small set of timezones that auth/contact creation paths
 * default to, so foreign-key checks pass even if the EF migration seed
 * isn't observable in the test environment.
 */
async function ensureMinimumTimezoneSeed(pgUrl: string): Promise<void> {
  const client = new pg.Client({ connectionString: pgUrl });
  try {
    await client.connect();
    // Match the column shape from migration `20260326190046_AddTimezones`.
    // Only "America/New_York" is strictly required — it's the EF column
    // default + the auth-side fallback. Seeding a US-only set so we don't
    // need to know about other country FK rows. ON CONFLICT keeps it
    // idempotent if the migration's seed eventually does show up.
    await client.query(`
      INSERT INTO timezones (
        iana_identifier, abbreviation_dst, abbreviation_std,
        country_iso_3166_1_alpha_2_code, display_name, utc_offset_dst, utc_offset_std
      ) VALUES
        ('America/New_York',    'EDT', 'EST', 'US', 'America / New York',    '-04:00', '-05:00'),
        ('America/Los_Angeles', 'PDT', 'PST', 'US', 'America / Los Angeles', '-07:00', '-08:00'),
        ('America/Chicago',     'CDT', 'CST', 'US', 'America / Chicago',     '-05:00', '-06:00')
      ON CONFLICT (iana_identifier) DO NOTHING;
    `);
  } finally {
    await client.end();
  }
}

/**
 * Stops the .NET Geo.API process.
 */
export async function stopGeoService(): Promise<void> {
  if (!geoProcess) return;

  return new Promise<void>((resolve) => {
    const timeout = setTimeout(() => {
      geoProcess?.kill("SIGKILL");
      resolve();
    }, 5_000);

    geoProcess!.on("exit", () => {
      clearTimeout(timeout);
      resolve();
    });

    geoProcess!.kill("SIGTERM");
  });
}

/**
 * Returns the gRPC address of the running Geo service.
 */
export function getGeoAddress(): string {
  if (!geoPort) throw new Error("Geo service not started");
  return `localhost:${geoPort}`;
}
