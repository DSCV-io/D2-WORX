import { describe, it, expect } from "vitest";
import { getDesiredJobs, type DkronMgrConfig } from "@d2/dkron-mgr";

const config: DkronMgrConfig = {
  dkronUrl: "http://localhost:8888",
  gatewayUrl: "http://host.docker.internal:5461",
  serviceKey: "test-key-12345",
  reconcileIntervalMs: 300_000,
};

describe("getDesiredJobs", () => {
  it("should return exactly 9 jobs", () => {
    const jobs = getDesiredJobs(config);
    expect(jobs).toHaveLength(9);
  });

  it("should include all expected job names", () => {
    const jobs = getDesiredJobs(config);
    const names = jobs.map((j) => j.name);

    expect(names).toContain("geo-purge-stale-whois");
    expect(names).toContain("geo-cleanup-orphaned-locations");
    expect(names).toContain("auth-purge-sessions");
    expect(names).toContain("auth-purge-sign-in-events");
    expect(names).toContain("auth-cleanup-invitations");
    expect(names).toContain("auth-cleanup-emulation-consents");
    expect(names).toContain("auth-cleanup-deleted-users");
    expect(names).toContain("comms-purge-deleted-messages");
    expect(names).toContain("comms-purge-delivery-history");
  });

  it("should have unique job names", () => {
    const jobs = getDesiredJobs(config);
    const names = jobs.map((j) => j.name);
    expect(new Set(names).size).toBe(names.length);
  });

  it("should resolve gateway URL in executor_config", () => {
    const jobs = getDesiredJobs(config);
    for (const job of jobs) {
      expect(job.executor_config.url!.startsWith(config.gatewayUrl)).toBe(true);
    }
  });

  it("should inject service key into headers", () => {
    const jobs = getDesiredJobs(config);
    for (const job of jobs) {
      expect(job.executor_config.headers).toContain(config.serviceKey);
      expect(job.executor_config.headers).toContain("X-Api-Key");
    }
  });

  it("should use UTC timezone for all jobs", () => {
    const jobs = getDesiredJobs(config);
    for (const job of jobs) {
      expect(job.timezone).toBe("UTC");
    }
  });

  it("should use http executor for all jobs", () => {
    const jobs = getDesiredJobs(config);
    for (const job of jobs) {
      expect(job.executor).toBe("http");
    }
  });

  it("should forbid concurrent execution for all jobs", () => {
    const jobs = getDesiredJobs(config);
    for (const job of jobs) {
      expect(job.concurrency).toBe("forbid");
    }
  });

  it("should have 2 retries for all jobs", () => {
    const jobs = getDesiredJobs(config);
    for (const job of jobs) {
      expect(job.retries).toBe(2);
    }
  });

  it("should have all jobs enabled", () => {
    const jobs = getDesiredJobs(config);
    for (const job of jobs) {
      expect(job.disabled).toBe(false);
    }
  });

  it("should use POST method for all jobs", () => {
    const jobs = getDesiredJobs(config);
    for (const job of jobs) {
      expect(job.executor_config.method).toBe("POST");
    }
  });

  it("should have 30s timeout for all jobs", () => {
    const jobs = getDesiredJobs(config);
    for (const job of jobs) {
      expect(job.executor_config.timeout).toBe("30000");
    }
  });

  it("should stagger schedules (no two jobs at the same time)", () => {
    const jobs = getDesiredJobs(config);
    const schedules = jobs.map((j) => j.schedule);
    expect(new Set(schedules).size).toBe(schedules.length);
  });

  it("should run geo-purge-stale-whois before geo-cleanup-orphaned-locations", () => {
    const jobs = getDesiredJobs(config);
    const whoisIdx = jobs.findIndex((j) => j.name === "geo-purge-stale-whois");
    const locationsIdx = jobs.findIndex((j) => j.name === "geo-cleanup-orphaned-locations");

    // WhoIs purge at 2:00, locations cleanup at 2:15
    expect(jobs[whoisIdx]!.schedule).toBe("0 0 2 * * *");
    expect(jobs[locationsIdx]!.schedule).toBe("0 15 2 * * *");
  });
});
