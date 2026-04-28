import { describe, it, expect } from "vitest";
import {
  buildRawStorageKey,
  buildVariantStorageKey,
  getExtensionForContentType,
} from "@d2/files-domain";

describe("Storage Key Utilities", () => {
  describe("getExtensionForContentType", () => {
    it.each([
      ["image/jpeg", "jpg"],
      ["image/png", "png"],
      ["image/webp", "webp"],
      ["application/pdf", "pdf"],
      ["video/mp4", "mp4"],
      ["audio/mpeg", "mp3"],
      ["audio/mp4", "m4a"],
      ["video/quicktime", "mov"],
    ])("should return '%s' → '%s'", (mime, ext) => {
      expect(getExtensionForContentType(mime)).toBe(ext);
    });

    it("should return 'bin' for unknown MIME types", () => {
      expect(getExtensionForContentType("application/octet-stream")).toBe("bin");
      expect(getExtensionForContentType("foo/bar")).toBe("bin");
    });
  });

  describe("buildRawStorageKey", () => {
    it("should build correct key format", () => {
      const key = buildRawStorageKey({
        contextKey: "user_avatar",
        relatedEntityId: "user-123",
        id: "file-456",
        contentType: "image/jpeg",
      });
      expect(key).toBe("user_avatar/user-123/file-456/raw.jpg");
    });

    it("should use 'bin' for unknown content type", () => {
      const key = buildRawStorageKey({
        contextKey: "custom",
        relatedEntityId: "entity-1",
        id: "file-1",
        contentType: "application/unknown",
      });
      expect(key).toBe("custom/entity-1/file-1/raw.bin");
    });

    it("should handle PDF content type", () => {
      const key = buildRawStorageKey({
        contextKey: "org_document",
        relatedEntityId: "org-789",
        id: "doc-001",
        contentType: "application/pdf",
      });
      expect(key).toBe("org_document/org-789/doc-001/raw.pdf");
    });
  });

  describe("buildVariantStorageKey", () => {
    it("should build correct variant key format", () => {
      const key = buildVariantStorageKey(
        { contextKey: "user_avatar", relatedEntityId: "user-123", id: "file-456" },
        "thumb",
        "image/webp",
      );
      expect(key).toBe("user_avatar/user-123/file-456/thumb.webp");
    });

    it("should handle original variant", () => {
      const key = buildVariantStorageKey(
        { contextKey: "org_logo", relatedEntityId: "org-1", id: "file-1" },
        "original",
        "image/png",
      );
      expect(key).toBe("org_logo/org-1/file-1/original.png");
    });
  });
});
