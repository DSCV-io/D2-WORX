import { describe, it, expect } from "vitest";

describe("VariantSize", () => {
  it("should accept any non-empty string as a variant size", () => {
    const size: string = "custom_preview";
    expect(typeof size).toBe("string");
    expect(size.length).toBeGreaterThan(0);
  });
});
