import { describe, it, expect } from "vitest";
import {
  createFile,
  transitionFileStatus,
  FilesValidationError,
  FILES_SIZE_LIMITS,
  FILES_FIELD_LIMITS,
} from "@d2/files-domain";
import type { CreateFileInput, FileVariant } from "@d2/files-domain";

const validInput: CreateFileInput = {
  contextKey: "user_avatar",
  relatedEntityId: "usr_01234567-89ab-cdef-0123-456789abcdef",
  uploaderUserId: "user-123",
  contentType: "image/png",
  displayName: "avatar.png",
  sizeBytes: 1024,
};

const validVariant: FileVariant = {
  size: "original",
  key: "files/usr_01234567/original.png",
  width: 0,
  height: 0,
  sizeBytes: 1024,
  contentType: "image/png",
};

describe("File Entity", () => {
  describe("createFile", () => {
    it("should create a file with valid input", () => {
      const file = createFile(validInput);

      expect(file.id).toBeTruthy();
      expect(file.contextKey).toBe("user_avatar");
      expect(file.relatedEntityId).toBe("usr_01234567-89ab-cdef-0123-456789abcdef");
      expect(file.status).toBe("pending");
      expect(file.contentType).toBe("image/png");
      expect(file.displayName).toBe("avatar.png");
      expect(file.sizeBytes).toBe(1024);
      expect(file.variants).toBeUndefined();
      expect(file.rejectionReason).toBeUndefined();
      expect(file.createdAt).toBeInstanceOf(Date);
    });

    it("should generate unique IDs", () => {
      const file1 = createFile(validInput);
      const file2 = createFile(validInput);
      expect(file1.id).not.toBe(file2.id);
    });

    it("should accept a pre-generated ID", () => {
      const file = createFile({ ...validInput, id: "custom-id" });
      expect(file.id).toBe("custom-id");
    });

    it("should trim and clean string fields", () => {
      const file = createFile({
        ...validInput,
        contextKey: "  user_avatar  ",
        relatedEntityId: "  usr_123  ",
        contentType: "  image/png  ",
        displayName: "  my   photo.png  ",
      });

      expect(file.contextKey).toBe("user_avatar");
      expect(file.relatedEntityId).toBe("usr_123");
      expect(file.contentType).toBe("image/png");
      expect(file.displayName).toBe("my photo.png");
    });

    it("should default status to pending", () => {
      const file = createFile(validInput);
      expect(file.status).toBe("pending");
    });

    it("should default variants to undefined", () => {
      const file = createFile(validInput);
      expect(file.variants).toBeUndefined();
    });

    it("should default rejectionReason to undefined", () => {
      const file = createFile(validInput);
      expect(file.rejectionReason).toBeUndefined();
    });

    // --- Validation: contextKey ---

    it("should throw on empty contextKey", () => {
      expect(() => createFile({ ...validInput, contextKey: "" })).toThrow(FilesValidationError);
    });

    it("should throw on whitespace-only contextKey", () => {
      expect(() => createFile({ ...validInput, contextKey: "   " })).toThrow(FilesValidationError);
    });

    it("should throw on contextKey exceeding max length", () => {
      const longKey = "a".repeat(FILES_FIELD_LIMITS.MAX_CONTEXT_KEY_LENGTH + 1);
      expect(() => createFile({ ...validInput, contextKey: longKey })).toThrow(
        FilesValidationError,
      );
    });

    it("should accept contextKey at exact max length", () => {
      const exactKey = "a".repeat(FILES_FIELD_LIMITS.MAX_CONTEXT_KEY_LENGTH);
      const file = createFile({ ...validInput, contextKey: exactKey });
      expect(file.contextKey).toBe(exactKey);
    });

    // --- Validation: relatedEntityId ---

    it("should throw on empty relatedEntityId", () => {
      expect(() => createFile({ ...validInput, relatedEntityId: "" })).toThrow(
        FilesValidationError,
      );
    });

    it("should throw on whitespace-only relatedEntityId", () => {
      expect(() => createFile({ ...validInput, relatedEntityId: "   " })).toThrow(
        FilesValidationError,
      );
    });

    it("should throw on relatedEntityId exceeding max length", () => {
      const longId = "x".repeat(FILES_FIELD_LIMITS.MAX_RELATED_ENTITY_ID_LENGTH + 1);
      expect(() => createFile({ ...validInput, relatedEntityId: longId })).toThrow(
        FilesValidationError,
      );
    });

    // --- Validation: contentType ---

    it("should throw on empty contentType", () => {
      expect(() => createFile({ ...validInput, contentType: "" })).toThrow(FilesValidationError);
    });

    it("should throw on whitespace-only contentType", () => {
      expect(() => createFile({ ...validInput, contentType: "   " })).toThrow(FilesValidationError);
    });

    it("should throw on contentType exceeding max length", () => {
      const longType = "x".repeat(FILES_FIELD_LIMITS.MAX_CONTENT_TYPE_LENGTH + 1);
      expect(() => createFile({ ...validInput, contentType: longType })).toThrow(
        FilesValidationError,
      );
    });

    // --- Validation: displayName ---

    it("should throw on empty displayName", () => {
      expect(() => createFile({ ...validInput, displayName: "" })).toThrow(FilesValidationError);
    });

    it("should throw on whitespace-only displayName", () => {
      expect(() => createFile({ ...validInput, displayName: "   " })).toThrow(FilesValidationError);
    });

    it("should throw on displayName exceeding max length", () => {
      const longName = "x".repeat(FILES_FIELD_LIMITS.MAX_DISPLAY_NAME_LENGTH + 1);
      expect(() => createFile({ ...validInput, displayName: longName })).toThrow(
        FilesValidationError,
      );
    });

    // --- Validation: sizeBytes ---

    it("should throw on zero sizeBytes", () => {
      expect(() => createFile({ ...validInput, sizeBytes: 0 })).toThrow(FilesValidationError);
    });

    it("should throw on negative sizeBytes", () => {
      expect(() => createFile({ ...validInput, sizeBytes: -1 })).toThrow(FilesValidationError);
    });

    it("should throw on NaN sizeBytes", () => {
      expect(() => createFile({ ...validInput, sizeBytes: NaN })).toThrow(FilesValidationError);
    });

    it("should throw on Infinity sizeBytes", () => {
      expect(() => createFile({ ...validInput, sizeBytes: Infinity })).toThrow(
        FilesValidationError,
      );
    });

    it("should throw when sizeBytes exceeds default limit", () => {
      expect(() =>
        createFile({
          ...validInput,
          sizeBytes: FILES_SIZE_LIMITS.DEFAULT_MAX_SIZE_BYTES + 1,
        }),
      ).toThrow(FilesValidationError);
    });

    it("should accept sizeBytes at exact default limit", () => {
      const file = createFile({
        ...validInput,
        sizeBytes: FILES_SIZE_LIMITS.DEFAULT_MAX_SIZE_BYTES,
      });
      expect(file.sizeBytes).toBe(FILES_SIZE_LIMITS.DEFAULT_MAX_SIZE_BYTES);
    });

    it("should respect custom maxSizeBytes override", () => {
      const customMax = 1024;
      expect(() =>
        createFile({ ...validInput, sizeBytes: customMax + 1, maxSizeBytes: customMax }),
      ).toThrow(FilesValidationError);
    });

    it("should accept sizeBytes at custom maxSizeBytes", () => {
      const customMax = 1024;
      const file = createFile({ ...validInput, sizeBytes: customMax, maxSizeBytes: customMax });
      expect(file.sizeBytes).toBe(customMax);
    });
  });

  describe("transitionFileStatus", () => {
    it("should transition pending to processing", () => {
      const file = createFile(validInput);
      const updated = transitionFileStatus(file, "processing");
      expect(updated.status).toBe("processing");
    });

    it("should transition pending to rejected with reason", () => {
      const file = createFile(validInput);
      const updated = transitionFileStatus(file, "rejected", {
        rejectionReason: "size_exceeded",
      });
      expect(updated.status).toBe("rejected");
      expect(updated.rejectionReason).toBe("size_exceeded");
    });

    it("should transition processing to ready with variants", () => {
      const file = createFile(validInput);
      const processing = transitionFileStatus(file, "processing");
      const ready = transitionFileStatus(processing, "ready", {
        variants: [validVariant],
      });
      expect(ready.status).toBe("ready");
      expect(ready.variants).toHaveLength(1);
      expect(ready.variants![0]).toEqual(validVariant);
    });

    it("should transition processing to rejected with reason", () => {
      const file = createFile(validInput);
      const processing = transitionFileStatus(file, "processing");
      const rejected = transitionFileStatus(processing, "rejected", {
        rejectionReason: "corrupt_file",
      });
      expect(rejected.status).toBe("rejected");
      expect(rejected.rejectionReason).toBe("corrupt_file");
    });

    // --- Invalid transitions ---

    it("should throw on pending to ready (skip processing)", () => {
      const file = createFile(validInput);
      expect(() => transitionFileStatus(file, "ready", { variants: [validVariant] })).toThrow(
        FilesValidationError,
      );
    });

    it("should throw on ready to any state (terminal)", () => {
      const file = createFile(validInput);
      const processing = transitionFileStatus(file, "processing");
      const ready = transitionFileStatus(processing, "ready", { variants: [validVariant] });

      expect(() => transitionFileStatus(ready, "pending")).toThrow(FilesValidationError);
      expect(() => transitionFileStatus(ready, "processing")).toThrow(FilesValidationError);
      expect(() =>
        transitionFileStatus(ready, "rejected", { rejectionReason: "corrupt_file" }),
      ).toThrow(FilesValidationError);
    });

    it("should throw on rejected to any state (terminal)", () => {
      const file = createFile(validInput);
      const rejected = transitionFileStatus(file, "rejected", {
        rejectionReason: "size_exceeded",
      });

      expect(() => transitionFileStatus(rejected, "pending")).toThrow(FilesValidationError);
      expect(() => transitionFileStatus(rejected, "processing")).toThrow(FilesValidationError);
      expect(() => transitionFileStatus(rejected, "ready", { variants: [validVariant] })).toThrow(
        FilesValidationError,
      );
    });

    it("should throw on processing to pending (no backward)", () => {
      const file = createFile(validInput);
      const processing = transitionFileStatus(file, "processing");
      expect(() => transitionFileStatus(processing, "pending")).toThrow(FilesValidationError);
    });

    // --- Rejected requires rejectionReason ---

    it("should throw when transitioning to rejected without rejectionReason", () => {
      const file = createFile(validInput);
      expect(() => transitionFileStatus(file, "rejected")).toThrow(FilesValidationError);
    });

    it("should throw when transitioning to rejected with undefined rejectionReason", () => {
      const file = createFile(validInput);
      expect(() => transitionFileStatus(file, "rejected", {})).toThrow(FilesValidationError);
    });

    it("should throw when transitioning to rejected with invalid rejectionReason", () => {
      const file = createFile(validInput);
      expect(() =>
        transitionFileStatus(file, "rejected", {
          rejectionReason: "not_a_real_reason" as any,
        }),
      ).toThrow(FilesValidationError);
    });

    // --- Ready requires variants ---

    it("should throw when transitioning to ready without variants", () => {
      const file = createFile(validInput);
      const processing = transitionFileStatus(file, "processing");
      expect(() => transitionFileStatus(processing, "ready")).toThrow(FilesValidationError);
    });

    it("should throw when transitioning to ready with empty variants array", () => {
      const file = createFile(validInput);
      const processing = transitionFileStatus(file, "processing");
      expect(() => transitionFileStatus(processing, "ready", { variants: [] })).toThrow(
        FilesValidationError,
      );
    });

    // --- Field preservation ---

    it("should preserve all other fields after transition", () => {
      const file = createFile(validInput);
      const processing = transitionFileStatus(file, "processing");

      expect(processing.id).toBe(file.id);
      expect(processing.contextKey).toBe(file.contextKey);
      expect(processing.relatedEntityId).toBe(file.relatedEntityId);
      expect(processing.contentType).toBe(file.contentType);
      expect(processing.displayName).toBe(file.displayName);
      expect(processing.sizeBytes).toBe(file.sizeBytes);
      expect(processing.createdAt).toBe(file.createdAt);
    });

    it("should accept all valid rejection reasons", () => {
      const reasons = [
        "size_exceeded",
        "invalid_content_type",
        "magic_bytes_mismatch",
        "content_moderation_failed",
        "processing_timeout",
        "corrupt_file",
      ] as const;

      for (const reason of reasons) {
        const file = createFile(validInput);
        const rejected = transitionFileStatus(file, "rejected", { rejectionReason: reason });
        expect(rejected.rejectionReason).toBe(reason);
      }
    });
  });
});
