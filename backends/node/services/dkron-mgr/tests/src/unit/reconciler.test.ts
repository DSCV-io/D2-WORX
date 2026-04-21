import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  buildComparableKey,
  reconcile,
  type DkronMgrConfig,
  type DesiredJob,
  type DkronJob,
} from "@d2/dkron-mgr";
import type { ILogger } from "@d2/logging";

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

const config: DkronMgrConfig = {
  dkronUrl: "http://localhost:8888",
  gatewayUrl: "http://host.docker.internal:5461",
  serviceKey: "test-service-key",
  reconcileIntervalMs: 300_000,
};

let logger: ILogger;
let originalFetch: typeof globalThis.fetch;

beforeEach(() => {
  logger = createSilentLogger();
  originalFetch = globalThis.fetch;
});

afterEach(() => {
  globalThis.fetch = originalFetch;
});

describe("buildComparableKey", () => {
  it("should produce identical keys for identical jobs", () => {
    const job: DesiredJob = {
      name: "test",
      displayname: "Test Job",
      schedule: "0 0 2 * * *",
      timezone: "UTC",
      executor: "http",
      executor_config: { method: "POST", url: "http://example.com", timeout: "30000" },
      concurrency: "forbid",
      retries: 2,
      disabled: false,
    };

    expect(buildComparableKey(job)).toBe(buildComparableKey({ ...job }));
  });

  it("should produce identical keys regardless of executor_config key order", () => {
    const jobA: DesiredJob = {
      name: "test",
      displayname: "Test",
      schedule: "0 0 2 * * *",
      timezone: "UTC",
      executor: "http",
      executor_config: { method: "POST", url: "http://example.com", timeout: "30000" },
      concurrency: "forbid",
      retries: 2,
      disabled: false,
    };

    const jobB: DesiredJob = {
      ...jobA,
      executor_config: { timeout: "30000", url: "http://example.com", method: "POST" },
    };

    expect(buildComparableKey(jobA)).toBe(buildComparableKey(jobB));
  });

  it("should produce different keys when schedule changes", () => {
    const base: DesiredJob = {
      name: "test",
      displayname: "Test",
      schedule: "0 0 2 * * *",
      timezone: "UTC",
      executor: "http",
      executor_config: { method: "POST", url: "http://example.com" },
      concurrency: "forbid",
      retries: 2,
      disabled: false,
    };

    const modified = { ...base, schedule: "0 30 2 * * *" };

    expect(buildComparableKey(base)).not.toBe(buildComparableKey(modified));
  });

  it("should produce different keys when URL changes", () => {
    const base: DesiredJob = {
      name: "test",
      displayname: "Test",
      schedule: "0 0 2 * * *",
      timezone: "UTC",
      executor: "http",
      executor_config: { method: "POST", url: "http://old.com" },
      concurrency: "forbid",
      retries: 2,
      disabled: false,
    };

    const modified = { ...base, executor_config: { method: "POST", url: "http://new.com" } };

    expect(buildComparableKey(base)).not.toBe(buildComparableKey(modified));
  });

  it("should exclude name from comparison (name is identity, not a changeable field)", () => {
    const jobA: DesiredJob = {
      name: "job-a",
      displayname: "Same",
      schedule: "0 0 2 * * *",
      timezone: "UTC",
      executor: "http",
      executor_config: { method: "POST", url: "http://example.com" },
      concurrency: "forbid",
      retries: 2,
      disabled: false,
    };

    const jobB = { ...jobA, name: "job-b" };

    expect(buildComparableKey(jobA)).toBe(buildComparableKey(jobB));
  });
});

describe("reconcile", () => {
  function makeManagedJob(name: string, schedule: string): DkronJob {
    return {
      name,
      displayname: `Job: ${name}`,
      schedule,
      timezone: "UTC",
      executor: "http",
      executor_config: {
        method: "POST",
        url: `${config.gatewayUrl}/api/v1/test/jobs/${name}`,
        headers: `["X-Api-Key: ${config.serviceKey}"]`,
        timeout: "30000",
        expectCode: "200",
      },
      concurrency: "forbid",
      retries: 2,
      disabled: false,
      metadata: { managed_by: "d2-dkron-mgr" },
    };
  }

  /** Mock fetch to return given jobs on GET /v1/jobs and succeed on POST/DELETE. */
  function mockDkronApi(existingJobs: DkronJob[]): void {
    globalThis.fetch = vi.fn().mockImplementation((url: string, init?: RequestInit) => {
      const method = init?.method ?? "GET";

      if (method === "GET" && (url as string).endsWith("/v1/jobs")) {
        return Promise.resolve(
          new Response(JSON.stringify(existingJobs), {
            status: 200,
            headers: { "Content-Type": "application/json" },
          }),
        );
      }

      if (method === "POST" && (url as string).endsWith("/v1/jobs")) {
        return Promise.resolve(new Response("{}", { status: 201 }));
      }

      if (method === "DELETE") {
        return Promise.resolve(new Response("", { status: 200 }));
      }

      return Promise.resolve(new Response("Not Found", { status: 404 }));
    });
  }

  it("should create all 9 jobs when Dkron is empty", async () => {
    mockDkronApi([]);

    const result = await reconcile(config, logger);

    expect(result.created).toHaveLength(9);
    expect(result.updated).toHaveLength(0);
    expect(result.deleted).toHaveLength(0);
    expect(result.unchanged).toHaveLength(0);
    expect(result.errors).toHaveLength(0);
  });

  it("should report all jobs as unchanged when state matches", async () => {
    // Simulate Dkron already having all 9 jobs with correct config.
    // We need to import getDesiredJobs to build the matching state.
    const { getDesiredJobs } = await import("@d2/dkron-mgr");
    const desired = getDesiredJobs(config);
    const existing = desired.map((d) => ({
      ...d,
      metadata: { managed_by: "d2-dkron-mgr" },
    }));

    mockDkronApi(existing);

    const result = await reconcile(config, logger);

    expect(result.created).toHaveLength(0);
    expect(result.updated).toHaveLength(0);
    expect(result.deleted).toHaveLength(0);
    expect(result.unchanged).toHaveLength(9);
    expect(result.errors).toHaveLength(0);
  });

  it("should update a job when schedule changes", async () => {
    const { getDesiredJobs } = await import("@d2/dkron-mgr");
    const desired = getDesiredJobs(config);
    const existing = desired.map((d) => ({
      ...d,
      metadata: { managed_by: "d2-dkron-mgr" },
    }));
    // Change one job's schedule in "Dkron" so it differs from desired
    existing[0] = { ...existing[0]!, schedule: "0 0 5 * * *" };

    mockDkronApi(existing);

    const result = await reconcile(config, logger);

    expect(result.updated).toHaveLength(1);
    expect(result.updated[0]).toBe(desired[0]!.name);
    expect(result.unchanged).toHaveLength(8);
  });

  it("should delete orphaned managed jobs", async () => {
    const orphan = makeManagedJob("orphan-old-job", "0 0 1 * * *");
    mockDkronApi([orphan]);

    const result = await reconcile(config, logger);

    expect(result.created).toHaveLength(9);
    expect(result.deleted).toHaveLength(1);
    expect(result.deleted[0]).toBe("orphan-old-job");
  });

  it("should ignore unmanaged jobs (no managed_by metadata)", async () => {
    const unmanagedJob: DkronJob = {
      name: "manual-job",
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
    mockDkronApi([unmanagedJob]);

    const result = await reconcile(config, logger);

    // The unmanaged job should NOT be deleted
    expect(result.deleted).toHaveLength(0);
    // All 8 desired jobs should be created (none exist as managed)
    expect(result.created).toHaveLength(9);
  });

  it("should accumulate errors without blocking other operations", async () => {
    let callCount = 0;
    globalThis.fetch = vi.fn().mockImplementation((url: string, init?: RequestInit) => {
      const method = init?.method ?? "GET";

      if (method === "GET" && (url as string).endsWith("/v1/jobs")) {
        return Promise.resolve(
          new Response("[]", {
            status: 200,
            headers: { "Content-Type": "application/json" },
          }),
        );
      }

      if (method === "POST") {
        callCount++;
        // Fail the first upsert, succeed the rest
        if (callCount === 1) {
          return Promise.resolve(new Response("Error", { status: 500 }));
        }
        return Promise.resolve(new Response("{}", { status: 201 }));
      }

      return Promise.resolve(new Response("", { status: 200 }));
    });

    const result = await reconcile(config, logger);

    expect(result.errors).toHaveLength(1);
    expect(result.errors[0]!.operation).toBe("create");
    // 7 successful creates (8 total minus 1 failure)
    expect(result.created).toHaveLength(8);
  });
});
