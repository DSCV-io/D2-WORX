import { GenericContainer, type StartedTestContainer, Wait } from "testcontainers";
import { Socket } from "node:net";

let container: StartedTestContainer;

/**
 * Checks ClamAV readiness by sending a TCP PING command.
 * ClamAV responds with "PONG\0" (null-terminated) when the daemon is ready.
 */
function checkClamdReady(host: string, port: number): Promise<boolean> {
  return new Promise<boolean>((resolve) => {
    const socket = new Socket();
    socket.setTimeout(5_000);

    socket.connect(port, host, () => {
      // ClamAV nINSTREAM/zINSTREAM protocol: null-terminated commands
      socket.write("PING\n");
    });

    socket.on("data", (data) => {
      const response = data.toString().replace(/\0/g, "").trim();
      socket.destroy();
      resolve(response === "PONG");
    });

    socket.on("error", () => {
      socket.destroy();
      resolve(false);
    });

    socket.on("timeout", () => {
      socket.destroy();
      resolve(false);
    });
  });
}

/**
 * Starts a ClamAV container.
 *
 * ClamAV takes ~30-90 seconds to load virus definitions on first start.
 * We wait until the daemon responds to PING with PONG.
 */
export async function startClamAV(): Promise<void> {
  container = await new GenericContainer("clamav/clamav:latest")
    .withExposedPorts(3310)
    .withEnvironment({
      // Skip freshclam virus DB update — base image includes EICAR signatures.
      // This cuts startup from ~90-120s to ~15-30s.
      CLAMAV_NO_FRESHCLAMD: "true",
    })
    .withStartupTimeout(120_000)
    .withWaitStrategy(Wait.forLogMessage(/socket found, clamd started/i))
    .start();

  // Additional readiness check — daemon may log "started" before accepting connections.
  // ClamAV loads virus definitions on first start which can take 60-120+ seconds.
  const host = container.getHost();
  const port = container.getMappedPort(3310);
  const deadline = Date.now() + 150_000;

  while (Date.now() < deadline) {
    if (await checkClamdReady(host, port)) return;
    await new Promise((r) => setTimeout(r, 2_000));
  }

  throw new Error("ClamAV failed to become ready within 150 seconds");
}

/** Race a promise against a timeout (resolves even if inner hangs). */
function withTimeout(promise: Promise<unknown>, ms: number, label: string): Promise<void> {
  return Promise.race([
    promise.then(() => {}),
    new Promise<void>((resolve) =>
      setTimeout(() => {
        console.warn(`[E2E] ${label} timed out after ${ms}ms — forcing continue`);
        resolve();
      }, ms),
    ),
  ]);
}

/**
 * Stops the ClamAV container.
 */
export async function stopClamAV(): Promise<void> {
  await withTimeout(container?.stop() ?? Promise.resolve(), 10_000, "clamavContainer.stop");
}

/** ClamAV daemon host. */
export function getClamdHost(): string {
  return container.getHost();
}

/** ClamAV daemon mapped port. */
export function getClamdPort(): number {
  return container.getMappedPort(3310);
}
