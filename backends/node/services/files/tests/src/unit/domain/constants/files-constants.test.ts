import { describe, it, expect } from "vitest";
import {
  FILES_SIZE_LIMITS,
  FILES_FIELD_LIMITS,
  ALLOWED_CONTENT_TYPES,
  FILES_MESSAGING,
} from "@d2/files-domain";

describe("Files Constants", () => {
  describe("FILES_SIZE_LIMITS", () => {
    it("should have correct default max size (25 MB)", () => {
      expect(FILES_SIZE_LIMITS.DEFAULT_MAX_SIZE_BYTES).toBe(25 * 1024 * 1024);
    });
  });

  describe("FILES_FIELD_LIMITS", () => {
    it("should have positive max lengths", () => {
      expect(FILES_FIELD_LIMITS.MAX_CONTEXT_KEY_LENGTH).toBeGreaterThan(0);
      expect(FILES_FIELD_LIMITS.MAX_RELATED_ENTITY_ID_LENGTH).toBeGreaterThan(0);
      expect(FILES_FIELD_LIMITS.MAX_CONTENT_TYPE_LENGTH).toBeGreaterThan(0);
      expect(FILES_FIELD_LIMITS.MAX_DISPLAY_NAME_LENGTH).toBeGreaterThan(0);
      expect(FILES_FIELD_LIMITS.MAX_VARIANT_KEY_LENGTH).toBeGreaterThan(0);
    });
  });

  describe("ALLOWED_CONTENT_TYPES", () => {
    it("should include jpeg, png, and webp for images", () => {
      expect(ALLOWED_CONTENT_TYPES.image).toContain("image/jpeg");
      expect(ALLOWED_CONTENT_TYPES.image).toContain("image/png");
      expect(ALLOWED_CONTENT_TYPES.image).toContain("image/webp");
    });

    it("should include HEIC/HEIF for smartphone photos", () => {
      expect(ALLOWED_CONTENT_TYPES.image).toContain("image/heic");
      expect(ALLOWED_CONTENT_TYPES.image).toContain("image/heif");
    });

    it("should include pdf for documents", () => {
      expect(ALLOWED_CONTENT_TYPES.document).toContain("application/pdf");
    });

    it("should include mp4 and 3gpp for video", () => {
      expect(ALLOWED_CONTENT_TYPES.video).toContain("video/mp4");
      expect(ALLOWED_CONTENT_TYPES.video).toContain("video/3gpp");
    });

    it("should include mpeg, aac, and m4a for audio", () => {
      expect(ALLOWED_CONTENT_TYPES.audio).toContain("audio/mpeg");
      expect(ALLOWED_CONTENT_TYPES.audio).toContain("audio/aac");
      expect(ALLOWED_CONTENT_TYPES.audio).toContain("audio/mp4");
    });
  });

  describe("FILES_MESSAGING", () => {
    it("should have direct exchange type for competing consumers", () => {
      expect(FILES_MESSAGING.EVENTS_EXCHANGE_TYPE).toBe("direct");
    });

    it("should define exchange, routing keys, and queue names", () => {
      expect(FILES_MESSAGING.EVENTS_EXCHANGE).toBeTruthy();
      expect(FILES_MESSAGING.UPLOAD_ROUTING_KEY).toBeTruthy();
      expect(FILES_MESSAGING.PROCESSING_ROUTING_KEY).toBeTruthy();
      expect(FILES_MESSAGING.INTAKE_QUEUE).toBeTruthy();
      expect(FILES_MESSAGING.PROCESSING_QUEUE).toBeTruthy();
    });
  });
});
