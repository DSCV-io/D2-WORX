import { describe, it, expect } from "vitest";
import { CONTENT_CATEGORIES, isValidContentCategory } from "@d2/files-domain";

describe("ContentCategory", () => {
  it("should have exactly 4 categories", () => {
    expect(CONTENT_CATEGORIES).toHaveLength(4);
  });

  it("should contain all expected categories", () => {
    expect(CONTENT_CATEGORIES).toContain("image");
    expect(CONTENT_CATEGORIES).toContain("document");
    expect(CONTENT_CATEGORIES).toContain("video");
    expect(CONTENT_CATEGORIES).toContain("audio");
  });

  describe("isValidContentCategory", () => {
    it.each(["image", "document", "video", "audio"])(
      "should return true for valid category '%s'",
      (category) => {
        expect(isValidContentCategory(category)).toBe(true);
      },
    );

    it.each(["Image", "DOCUMENT", "file", "binary", "", 42, null, undefined, true])(
      "should return false for invalid value '%s'",
      (value) => {
        expect(isValidContentCategory(value)).toBe(false);
      },
    );
  });
});
