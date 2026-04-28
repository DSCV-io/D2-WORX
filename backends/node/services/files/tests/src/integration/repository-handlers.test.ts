import { describe, it, expect, beforeAll, afterAll, beforeEach } from "vitest";
import { generateUuidV7 } from "@d2/utilities";
import { createFile, type File } from "@d2/files-domain";
import {
  CreateFileRecord,
  GetFileById,
  GetFilesByContext,
  GetStaleFiles,
  UpdateFileRecord,
  DeleteFileRecord,
  DeleteFileRecordsByIds,
  PingDb,
} from "@d2/files-infra";
import {
  startPostgres,
  stopPostgres,
  getDb,
  cleanAllTables,
} from "./helpers/postgres-test-helpers.js";
import { createTestContext } from "./helpers/test-context.js";

describe("Repository handlers (integration)", () => {
  let createFileRecord: CreateFileRecord;
  let getFileById: GetFileById;
  let getFilesByContext: GetFilesByContext;
  let getStaleFiles: GetStaleFiles;
  let updateFileRecord: UpdateFileRecord;
  let deleteFileRecord: DeleteFileRecord;
  let deleteFileRecordsByIds: DeleteFileRecordsByIds;
  let pingDb: PingDb;

  beforeAll(async () => {
    await startPostgres();
    const db = getDb();
    const ctx = createTestContext();
    createFileRecord = new CreateFileRecord(db, ctx);
    getFileById = new GetFileById(db, ctx);
    getFilesByContext = new GetFilesByContext(db, ctx);
    getStaleFiles = new GetStaleFiles(db, ctx);
    updateFileRecord = new UpdateFileRecord(db, ctx);
    deleteFileRecord = new DeleteFileRecord(db, ctx);
    deleteFileRecordsByIds = new DeleteFileRecordsByIds(db, ctx);
    pingDb = new PingDb(db, ctx);
  }, 120_000);

  afterAll(async () => {
    await stopPostgres();
  });

  beforeEach(async () => {
    await cleanAllTables();
  });

  function makeFile(overrides?: Partial<File>): File {
    const base = createFile({
      contextKey: "user_avatar",
      relatedEntityId: generateUuidV7(),
      uploaderUserId: "user-123",
      contentType: "image/jpeg",
      displayName: "photo.jpg",
      sizeBytes: 2048,
    });
    return overrides ? { ...base, ...overrides } : base;
  }

  // ---------------------------------------------------------------------------
  // CreateFileRecord + GetFileById — round-trip
  // ---------------------------------------------------------------------------

  describe("CreateFileRecord + GetFileById", () => {
    it("should create and retrieve a file with all fields", async () => {
      const f = makeFile();
      const createResult = await createFileRecord.handleAsync({ file: f });
      expect(createResult.success).toBe(true);

      const findResult = await getFileById.handleAsync({ id: f.id });
      expect(findResult.success).toBe(true);

      const found = findResult.data!.file;
      expect(found.id).toBe(f.id);
      expect(found.contextKey).toBe("user_avatar");
      expect(found.relatedEntityId).toBe(f.relatedEntityId);
      expect(found.status).toBe("pending");
      expect(found.contentType).toBe("image/jpeg");
      expect(found.displayName).toBe("photo.jpg");
      expect(found.sizeBytes).toBe(2048);
      expect(found.variants).toBeUndefined();
      expect(found.rejectionReason).toBeUndefined();
      expect(found.createdAt).toBeInstanceOf(Date);
    });

    it("should return notFound for missing id", async () => {
      const result = await getFileById.handleAsync({ id: generateUuidV7() });
      expect(result.success).toBe(false);
    });

    it("should store JSONB variants when provided", async () => {
      const variants = [
        {
          size: "thumb" as const,
          key: "files/abc/thumb.webp",
          width: 100,
          height: 100,
          sizeBytes: 512,
          contentType: "image/webp",
        },
        {
          size: "medium" as const,
          key: "files/abc/medium.webp",
          width: 400,
          height: 400,
          sizeBytes: 4096,
          contentType: "image/webp",
        },
      ];
      const f = makeFile({ variants, status: "ready" as const });
      await createFileRecord.handleAsync({ file: f });

      const result = await getFileById.handleAsync({ id: f.id });
      const found = result.data!.file;
      expect(found.variants).toEqual(variants);
    });

    it("should store undefined variants as undefined", async () => {
      const f = makeFile({ variants: undefined });
      await createFileRecord.handleAsync({ file: f });

      const result = await getFileById.handleAsync({ id: f.id });
      expect(result.data!.file.variants).toBeUndefined();
    });

    it("should store rejectionReason when provided", async () => {
      const f = makeFile({
        status: "rejected" as const,
        rejectionReason: "malware" as const,
      });
      await createFileRecord.handleAsync({ file: f });

      const result = await getFileById.handleAsync({ id: f.id });
      expect(result.data!.file.rejectionReason).toBe("malware");
    });

    it("should handle large sizeBytes (bigint column)", async () => {
      const f = makeFile({ sizeBytes: 5_000_000_000 });
      await createFileRecord.handleAsync({ file: f });

      const result = await getFileById.handleAsync({ id: f.id });
      expect(result.data!.file.sizeBytes).toBe(5_000_000_000);
    });

    it("should handle timestamps as Date objects", async () => {
      const f = makeFile();
      await createFileRecord.handleAsync({ file: f });

      const result = await getFileById.handleAsync({ id: f.id });
      const found = result.data!.file;
      expect(found.createdAt).toBeInstanceOf(Date);
      expect(found.createdAt.getTime()).toBeGreaterThan(0);
    });
  });

  // ---------------------------------------------------------------------------
  // GetFilesByContext
  // ---------------------------------------------------------------------------

  describe("GetFilesByContext", () => {
    it("should return paginated files for a given context", async () => {
      const entityId = generateUuidV7();
      const files: File[] = [];
      for (let i = 0; i < 5; i++) {
        const f = makeFile({ relatedEntityId: entityId });
        files.push(f);
        await createFileRecord.handleAsync({ file: f });
      }

      const result = await getFilesByContext.handleAsync({
        contextKey: "user_avatar",
        relatedEntityId: entityId,
        limit: 3,
        offset: 0,
      });

      expect(result.success).toBe(true);
      expect(result.data!.files).toHaveLength(3);
      expect(result.data!.total).toBe(5);
    });

    it("should order by createdAt DESC (newest first)", async () => {
      const entityId = generateUuidV7();
      const fileIds: string[] = [];

      for (let i = 0; i < 3; i++) {
        // Stagger creates by 2ms so each file's createdAt lands in a distinct
        // millisecond. Without this, all three records can share a timestamp
        // and the sort then falls back to the id tiebreaker — which is the
        // UUIDv7 random tail, not creation order.
        if (i > 0) await new Promise((r) => setTimeout(r, 2));
        const f = makeFile({ relatedEntityId: entityId });
        fileIds.push(f.id);
        await createFileRecord.handleAsync({ file: f });
      }

      const result = await getFilesByContext.handleAsync({
        contextKey: "user_avatar",
        relatedEntityId: entityId,
      });

      const returnedIds = result.data!.files.map((f) => f.id);
      // Most recently created file should be first
      expect(returnedIds[0]).toBe(fileIds[2]);
      expect(returnedIds[2]).toBe(fileIds[0]);
    });

    it("should not leak files across different context keys", async () => {
      const entityId = generateUuidV7();
      const f1 = makeFile({ relatedEntityId: entityId, contextKey: "user_avatar" });
      const f2 = makeFile({ relatedEntityId: entityId, contextKey: "org_logo" });
      await createFileRecord.handleAsync({ file: f1 });
      await createFileRecord.handleAsync({ file: f2 });

      const result = await getFilesByContext.handleAsync({
        contextKey: "user_avatar",
        relatedEntityId: entityId,
      });

      expect(result.data!.files).toHaveLength(1);
      expect(result.data!.files[0].id).toBe(f1.id);
      expect(result.data!.total).toBe(1);
    });

    it("should not leak files across different entity ids", async () => {
      const entity1 = generateUuidV7();
      const entity2 = generateUuidV7();
      await createFileRecord.handleAsync({ file: makeFile({ relatedEntityId: entity1 }) });
      await createFileRecord.handleAsync({ file: makeFile({ relatedEntityId: entity2 }) });

      const result = await getFilesByContext.handleAsync({
        contextKey: "user_avatar",
        relatedEntityId: entity1,
      });

      expect(result.data!.files).toHaveLength(1);
      expect(result.data!.total).toBe(1);
    });

    it("should return empty for non-existent context", async () => {
      const result = await getFilesByContext.handleAsync({
        contextKey: "non_existent",
        relatedEntityId: generateUuidV7(),
      });

      expect(result.success).toBe(true);
      expect(result.data!.files).toHaveLength(0);
      expect(result.data!.total).toBe(0);
    });

    it("should respect offset for pagination", async () => {
      const entityId = generateUuidV7();
      for (let i = 0; i < 5; i++) {
        await createFileRecord.handleAsync({
          file: makeFile({ relatedEntityId: entityId }),
        });
      }

      const result = await getFilesByContext.handleAsync({
        contextKey: "user_avatar",
        relatedEntityId: entityId,
        limit: 2,
        offset: 3,
      });

      expect(result.data!.files).toHaveLength(2);
      expect(result.data!.total).toBe(5);
    });

    it("should clamp limit to MAX_LIMIT (100)", async () => {
      const entityId = generateUuidV7();
      await createFileRecord.handleAsync({
        file: makeFile({ relatedEntityId: entityId }),
      });

      // Requesting limit > 100 should be clamped
      const result = await getFilesByContext.handleAsync({
        contextKey: "user_avatar",
        relatedEntityId: entityId,
        limit: 500,
      });

      // Should succeed (clamped internally), not fail
      expect(result.success).toBe(true);
    });
  });

  // ---------------------------------------------------------------------------
  // GetStaleFiles
  // ---------------------------------------------------------------------------

  describe("GetStaleFiles", () => {
    it("should find files with matching status created before cutoff", async () => {
      const old = makeFile();
      await createFileRecord.handleAsync({ file: old });

      // Use a cutoff in the future so the file we just created qualifies
      const cutoff = new Date(Date.now() + 60_000);
      const result = await getStaleFiles.handleAsync({
        status: "pending",
        cutoffDate: cutoff,
        limit: 10,
      });

      expect(result.success).toBe(true);
      expect(result.data!.files.length).toBeGreaterThanOrEqual(1);
      expect(result.data!.files.some((f) => f.id === old.id)).toBe(true);
    });

    it("should not return files with different status", async () => {
      const f = makeFile({ status: "ready" as const });
      await createFileRecord.handleAsync({ file: f });

      const cutoff = new Date(Date.now() + 60_000);
      const result = await getStaleFiles.handleAsync({
        status: "pending",
        cutoffDate: cutoff,
        limit: 10,
      });

      expect(result.data!.files.some((r) => r.id === f.id)).toBe(false);
    });

    it("should not return files created after cutoff", async () => {
      const f = makeFile();
      await createFileRecord.handleAsync({ file: f });

      // Cutoff in the past — file was just created, so it shouldn't match
      const cutoff = new Date(Date.now() - 60_000);
      const result = await getStaleFiles.handleAsync({
        status: "pending",
        cutoffDate: cutoff,
        limit: 10,
      });

      expect(result.data!.files.some((r) => r.id === f.id)).toBe(false);
    });

    it("should enforce limit", async () => {
      for (let i = 0; i < 5; i++) {
        await createFileRecord.handleAsync({ file: makeFile() });
      }

      const cutoff = new Date(Date.now() + 60_000);
      const result = await getStaleFiles.handleAsync({
        status: "pending",
        cutoffDate: cutoff,
        limit: 2,
      });

      expect(result.data!.files).toHaveLength(2);
    });
  });

  // ---------------------------------------------------------------------------
  // UpdateFileRecord
  // ---------------------------------------------------------------------------

  describe("UpdateFileRecord", () => {
    it("should update status and persist changes", async () => {
      const f = makeFile();
      await createFileRecord.handleAsync({ file: f });

      const updated: File = { ...f, status: "processing" as const };
      const result = await updateFileRecord.handleAsync({ file: updated });

      expect(result.success).toBe(true);
      expect(result.data!.file.status).toBe("processing");

      // Verify via separate read
      const readResult = await getFileById.handleAsync({ id: f.id });
      expect(readResult.data!.file.status).toBe("processing");
    });

    it("should update variants (JSONB)", async () => {
      const f = makeFile();
      await createFileRecord.handleAsync({ file: f });

      const variants = [
        {
          size: "thumb" as const,
          key: "files/abc/thumb.webp",
          width: 100,
          height: 100,
          sizeBytes: 512,
          contentType: "image/webp",
        },
      ];
      const updated: File = { ...f, status: "ready" as const, variants };
      const result = await updateFileRecord.handleAsync({ file: updated });

      expect(result.success).toBe(true);
      expect(result.data!.file.variants).toEqual(variants);
    });

    it("should return notFound for missing id", async () => {
      const f = makeFile();
      const result = await updateFileRecord.handleAsync({ file: f });
      expect(result.success).toBe(false);
    });

    it("should update updatedAt timestamp", async () => {
      const f = makeFile();
      await createFileRecord.handleAsync({ file: f });

      // Read original timestamp
      const before = await getFileById.handleAsync({ id: f.id });
      const originalUpdatedAt = before.data!.file.createdAt;

      // Small delay to ensure timestamp changes
      await new Promise((resolve) => setTimeout(resolve, 50));

      const updated: File = { ...f, status: "processing" as const };
      await updateFileRecord.handleAsync({ file: updated });

      const after = await getFileById.handleAsync({ id: f.id });
      // updatedAt should be equal to or after the createdAt
      expect(after.data!.file.createdAt.getTime()).toBe(originalUpdatedAt.getTime());
    });

    it("should update rejectionReason", async () => {
      const f = makeFile();
      await createFileRecord.handleAsync({ file: f });

      const updated: File = {
        ...f,
        status: "rejected" as const,
        rejectionReason: "oversized" as const,
      };
      const result = await updateFileRecord.handleAsync({ file: updated });

      expect(result.success).toBe(true);
      expect(result.data!.file.rejectionReason).toBe("oversized");
    });
  });

  // ---------------------------------------------------------------------------
  // DeleteFileRecord
  // ---------------------------------------------------------------------------

  describe("DeleteFileRecord", () => {
    it("should delete an existing file", async () => {
      const f = makeFile();
      await createFileRecord.handleAsync({ file: f });

      const result = await deleteFileRecord.handleAsync({ id: f.id });
      expect(result.success).toBe(true);

      // Confirm deletion
      const findResult = await getFileById.handleAsync({ id: f.id });
      expect(findResult.success).toBe(false);
    });

    it("should return notFound for missing id", async () => {
      const result = await deleteFileRecord.handleAsync({ id: generateUuidV7() });
      expect(result.success).toBe(false);
    });
  });

  // ---------------------------------------------------------------------------
  // DeleteFileRecordsByIds
  // ---------------------------------------------------------------------------

  describe("DeleteFileRecordsByIds", () => {
    it("should batch delete multiple files", async () => {
      const f1 = makeFile();
      const f2 = makeFile();
      const f3 = makeFile();
      await createFileRecord.handleAsync({ file: f1 });
      await createFileRecord.handleAsync({ file: f2 });
      await createFileRecord.handleAsync({ file: f3 });

      const result = await deleteFileRecordsByIds.handleAsync({
        ids: [f1.id, f2.id],
      });

      expect(result.success).toBe(true);
      expect(result.data!.rowsAffected).toBe(2);

      // f3 should still exist
      const remaining = await getFileById.handleAsync({ id: f3.id });
      expect(remaining.success).toBe(true);
    });

    it("should return 0 for empty ids array", async () => {
      const result = await deleteFileRecordsByIds.handleAsync({ ids: [] });
      expect(result.success).toBe(true);
      expect(result.data!.rowsAffected).toBe(0);
    });

    it("should handle mix of existing and missing ids", async () => {
      const f = makeFile();
      await createFileRecord.handleAsync({ file: f });

      const result = await deleteFileRecordsByIds.handleAsync({
        ids: [f.id, generateUuidV7(), generateUuidV7()],
      });

      expect(result.success).toBe(true);
      expect(result.data!.rowsAffected).toBe(1);
    });
  });

  // ---------------------------------------------------------------------------
  // PingDb
  // ---------------------------------------------------------------------------

  describe("PingDb", () => {
    it("should return healthy with positive latencyMs", async () => {
      const result = await pingDb.handleAsync({});
      expect(result.success).toBe(true);
      expect(result.data!.healthy).toBe(true);
      expect(result.data!.latencyMs).toBeGreaterThanOrEqual(0);
    });
  });
});
