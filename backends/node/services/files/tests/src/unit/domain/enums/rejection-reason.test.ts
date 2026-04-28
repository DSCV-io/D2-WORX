import { describe, it, expect } from "vitest";
import { REJECTION_REASONS, isValidRejectionReason } from "@d2/files-domain";

describe("RejectionReason", () => {
  it("should have exactly 8 reasons", () => {
    expect(REJECTION_REASONS).toHaveLength(8);
  });

  it("should contain all expected reasons", () => {
    expect(REJECTION_REASONS).toContain("size_exceeded");
    expect(REJECTION_REASONS).toContain("size_mismatch");
    expect(REJECTION_REASONS).toContain("invalid_content_type");
    expect(REJECTION_REASONS).toContain("magic_bytes_mismatch");
    expect(REJECTION_REASONS).toContain("content_moderation_failed");
    expect(REJECTION_REASONS).toContain("processing_timeout");
    expect(REJECTION_REASONS).toContain("corrupt_file");
    expect(REJECTION_REASONS).toContain("virus_detected");
  });

  describe("isValidRejectionReason", () => {
    it.each([
      "size_exceeded",
      "size_mismatch",
      "invalid_content_type",
      "magic_bytes_mismatch",
      "content_moderation_failed",
      "processing_timeout",
      "corrupt_file",
      "virus_detected",
    ])("should return true for valid reason '%s'", (reason) => {
      expect(isValidRejectionReason(reason)).toBe(true);
    });

    it.each(["Size_Exceeded", "CORRUPT_FILE", "too_big", "virus", "", 42, null, undefined])(
      "should return false for invalid value '%s'",
      (value) => {
        expect(isValidRejectionReason(value)).toBe(false);
      },
    );
  });
});
