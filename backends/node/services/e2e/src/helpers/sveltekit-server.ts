import { spawn, type ChildProcess } from "node:child_process";
import { resolve } from "node:path";
import net from "node:net";

let svelteKitProcess: ChildProcess | undefined;
let svelteKitPort: number | undefined;

/**
 * Finds a random available port by binding to port 0.
 * Exported so global-setup can pre-allocate the SvelteKit port before
 * starting the auth service (needed for CORS/trusted origins config).
 */
export async function getAvailablePort(): Promise<number> {
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
 * Polls the SvelteKit dev server until it responds.
 */
async function waitForSvelteKitReady(port: number, timeoutMs = 60_000): Promise<void> {
  const deadline = Date.now() + timeoutMs;
  const url = `http://localhost:${port}/`;

  while (Date.now() < deadline) {
    try {
      const res = await fetch(url, { signal: AbortSignal.timeout(2_000) });
      if (res.ok || res.status === 404) return; // Any response means server is up
    } catch {
      // Not ready yet
    }
    await new Promise((r) => setTimeout(r, 500));
  }

  throw new Error(`SvelteKit dev server failed to start within ${timeoutMs}ms on port ${port}`);
}

/**
 * Starts the SvelteKit dev server as a child process.
 *
 * Configures env vars to point at the running auth, geo, and redis services
 * so the SvelteKit server can resolve sessions, load geo data, etc.
 *
 * @returns The SvelteKit base URL (http://localhost:{port})
 */
export async function startSvelteKitServer(opts: {
  authUrl: string;
  /** S2S API key the SvelteKit auth proxy sends as `x-api-key` to the auth API. */
  authApiKey: string;
  redisUrl: string;
  geoAddress: string;
  geoApiKey: string;
  port?: number;
}): Promise<string> {
  const webDir = resolve(import.meta.dirname, "../../../../../../clients/web");

  const port = opts.port ?? (await getAvailablePort());
  svelteKitPort = port;

  const env: Record<string, string> = {
    ...process.env,
    // SvelteKit auth proxy config — apiKey is required since the auth API
    // service-key middleware is fail-closed (commit 95ca94c7).
    SVELTEKIT_AUTH__URL: opts.authUrl,
    SVELTEKIT_AUTH__API_KEY: opts.authApiKey,
    // Geo gRPC config
    GEO_GRPC_ADDRESS: opts.geoAddress,
    SVELTEKIT_GEO_CLIENT__APIKEY: opts.geoApiKey,
    // Redis for distributed cache
    REDIS_URL: opts.redisUrl,
    // Disable OTel in tests
    OTEL_SDK_DISABLED: "true",
    // Override CI/mock flags so SvelteKit uses real infrastructure.
    // GitHub Actions sets CI=true by default, which triggers mock mode
    // in auth.server.ts and middleware.server.ts — but browser E2E tests
    // have real infrastructure running via Testcontainers.
    CI: "",
    D2_MOCK_INFRA: "false",
  } as Record<string, string>;

  svelteKitProcess = spawn("pnpm", ["dev", "--port", String(port)], {
    cwd: webDir,
    env,
    stdio: ["ignore", "pipe", "pipe"],
    shell: true,
  });

  // Log stdout/stderr for debugging
  svelteKitProcess.stdout?.on("data", (data: Buffer) => {
    const msg = data.toString().trim();
    if (msg) console.log(`[SvelteKit] ${msg}`);
  });
  svelteKitProcess.stderr?.on("data", (data: Buffer) => {
    const msg = data.toString().trim();
    if (msg) console.error(`[SvelteKit] ${msg}`);
  });

  svelteKitProcess.on("exit", (code) => {
    if (code !== null && code !== 0) {
      console.error(`[SvelteKit] Process exited with code ${code}`);
    }
  });

  // Wait for the dev server to be ready
  await waitForSvelteKitReady(port);

  return `http://localhost:${port}`;
}

/**
 * Stops the SvelteKit dev server process.
 */
export async function stopSvelteKitServer(): Promise<void> {
  if (!svelteKitProcess) return;

  return new Promise<void>((resolve) => {
    const timeout = setTimeout(() => {
      svelteKitProcess?.kill("SIGKILL");
      resolve();
    }, 5_000);

    svelteKitProcess!.on("exit", () => {
      clearTimeout(timeout);
      resolve();
    });

    svelteKitProcess!.kill("SIGTERM");
  });
}

/**
 * Returns the URL of the running SvelteKit dev server.
 */
export function getSvelteKitUrl(): string {
  if (!svelteKitPort) throw new Error("SvelteKit server not started");
  return `http://localhost:${svelteKitPort}`;
}
