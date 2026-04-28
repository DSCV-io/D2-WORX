import { describe, it, expect } from "vitest";
import { createFileVariant, FilesValidationError, FILES_FIELD_LIMITS } from "@d2/files-domain";
import type { CreateFileVariantInput } from "@d2/files-domain";

const validInput: CreateFileVariantInput = {
  size: "medium",
  key: "files/usr_123/medium.png",
  width: 512,
  height: 512,
  sizeBytes: 2048,
  contentType: "image/png",
};

describe("FileVariant", () => {
  describe("createFileVariant", () => {
    it("should create a variant with valid input", () => {
      const variant = createFileVariant(validInput);

      expect(variant.size).toBe("medium");
      expect(variant.key).toBe("files/usr_123/medium.png");
      expect(variant.width).toBe(512);
      expect(variant.height).toBe(512);
      expect(variant.sizeBytes).toBe(2048);
      expect(variant.contentType).toBe("image/png");
    });

    it("should trim string fields", () => {
      const variant = createFileVariant({
        ...validInput,
        key: "  files/usr_123/medium.png  ",
        contentType: "  image/png  ",
      });

      expect(variant.key).toBe("files/usr_123/medium.png");
      expect(variant.contentType).toBe("image/png");
    });

    // --- Validation: size ---

    it("should accept any non-empty string as size", () => {
      const variant = createFileVariant({ ...validInput, size: "custom_preview" });
      expect(variant.size).toBe("custom_preview");
    });

    it("should throw on empty size", () => {
      expect(() => createFileVariant({ ...validInput, size: "" })).toThrow(FilesValidationError);
    });

    it("should throw on whitespace-only size", () => {
      expect(() => createFileVariant({ ...validInput, size: "   " })).toThrow(FilesValidationError);
    });

    it("should trim size", () => {
      const variant = createFileVariant({ ...validInput, size: "  thumb  " });
      expect(variant.size).toBe("thumb");
    });

    // --- Validation: key ---

    it("should throw on empty key", () => {
      expect(() => createFileVariant({ ...validInput, key: "" })).toThrow(FilesValidationError);
    });

    it("should throw on whitespace-only key", () => {
      expect(() => createFileVariant({ ...validInput, key: "   " })).toThrow(FilesValidationError);
    });

    it("should throw on key exceeding max length", () => {
      const longKey = "k".repeat(FILES_FIELD_LIMITS.MAX_VARIANT_KEY_LENGTH + 1);
      expect(() => createFileVariant({ ...validInput, key: longKey })).toThrow(
        FilesValidationError,
      );
    });

    it("should accept key at exact max length", () => {
      const exactKey = "k".repeat(FILES_FIELD_LIMITS.MAX_VARIANT_KEY_LENGTH);
      const variant = createFileVariant({ ...validInput, key: exactKey });
      expect(variant.key).toBe(exactKey);
    });

    // --- Validation: width ---

    it("should throw on negative width", () => {
      expect(() => createFileVariant({ ...validInput, width: -1 })).toThrow(FilesValidationError);
    });

    it("should accept zero width (for original)", () => {
      const variant = createFileVariant({ ...validInput, size: "original", width: 0, height: 0 });
      expect(variant.width).toBe(0);
    });

    it("should throw on NaN width", () => {
      expect(() => createFileVariant({ ...validInput, width: NaN })).toThrow(FilesValidationError);
    });

    // --- Validation: height ---

    it("should throw on negative height", () => {
      expect(() => createFileVariant({ ...validInput, height: -1 })).toThrow(FilesValidationError);
    });

    it("should accept zero height (for original)", () => {
      const variant = createFileVariant({ ...validInput, size: "original", width: 0, height: 0 });
      expect(variant.height).toBe(0);
    });

    it("should throw on Infinity height", () => {
      expect(() => createFileVariant({ ...validInput, height: Infinity })).toThrow(
        FilesValidationError,
      );
    });

    // --- Validation: sizeBytes ---

    it("should throw on zero sizeBytes", () => {
      expect(() => createFileVariant({ ...validInput, sizeBytes: 0 })).toThrow(
        FilesValidationError,
      );
    });

    it("should throw on negative sizeBytes", () => {
      expect(() => createFileVariant({ ...validInput, sizeBytes: -1 })).toThrow(
        FilesValidationError,
      );
    });

    it("should throw on NaN sizeBytes", () => {
      expect(() => createFileVariant({ ...validInput, sizeBytes: NaN })).toThrow(
        FilesValidationError,
      );
    });

    it("should throw on Infinity sizeBytes", () => {
      expect(() => createFileVariant({ ...validInput, sizeBytes: Infinity })).toThrow(
        FilesValidationError,
      );
    });

    // --- Validation: contentType ---

    it("should throw on empty contentType", () => {
      expect(() => createFileVariant({ ...validInput, contentType: "" })).toThrow(
        FilesValidationError,
      );
    });

    it("should throw on whitespace-only contentType", () => {
      expect(() => createFileVariant({ ...validInput, contentType: "   " })).toThrow(
        FilesValidationError,
      );
    });
  });
});
