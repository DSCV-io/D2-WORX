import { describe, it, expect } from "vitest";
import { generateIdempotencyKey } from "./idempotency";

describe("generateIdempotencyKey", () => {
  it("returns a valid UUID string", () => {
    const key = generateIdempotencyKey();
    // UUIDv4 format: 8-4-4-4-12 hex chars
    expect(key).toMatch(/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/);
  });

  it("generates unique keys on each call", () => {
    const keys = new Set(Array.from({ length: 100 }, () => generateIdempotencyKey()));
    expect(keys.size).toBe(100);
  });
});
