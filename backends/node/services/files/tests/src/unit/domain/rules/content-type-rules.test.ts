import { describe, it, expect } from "vitest";
import {
  resolveContentCategory,
  isContentTypeAllowed,
  getAllowedContentTypes,
} from "@d2/files-domain";

describe("Content Type Rules", () => {
  describe("resolveContentCategory", () => {
    it.each([
      ["image/jpeg", "image"],
      ["image/png", "image"],
      ["image/gif", "image"],
      ["image/webp", "image"],
      ["image/avif", "image"],
      ["image/heic", "image"],
      ["image/heif", "image"],
    ] as const)("should resolve '%s' to '%s'", (contentType, expected) => {
      expect(resolveContentCategory(contentType)).toBe(expected);
    });

    it.each([
      ["application/pdf", "document"],
      ["text/plain", "document"],
      ["text/csv", "document"],
    ] as const)("should resolve '%s' to '%s'", (contentType, expected) => {
      expect(resolveContentCategory(contentType)).toBe(expected);
    });

    it.each([
      ["video/mp4", "video"],
      ["video/webm", "video"],
      ["video/quicktime", "video"],
      ["video/3gpp", "video"],
    ] as const)("should resolve '%s' to '%s'", (contentType, expected) => {
      expect(resolveContentCategory(contentType)).toBe(expected);
    });

    it.each([
      ["audio/mpeg", "audio"],
      ["audio/ogg", "audio"],
      ["audio/wav", "audio"],
      ["audio/webm", "audio"],
      ["audio/aac", "audio"],
      ["audio/mp4", "audio"],
    ] as const)("should resolve '%s' to '%s'", (contentType, expected) => {
      expect(resolveContentCategory(contentType)).toBe(expected);
    });

    it("should return undefined for unknown content type", () => {
      expect(resolveContentCategory("application/octet-stream")).toBeUndefined();
      expect(resolveContentCategory("text/html")).toBeUndefined();
      expect(resolveContentCategory("application/json")).toBeUndefined();
    });

    it("should NOT classify image/svg+xml as image (B1: SVG dropped due to XSS via presigned GET)", () => {
      // Regression: SVG was previously in the image category but enables script
      // execution in the storage origin via presigned GET URLs. Stays out of
      // every category so UploadFile rejects it as a disallowed content type.
      expect(resolveContentCategory("image/svg+xml")).toBeUndefined();
    });
  });

  describe("isContentTypeAllowed", () => {
    it("should return true when content type belongs to an allowed category", () => {
      expect(isContentTypeAllowed("image/png", ["image"])).toBe(true);
      expect(isContentTypeAllowed("image/png", ["image", "document"])).toBe(true);
    });

    it("should return false when content type does not belong to allowed categories", () => {
      expect(isContentTypeAllowed("image/png", ["document"])).toBe(false);
      expect(isContentTypeAllowed("video/mp4", ["image", "audio"])).toBe(false);
    });

    it("should return false for unknown content type", () => {
      expect(isContentTypeAllowed("application/octet-stream", ["image", "document"])).toBe(false);
    });

    it("should return false for empty allowed categories", () => {
      expect(isContentTypeAllowed("image/png", [])).toBe(false);
    });
  });

  describe("getAllowedContentTypes", () => {
    it("should return all image MIME types for image category", () => {
      const types = getAllowedContentTypes(["image"]);
      expect(types).toContain("image/jpeg");
      expect(types).toContain("image/png");
      expect(types).toContain("image/webp");
    });

    it("should NOT include image/svg+xml in the image category (B1)", () => {
      const types = getAllowedContentTypes(["image"]);
      expect(types).not.toContain("image/svg+xml");
    });

    it("should return combined MIME types for multiple categories", () => {
      const types = getAllowedContentTypes(["image", "document"]);
      expect(types).toContain("image/jpeg");
      expect(types).toContain("application/pdf");
    });

    it("should return empty array for empty categories", () => {
      expect(getAllowedContentTypes([])).toEqual([]);
    });

    it("should not have duplicates for single category", () => {
      const types = getAllowedContentTypes(["image"]);
      expect(new Set(types).size).toBe(types.length);
    });
  });
});
