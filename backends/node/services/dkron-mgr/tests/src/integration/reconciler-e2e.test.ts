import { describe, it, expect, beforeAll, afterAll } from "vitest";
import { GenericContainer, type StartedTestContainer, Wait } from "testcontainers";
import {
  checkHealth,
  listJobs,
  upsertJob,
  deleteJob,
  reconcile,
  getDesiredJobs,
  type DkronMgrConfig,
  type DkronJob,
} from "@d2/dkron-mgr";
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

describe("Dkron Integration (real container)", () => {
  let container: StartedTestContainer;
  let dkronUrl: string;
  let config: DkronMgrConfig;
  let logger: ILogger;

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

    config = {
      dkronUrl,
      gatewayUrl: "http://host.docker.internal:5461",
      serviceKey: "integration-test-key",
      reconcileIntervalMs: 300_000,
    };

    logger = createSilentLogger();
  }, 120_000);

  afterAll(async () => {
    await container?.stop().catch(() => {});
  });

  // ── Dkron Client ─────────────────────────────────────────────────

  it("should report healthy", async () => {
    const healthy = await checkHealth(dkronUrl, logger);
    expect(healthy).toBe(true);
  });

  it("should return empty job list on fresh container", async () => {
    const jobs = await listJobs(dkronUrl, logger);
    expect(jobs).toEqual([]);
  });

  it("should create a job via upsert", async () => {
    const job: DkronJob = {
      name: "test-create-job",
      displayname: "Integration: Create",
      schedule: "0 0 12 * * *",
      timezone: "UTC",
      executor: "http",
      executor_config: {
        method: "POST",
        url: "http://localhost:9999/noop",
        timeout: "5000",
      },
      concurrency: "forbid",
      retries: 1,
      disabled: true,
      metadata: { managed_by: "d2-dkron-mgr" },
    };

    await upsertJob(dkronUrl, job, logger);

    const jobs = await listJobs(dkronUrl, logger);
    const found = jobs.find((j) => j.name === "test-create-job");
    expect(found).toBeDefined();
    expect(found!.schedule).toBe("0 0 12 * * *");
    expect(found!.disabled).toBe(true);
  });

  it("should update a job via upsert", async () => {
    const updated: DkronJob = {
      name: "test-create-job",
      displayname: "Integration: Updated",
      schedule: "0 30 12 * * *",
      timezone: "UTC",
      executor: "http",
      executor_config: {
        method: "POST",
        url: "http://localhost:9999/noop",
        timeout: "5000",
      },
      concurrency: "forbid",
      retries: 2,
      disabled: true,
      metadata: { managed_by: "d2-dkron-mgr" },
    };

    await upsertJob(dkronUrl, updated, logger);

    const jobs = await listJobs(dkronUrl, logger);
    const found = jobs.find((j) => j.name === "test-create-job");
    expect(found).toBeDefined();
    expect(found!.schedule).toBe("0 30 12 * * *");
    expect(found!.retries).toBe(2);
    expect(found!.displayname).toBe("Integration: Updated");
  });

  it("should delete a job", async () => {
    await deleteJob(dkronUrl, "test-create-job", logger);

    const jobs = await listJobs(dkronUrl, logger);
    const found = jobs.find((j) => j.name === "test-create-job");
    expect(found).toBeUndefined();
  });

  it("should handle delete of non-existent job gracefully", async () => {
    // Should not throw — 404 is treated as success
    await expect(deleteJob(dkronUrl, "does-not-exist", logger)).resolves.toBeUndefined();
  });

  // ── Full Reconciliation Cycle ────────────────────────────────────

  it("should create all 9 jobs on first reconciliation", async () => {
    const result = await reconcile(config, logger);

    expect(result.errors).toHaveLength(0);
    expect(result.created).toHaveLength(9);
    expect(result.updated).toHaveLength(0);
    expect(result.deleted).toHaveLength(0);
    expect(result.unchanged).toHaveLength(0);

    // Verify jobs exist in Dkron
    const jobs = await listJobs(dkronUrl, logger);
    const managed = jobs.filter((j) => j.metadata?.managed_by === "d2-dkron-mgr");
    expect(managed).toHaveLength(9);
  });

  it("should report all unchanged on second reconciliation", async () => {
    const result = await reconcile(config, logger);

    expect(result.errors).toHaveLength(0);
    expect(result.created).toHaveLength(0);
    expect(result.updated).toHaveLength(0);
    expect(result.deleted).toHaveLength(0);
    expect(result.unchanged).toHaveLength(9);
  });

  it("should detect and fix a schedule drift", async () => {
    // Tamper with one job's schedule directly in Dkron
    const desired = getDesiredJobs(config);
    const firstJob = desired[0]!;
    const tampered: DkronJob = {
      ...firstJob,
      schedule: "0 0 23 * * *", // changed from original
      metadata: { managed_by: "d2-dkron-mgr" },
    };
    await upsertJob(dkronUrl, tampered, logger);

    // Reconcile should detect the drift and update it
    const result = await reconcile(config, logger);

    expect(result.errors).toHaveLength(0);
    expect(result.updated).toHaveLength(1);
    expect(result.updated[0]).toBe(firstJob.name);
    expect(result.unchanged).toHaveLength(8);
    expect(result.created).toHaveLength(0);
    expect(result.deleted).toHaveLength(0);

    // Verify the schedule was corrected
    const jobs = await listJobs(dkronUrl, logger);
    const fixed = jobs.find((j) => j.name === firstJob.name);
    expect(fixed!.schedule).toBe(firstJob.schedule);
  });

  it("should delete orphaned managed jobs", async () => {
    // Create an extra managed job that's NOT in our desired set
    const orphan: DkronJob = {
      name: "orphan-legacy-job",
      displayname: "Should Be Deleted",
      schedule: "0 0 1 * * *",
      timezone: "UTC",
      executor: "http",
      executor_config: { method: "POST", url: "http://localhost:9999/orphan" },
      concurrency: "forbid",
      retries: 0,
      disabled: false,
      metadata: { managed_by: "d2-dkron-mgr" },
    };
    await upsertJob(dkronUrl, orphan, logger);

    const result = await reconcile(config, logger);

    expect(result.errors).toHaveLength(0);
    expect(result.deleted).toHaveLength(1);
    expect(result.deleted[0]).toBe("orphan-legacy-job");
    expect(result.unchanged).toHaveLength(9);

    // Verify it's actually gone
    const jobs = await listJobs(dkronUrl, logger);
    expect(jobs.find((j) => j.name === "orphan-legacy-job")).toBeUndefined();
  });

  it("should leave unmanaged jobs untouched", async () => {
    // Create a job without managed_by tag (simulating manual Dkron job)
    const manual: DkronJob = {
      name: "manual-user-job",
      displayname: "Manually Created",
      schedule: "0 0 * * * *",
      timezone: "UTC",
      executor: "shell",
      executor_config: { command: "echo hello" },
      concurrency: "allow",
      retries: 0,
      disabled: false,
      metadata: null,
    };
    await upsertJob(dkronUrl, manual, logger);

    const result = await reconcile(config, logger);

    // Should NOT delete the unmanaged job
    expect(result.deleted).toHaveLength(0);
    expect(result.unchanged).toHaveLength(9);

    // Verify the manual job still exists
    const jobs = await listJobs(dkronUrl, logger);
    expect(jobs.find((j) => j.name === "manual-user-job")).toBeDefined();

    // Cleanup
    await deleteJob(dkronUrl, "manual-user-job", logger);
  });
});
