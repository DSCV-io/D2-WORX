import { describe, it, expect } from "vitest";
import { toFile } from "@d2/files-infra";
import type { FileRow } from "@d2/files-infra";

describe("toFile", () => {
  const baseRow: FileRow = {
    id: "file-001",
    contextKey: "user_avatar",
    relatedEntityId: "user-123",
    uploaderUserId: "user-123",
    status: "pending",
    contentType: "image/jpeg",
    displayName: "avatar.jpg",
    sizeBytes: 2048,
    variants: null,
    rejectionReason: null,
    createdAt: new Date("2026-01-15T10:00:00Z"),
    updatedAt: new Date("2026-01-15T10:00:00Z"),
  };

  it("should map all scalar fields correctly", () => {
    const file = toFile(baseRow);

    expect(file.id).toBe("file-001");
    expect(file.contextKey).toBe("user_avatar");
    expect(file.relatedEntityId).toBe("user-123");
    expect(file.status).toBe("pending");
    expect(file.contentType).toBe("image/jpeg");
    expect(file.displayName).toBe("avatar.jpg");
    expect(file.sizeBytes).toBe(2048);
    expect(file.createdAt).toEqual(new Date("2026-01-15T10:00:00Z"));
  });

  it("should map variants as undefined when row.variants is null", () => {
    const file = toFile(baseRow);

    expect(file.variants).toBeUndefined();
  });

  it("should map variants as FileVariant[] when present", () => {
    const variants = [
      {
        size: "thumb",
        key: "user_avatar/user-123/file-001/thumb.jpg",
        width: 64,
        height: 64,
        sizeBytes: 512,
        contentType: "image/jpeg",
      },
      {
        size: "medium",
        key: "user_avatar/user-123/file-001/medium.jpg",
        width: 256,
        height: 256,
        sizeBytes: 4096,
        contentType: "image/jpeg",
      },
    ];
    const row: FileRow = { ...baseRow, variants };
    const file = toFile(row);

    expect(file.variants).toHaveLength(2);
    expect(file.variants![0].size).toBe("thumb");
    expect(file.variants![0].width).toBe(64);
    expect(file.variants![1].size).toBe("medium");
    expect(file.variants![1].key).toBe("user_avatar/user-123/file-001/medium.jpg");
  });

  it("should map rejectionReason as undefined when absent", () => {
    const file = toFile(baseRow);

    expect(file.rejectionReason).toBeUndefined();
  });

  it("should map rejectionReason when present", () => {
    const row: FileRow = {
      ...baseRow,
      status: "rejected",
      rejectionReason: "size_exceeded",
    };
    const file = toFile(row);

    expect(file.rejectionReason).toBe("size_exceeded");
  });

  it("should map empty string rejectionReason as undefined", () => {
    const row: FileRow = {
      ...baseRow,
      rejectionReason: "",
    };
    const file = toFile(row);

    expect(file.rejectionReason).toBeUndefined();
  });

  it("should map whitespace-only rejectionReason as undefined", () => {
    const row: FileRow = {
      ...baseRow,
      rejectionReason: "   ",
    };
    const file = toFile(row);

    expect(file.rejectionReason).toBeUndefined();
  });

  it("should trim rejectionReason whitespace when present", () => {
    const row: FileRow = {
      ...baseRow,
      status: "rejected",
      rejectionReason: "  size_exceeded  ",
    };
    const file = toFile(row);

    expect(file.rejectionReason).toBe("size_exceeded");
  });

  it("should cast status as FileStatus", () => {
    const readyRow: FileRow = { ...baseRow, status: "ready" };
    const file = toFile(readyRow);

    expect(file.status).toBe("ready");
  });

  it("should handle all file statuses", () => {
    const statuses = ["pending", "processing", "ready", "rejected"] as const;

    for (const status of statuses) {
      const row: FileRow = { ...baseRow, status };
      const file = toFile(row);
      expect(file.status).toBe(status);
    }
  });

  it("should handle all rejection reasons", () => {
    const reasons = [
      "size_exceeded",
      "invalid_content_type",
      "magic_bytes_mismatch",
      "content_moderation_failed",
      "processing_timeout",
      "corrupt_file",
    ] as const;

    for (const reason of reasons) {
      const row: FileRow = { ...baseRow, status: "rejected", rejectionReason: reason };
      const file = toFile(row);
      expect(file.rejectionReason).toBe(reason);
    }
  });

  it("should preserve Date objects (not serialize to string)", () => {
    const file = toFile(baseRow);

    expect(file.createdAt).toBeInstanceOf(Date);
  });

  it("should not include updatedAt in the mapped File entity", () => {
    const file = toFile(baseRow);

    // File domain entity does not have updatedAt — only the DB row does
    expect("updatedAt" in file).toBe(false);
  });

  it("should handle zero sizeBytes", () => {
    const row: FileRow = { ...baseRow, sizeBytes: 0 };
    const file = toFile(row);

    expect(file.sizeBytes).toBe(0);
  });

  it("should handle large sizeBytes (bigint range)", () => {
    const row: FileRow = { ...baseRow, sizeBytes: 5_000_000_000 };
    const file = toFile(row);

    expect(file.sizeBytes).toBe(5_000_000_000);
  });

  it("should handle empty string fields", () => {
    const row: FileRow = {
      ...baseRow,
      contextKey: "",
      relatedEntityId: "",
      contentType: "",
      displayName: "",
    };
    const file = toFile(row);

    expect(file.contextKey).toBe("");
    expect(file.relatedEntityId).toBe("");
    expect(file.contentType).toBe("");
    expect(file.displayName).toBe("");
  });
});
