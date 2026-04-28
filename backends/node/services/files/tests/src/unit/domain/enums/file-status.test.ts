import { describe, it, expect } from "vitest";
import { FILE_STATUSES, FILE_STATUS_TRANSITIONS, isValidFileStatus } from "@d2/files-domain";

describe("FileStatus", () => {
  it("should have exactly 4 statuses", () => {
    expect(FILE_STATUSES).toHaveLength(4);
  });

  it("should contain all expected statuses", () => {
    expect(FILE_STATUSES).toContain("pending");
    expect(FILE_STATUSES).toContain("processing");
    expect(FILE_STATUSES).toContain("ready");
    expect(FILE_STATUSES).toContain("rejected");
  });

  describe("isValidFileStatus", () => {
    it.each(["pending", "processing", "ready", "rejected"])(
      "should return true for valid status '%s'",
      (status) => {
        expect(isValidFileStatus(status)).toBe(true);
      },
    );

    it.each(["Pending", "READY", "active", "completed", "", 42, null, undefined, true])(
      "should return false for invalid value '%s'",
      (value) => {
        expect(isValidFileStatus(value)).toBe(false);
      },
    );
  });

  describe("FILE_STATUS_TRANSITIONS", () => {
    it("should allow pending to transition to processing or rejected", () => {
      expect(FILE_STATUS_TRANSITIONS.pending).toContain("processing");
      expect(FILE_STATUS_TRANSITIONS.pending).toContain("rejected");
    });

    it("should allow processing to transition to ready or rejected", () => {
      expect(FILE_STATUS_TRANSITIONS.processing).toContain("ready");
      expect(FILE_STATUS_TRANSITIONS.processing).toContain("rejected");
    });

    it("should have ready as terminal (no transitions)", () => {
      expect(FILE_STATUS_TRANSITIONS.ready).toHaveLength(0);
    });

    it("should have rejected as terminal (no transitions)", () => {
      expect(FILE_STATUS_TRANSITIONS.rejected).toHaveLength(0);
    });

    it("should not allow pending to transition directly to ready", () => {
      expect(FILE_STATUS_TRANSITIONS.pending).not.toContain("ready");
    });

    it("should not allow processing to transition back to pending", () => {
      expect(FILE_STATUS_TRANSITIONS.processing).not.toContain("pending");
    });
  });
});
