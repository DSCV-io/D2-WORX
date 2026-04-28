import { describe, it, expect } from "vitest";
import { FILES_ERROR_CODES } from "@d2/files-domain";

describe("FILES_ERROR_CODES", () => {
  it("all values are prefixed with FILES_", () => {
    for (const [, value] of Object.entries(FILES_ERROR_CODES)) {
      expect(value).toMatch(/^FILES_/);
    }
  });

  it("all values are unique", () => {
    const values = Object.values(FILES_ERROR_CODES);
    expect(new Set(values).size).toBe(values.length);
  });

  it("should have exactly 9 error codes", () => {
    expect(Object.keys(FILES_ERROR_CODES)).toHaveLength(9);
  });
});
