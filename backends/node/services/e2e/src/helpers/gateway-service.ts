import { spawn, type ChildProcess } from "node:child_process";
import { resolve } from "node:path";
import net from "node:net";

let gatewayProcess: ChildProcess | undefined;
let gatewayPort: number | undefined;

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
 * Polls the Gateway health endpoint until it responds.
 *
 * 180s default — the .NET gateway compiles + binds slowly under Docker
 * resource pressure when several other Testcontainer-backed services boot
 * in the same suite. 60s was tight enough to flake; 120s also flaked
 * intermittently on cold CI runners; 180s gives headroom for first-restore
 * NuGet downloads + JIT warmup without bloating green-path runtime
 * (the wait exits as soon as `/health` 200s).
 */
async function waitForGatewayReady(port: number, timeoutMs = 180_000): Promise<void> {
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

  throw new Error(`Gateway failed to start within ${timeoutMs}ms on port ${port}`);
}

/**
 * Starts the .NET REST Gateway as a child process.
 *
 * @returns The Gateway HTTP URL (http://localhost:{port})
 */
export async function startGateway(opts: {
  redisUrl: string;
  geoGrpcAddress: string;
  serviceKey: string;
  geoApiKey: string;
}): Promise<string> {
  const projectDir = resolve(
    import.meta.dirname,
    "../../../../../../backends/dotnet/gateways/REST",
  );

  const httpPort = await getAvailablePort();
  gatewayPort = httpPort;

  const env: Record<string, string> = {
    ...process.env,
    ASPNETCORE_ENVIRONMENT: "Development",
    // Single HTTP endpoint (Gateway doesn't need separate gRPC port)
    ASPNETCORE_URLS: `http://+:${httpPort}`,
    // Infrastructure URL — ConnectionStringHelper.cs parses URI to StackExchange format.
    REDIS_URL: opts.redisUrl,
    // Geo gRPC address (the only service we actually call)
    GEO_GRPC_ADDRESS: opts.geoGrpcAddress,
    // Auth + Comms + Files + SignalR gRPC addresses (required by config
    // validation, but not called in Geo-only tests). SignalR was added to
    // the fail-closed startup check in `SignalREndpoints.cs:37` and the
    // sentinel here keeps the gateway from crashing during e2e setup.
    AUTH_GRPC_ADDRESS: "localhost:1",
    COMMS_GRPC_ADDRESS: "localhost:1",
    FILES_GRPC_ADDRESS: "localhost:1",
    SIGNALR_GRPC_ADDRESS: "localhost:1",
    // Service key config: allow Dkron's X-Api-Key to pass
    GATEWAY_SERVICEKEY__ValidKeys__0: opts.serviceKey,
    // gRPC API keys (sent as call credentials to downstream services)
    GATEWAY_GEO_GRPC_API_KEY: opts.geoApiKey,
    // Auth + Comms + Files keys required by startup validation but not called in Geo-only tests
    GATEWAY_AUTH_GRPC_API_KEY: "e2e-dummy-auth-key",
    GATEWAY_COMMS_GRPC_API_KEY: "e2e-dummy-comms-key",
    GATEWAY_FILES_GRPC_API_KEY: "e2e-dummy-files-key",
    // JWT config (required by service registration, but service-key endpoints skip JWT)
    GATEWAY_AUTH__AuthServiceBaseUrl: "http://localhost:1",
    GATEWAY_AUTH__Issuer: "e2e-test",
    GATEWAY_AUTH__Audience: "e2e-test",
    // Disable OTel in tests
    OTEL_SDK_DISABLED: "true",
  } as Record<string, string>;

  gatewayProcess = spawn(
    "dotnet",
    ["run", "--project", projectDir, "--no-build", "--no-launch-profile"],
    {
      env,
      stdio: ["ignore", "pipe", "pipe"],
    },
  );

  // Log stdout/stderr for debugging
  gatewayProcess.stdout?.on("data", (data: Buffer) => {
    const msg = data.toString().trim();
    if (msg) console.log(`[Gateway] ${msg}`);
  });
  gatewayProcess.stderr?.on("data", (data: Buffer) => {
    const msg = data.toString().trim();
    if (msg) console.error(`[Gateway] ${msg}`);
  });

  gatewayProcess.on("exit", (code) => {
    if (code !== null && code !== 0) {
      console.error(`[Gateway] Process exited with code ${code}`);
    }
  });

  // Wait for the health endpoint
  await waitForGatewayReady(httpPort);

  return `http://localhost:${httpPort}`;
}

/**
 * Stops the .NET Gateway process.
 */
export async function stopGateway(): Promise<void> {
  if (!gatewayProcess) return;

  return new Promise<void>((resolve) => {
    const timeout = setTimeout(() => {
      gatewayProcess?.kill("SIGKILL");
      resolve();
    }, 5_000);

    gatewayProcess!.on("exit", () => {
      clearTimeout(timeout);
      resolve();
    });

    gatewayProcess!.kill("SIGTERM");
  });
}

/**
 * Returns the HTTP URL of the running Gateway.
 */
export function getGatewayUrl(): string {
  if (!gatewayPort) throw new Error("Gateway not started");
  return `http://localhost:${gatewayPort}`;
}
