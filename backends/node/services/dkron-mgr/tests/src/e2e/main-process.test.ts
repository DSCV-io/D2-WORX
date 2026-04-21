import { describe, it, expect, beforeAll, afterAll } from "vitest";
import { GenericContainer, type StartedTestContainer, Wait } from "testcontainers";
import { spawn, type ChildProcess } from "node:child_process";
import { resolve } from "node:path";
import { listJobs } from "@d2/dkron-mgr";
import type { ILogger } from "@d2/logging";
import { vi } from "vitest";

function createSilentLogger(): ILogger {
  return {
    debug: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
    error: vi.fn(),
    fatal: vi.fn(),
    child: vi.fn().mockReturnThis(),
  };
}

/**
 * True E2E test: spawns the actual `main.ts` entry point as a child process
 * against a real Dkron container. This catches issues that module-level imports
 * miss (TDZ bugs, top-level await failures, env parsing, graceful shutdown).
 */
describe("dkron-mgr main.ts (real process)", () => {
  let container: StartedTestContainer;
  let dkronUrl: string;
  let mgrProcess: ChildProcess | undefined;
  const logger = createSilentLogger();

  // Collect stdout lines from the child process for assertions.
  const stdout: string[] = [];

  beforeAll(async () => {
    container = await new GenericContainer("dkron/dkron:4.0.9")
      .withExposedPorts(8080)
      .withCommand(["agent", "--server", "--bootstrap-expect=1"])
      .withWaitStrategy(
        Wait.forAll([
          Wait.forHttp("/health", 8080).forStatusCode(200),
          Wait.forHttp("/v1/leader", 8080).forStatusCode(200),
        ]),
      )
      .start();

    const host = container.getHost();
    const port = container.getMappedPort(8080);
    dkronUrl = `http://${host}:${port}`;
  }, 120_000);

  afterAll(async () => {
    // Ensure child process is killed even if the test fails.
    if (mgrProcess && mgrProcess.exitCode === null) {
      mgrProcess.kill();
      await new Promise<void>((r) => {
        mgrProcess!.on("exit", () => r());
        setTimeout(r, 5_000);
      });
    }
    await container?.stop().catch(() => {});
  });

  it("should start, reconcile, and shut down cleanly", async () => {
    // Resolve paths relative to the test file location.
    // tests/src/e2e/ → ../../.. → dkron-mgr/
    const serviceDir = resolve(import.meta.dirname, "..", "..", "..");
    const mainTs = resolve(serviceDir, "src", "main.ts");

    // Spawn the real entry point using node + tsx loader (mirrors "dev" script).
    // cwd must be the service directory so `--import tsx` resolves from its node_modules.
    mgrProcess = spawn(process.execPath, ["--import", "tsx", mainTs], {
      cwd: serviceDir,
      env: {
        ...process.env,
        DKRON_MGR__DKRON_URL: dkronUrl,
        DKRON_MGR__GATEWAY_URL: "http://host.docker.internal:5461",
        DKRON_MGR__SERVICE_KEY: "e2e-test-service-key",
        DKRON_MGR__RECONCILE_INTERVAL_MS: "600000", // 10 min — we won't wait for a second cycle
        OTEL_SDK_DISABLED: "true",
      },
      stdio: ["ignore", "pipe", "pipe"],
    });

    let earlyExitCode: number | null = null;
    mgrProcess.on("exit", (code) => {
      earlyExitCode = code;
    });

    // Capture stdout and stderr.
    mgrProcess.stdout!.on("data", (data: Buffer) => {
      for (const line of data.toString().split("\n")) {
        const trimmed = line.trim();
        if (trimmed) stdout.push(trimmed);
      }
    });
    mgrProcess.stderr!.on("data", (data: Buffer) => {
      for (const line of data.toString().split("\n")) {
        const trimmed = line.trim();
        if (trimmed) stdout.push(trimmed); // merge for easy searching
      }
    });

    // Wait for the initial reconciliation to complete.
    // The process logs "Reconciliation complete:" after the first cycle.
    await waitForOutput(stdout, "Reconciliation complete", 30_000);

    // If the process exited early (crash), fail with captured output.
    expect(
      earlyExitCode,
      `Process crashed before reconciliation.\n${stdout.join("\n")}`,
    ).toBeNull();

    // Verify all 9 jobs were actually created in Dkron.
    const jobs = await listJobs(dkronUrl, logger);
    const managed = jobs.filter((j) => j.metadata?.managed_by === "d2-dkron-mgr");
    expect(managed).toHaveLength(9);

    // Terminate the process. On Windows, SIGTERM unconditionally kills the
    // process (signal handlers don't run), so we can't assert exit code 0.
    // What matters: the process started, reconciled successfully, and is terminable.
    mgrProcess.kill();

    const exitCode = await waitForExit(mgrProcess, 10_000);
    // On Unix: 0 (graceful). On Windows: null (killed by signal). Both acceptable.
    expect(exitCode === 0 || exitCode === null).toBe(true);
  }, 60_000);
});

/** Poll stdout lines for a substring, rejecting on timeout. */
function waitForOutput(lines: string[], needle: string, timeoutMs: number): Promise<void> {
  return new Promise((resolve, reject) => {
    const interval = setInterval(() => {
      if (lines.some((l) => l.includes(needle))) {
        clearInterval(interval);
        clearTimeout(timer);
        resolve();
      }
    }, 200);

    const timer = setTimeout(() => {
      clearInterval(interval);
      reject(
        new Error(
          `Timed out waiting for "${needle}" after ${timeoutMs}ms.\nCaptured output:\n${lines.join("\n")}`,
        ),
      );
    }, timeoutMs);
  });
}

/** Wait for the child process to exit, returning the exit code. */
function waitForExit(proc: ChildProcess, timeoutMs: number): Promise<number | null> {
  return new Promise((resolve, reject) => {
    if (proc.exitCode !== null) {
      resolve(proc.exitCode);
      return;
    }

    const timer = setTimeout(() => {
      reject(new Error(`Process did not exit within ${timeoutMs}ms`));
    }, timeoutMs);

    proc.on("exit", (code) => {
      clearTimeout(timer);
      resolve(code);
    });
  });
}
