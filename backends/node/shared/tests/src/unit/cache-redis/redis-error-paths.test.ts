import { describe, it, expect } from "vitest";
import { AcquireLock, Get, Set, SetNx, Remove, Exists, GetTtl, Increment } from "@d2/cache-redis";
import { HandlerContext, type IHandlerContext, type IRequestContext } from "@d2/handler";
import { createLogger } from "@d2/logging";
import { HttpStatusCode, ErrorCodes } from "@d2/result";
import type Redis from "ioredis";

/**
 * Creates a mock ioredis client where every method throws a connection error.
 * This exercises the SERVICE_UNAVAILABLE catch blocks in all Redis handlers.
 */
function createBrokenRedis(): Redis {
  const error = new Error("Connection is closed");
  return {
    getBuffer: () => Promise.reject(error),
    set: () => Promise.reject(error),
    del: () => Promise.reject(error),
    exists: () => Promise.reject(error),
    pttl: () => Promise.reject(error),
    incrby: () => Promise.reject(error),
    pexpire: () => Promise.reject(error),
  } as unknown as Redis;
}

function createTestContext(traceId?: string): IHandlerContext {
  const request: IRequestContext = {
    traceId: traceId ?? "test-trace-id",
    isAuthenticated: false,
    isTrustedService: false,
    isOrgEmulating: false,
    isUserImpersonating: false,
    isAgentStaff: false,
    isAgentAdmin: false,
    isTargetingStaff: false,
    isTargetingAdmin: false,
  };
  return new HandlerContext(request, createLogger({ level: "silent" as never }));
}

describe("DistributedCache Redis error paths (SERVICE_UNAVAILABLE)", () => {
  const redis = createBrokenRedis();

  it("Get returns SERVICE_UNAVAILABLE when Redis is down", async () => {
    const handler = new Get<string>(redis, createTestContext());
    const result = await handler.handleAsync({ key: "k" });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(result.errorCode).toBe(ErrorCodes.SERVICE_UNAVAILABLE);
    expect(result.messages).toEqual(
      expect.arrayContaining([expect.stringContaining("Redis operation failed")]),
    );
  });

  it("Set returns SERVICE_UNAVAILABLE when Redis is down", async () => {
    const handler = new Set<string>(redis, createTestContext());
    const result = await handler.handleAsync({ key: "k", value: "v" });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(result.errorCode).toBe(ErrorCodes.SERVICE_UNAVAILABLE);
  });

  it("Remove returns SERVICE_UNAVAILABLE when Redis is down", async () => {
    const handler = new Remove(redis, createTestContext());
    const result = await handler.handleAsync({ key: "k" });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(result.errorCode).toBe(ErrorCodes.SERVICE_UNAVAILABLE);
  });

  it("Exists returns SERVICE_UNAVAILABLE when Redis is down", async () => {
    const handler = new Exists(redis, createTestContext());
    const result = await handler.handleAsync({ key: "k" });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(result.errorCode).toBe(ErrorCodes.SERVICE_UNAVAILABLE);
  });

  it("GetTtl returns SERVICE_UNAVAILABLE when Redis is down", async () => {
    const handler = new GetTtl(redis, createTestContext());
    const result = await handler.handleAsync({ key: "k" });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(result.errorCode).toBe(ErrorCodes.SERVICE_UNAVAILABLE);
  });

  it("SetNx returns SERVICE_UNAVAILABLE when Redis is down", async () => {
    const handler = new SetNx<string>(redis, createTestContext());
    const result = await handler.handleAsync({ key: "k", value: "v" });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(result.errorCode).toBe(ErrorCodes.SERVICE_UNAVAILABLE);
  });

  it("Increment returns SERVICE_UNAVAILABLE when Redis is down", async () => {
    const handler = new Increment(redis, createTestContext());
    const result = await handler.handleAsync({ key: "k" });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(HttpStatusCode.ServiceUnavailable);
    expect(result.errorCode).toBe(ErrorCodes.SERVICE_UNAVAILABLE);
  });

  it("preserves traceId on SERVICE_UNAVAILABLE", async () => {
    const handler = new Get<string>(redis, createTestContext("trace-123"));
    const result = await handler.handleAsync({ key: "k" });

    expect(result.traceId).toBe("trace-123");
  });
});

describe("AcquireLock input validation", () => {
  // Use a broken redis — validation should return before hitting Redis
  const redis = createBrokenRedis();

  it("returns VALIDATION_FAILED for zero expirationMs", async () => {
    const handler = new AcquireLock(redis, createTestContext());
    const result = await handler.handleAsync({ key: "k", lockId: "id", expirationMs: 0 });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
    expect(result.inputErrors).toEqual([["expirationMs", "Must be a finite positive number."]]);
  });

  it("returns VALIDATION_FAILED for negative expirationMs", async () => {
    const handler = new AcquireLock(redis, createTestContext());
    const result = await handler.handleAsync({ key: "k", lockId: "id", expirationMs: -100 });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  it("returns VALIDATION_FAILED for NaN expirationMs", async () => {
    const handler = new AcquireLock(redis, createTestContext());
    const result = await handler.handleAsync({ key: "k", lockId: "id", expirationMs: NaN });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });

  it("returns VALIDATION_FAILED for Infinity expirationMs", async () => {
    const handler = new AcquireLock(redis, createTestContext());
    const result = await handler.handleAsync({ key: "k", lockId: "id", expirationMs: Infinity });

    expect(result).toBeFailure();
    expect(result.statusCode).toBe(HttpStatusCode.BadRequest);
  });
});
