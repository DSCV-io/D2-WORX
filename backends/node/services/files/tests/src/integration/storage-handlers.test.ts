import { describe, it, expect, beforeAll, afterAll } from "vitest";
import { generateUuidV7 } from "@d2/utilities";
import {
  PutStorageObject,
  GetStorageObject,
  DeleteStorageObject,
  DeleteStorageObjects,
  PresignPutUrl,
  HeadStorageObject,
  PingStorage,
  DEFAULT_FILES_STORAGE_OPTIONS,
} from "@d2/files-infra";
import { startMinIO, stopMinIO, getS3Client, getBucketName } from "./helpers/minio-test-helpers.js";
import { createTestContext } from "./helpers/test-context.js";

describe("Storage handlers (integration)", () => {
  let putObject: PutStorageObject;
  let getObject: GetStorageObject;
  let deleteObject: DeleteStorageObject;
  let deleteObjects: DeleteStorageObjects;
  let presignPutUrl: PresignPutUrl;
  let headObject: HeadStorageObject;
  let pingStorage: PingStorage;

  beforeAll(async () => {
    await startMinIO();
    const s3 = getS3Client();
    const bucket = getBucketName();
    const ctx = createTestContext();
    putObject = new PutStorageObject(s3, bucket, ctx);
    getObject = new GetStorageObject(s3, bucket, ctx);
    deleteObject = new DeleteStorageObject(s3, bucket, ctx);
    deleteObjects = new DeleteStorageObjects(s3, bucket, ctx);
    presignPutUrl = new PresignPutUrl(s3, bucket, DEFAULT_FILES_STORAGE_OPTIONS, ctx);
    headObject = new HeadStorageObject(s3, bucket, ctx);
    pingStorage = new PingStorage(s3, ctx);
  }, 120_000);

  afterAll(async () => {
    await stopMinIO();
  });

  function randomKey(): string {
    return `test/${generateUuidV7()}.bin`;
  }

  // ---------------------------------------------------------------------------
  // PutStorageObject + GetStorageObject — round-trip
  // ---------------------------------------------------------------------------

  describe("Put + Get", () => {
    it("should round-trip content byte-for-byte", async () => {
      const key = randomKey();
      const content = Buffer.from("Hello, MinIO integration test!");

      const putResult = await putObject.handleAsync({
        key,
        buffer: content,
        contentType: "text/plain",
      });
      expect(putResult.success).toBe(true);

      const getResult = await getObject.handleAsync({ key });
      expect(getResult.success).toBe(true);
      expect(Buffer.compare(getResult.data!.buffer, content)).toBe(0);
    });

    it("should round-trip binary content", async () => {
      const key = randomKey();
      const content = Buffer.from([0x00, 0xff, 0x42, 0x89, 0x50, 0x4e, 0x47]);

      await putObject.handleAsync({
        key,
        buffer: content,
        contentType: "application/octet-stream",
      });

      const getResult = await getObject.handleAsync({ key });
      expect(getResult.success).toBe(true);
      expect(Buffer.compare(getResult.data!.buffer, content)).toBe(0);
    });

    it("should return notFound for missing key", async () => {
      const result = await getObject.handleAsync({ key: `missing/${generateUuidV7()}.bin` });
      expect(result.success).toBe(false);
    });

    it("should overwrite existing key with new content", async () => {
      const key = randomKey();
      await putObject.handleAsync({
        key,
        buffer: Buffer.from("version1"),
        contentType: "text/plain",
      });
      await putObject.handleAsync({
        key,
        buffer: Buffer.from("version2"),
        contentType: "text/plain",
      });

      const result = await getObject.handleAsync({ key });
      expect(result.data!.buffer.toString()).toBe("version2");
    });
  });

  // ---------------------------------------------------------------------------
  // HeadStorageObject
  // ---------------------------------------------------------------------------

  describe("HeadStorageObject", () => {
    it("should return exists: true with metadata for existing object", async () => {
      const key = randomKey();
      const content = Buffer.from("head test content");
      await putObject.handleAsync({
        key,
        buffer: content,
        contentType: "image/png",
      });

      const result = await headObject.handleAsync({ key });
      expect(result.success).toBe(true);
      expect(result.data!.exists).toBe(true);
      expect(result.data!.contentType).toBe("image/png");
      expect(result.data!.sizeBytes).toBe(content.length);
    });

    it("should return exists: false for missing object", async () => {
      const result = await headObject.handleAsync({
        key: `missing/${generateUuidV7()}.bin`,
      });
      expect(result.success).toBe(true);
      expect(result.data!.exists).toBe(false);
    });
  });

  // ---------------------------------------------------------------------------
  // DeleteStorageObject
  // ---------------------------------------------------------------------------

  describe("DeleteStorageObject", () => {
    it("should delete an existing object", async () => {
      const key = randomKey();
      await putObject.handleAsync({
        key,
        buffer: Buffer.from("to be deleted"),
        contentType: "text/plain",
      });

      const deleteResult = await deleteObject.handleAsync({ key });
      expect(deleteResult.success).toBe(true);

      // Confirm deletion via head
      const headResult = await headObject.handleAsync({ key });
      expect(headResult.data!.exists).toBe(false);
    });

    it("should succeed (idempotent) on missing key", async () => {
      const result = await deleteObject.handleAsync({
        key: `missing/${generateUuidV7()}.bin`,
      });
      expect(result.success).toBe(true);
    });
  });

  // ---------------------------------------------------------------------------
  // DeleteStorageObjects (batch)
  // ---------------------------------------------------------------------------

  describe("DeleteStorageObjects", () => {
    it("should batch delete a subset and leave remainder", async () => {
      const keys = [randomKey(), randomKey(), randomKey()];
      for (const key of keys) {
        await putObject.handleAsync({
          key,
          buffer: Buffer.from("batch item"),
          contentType: "text/plain",
        });
      }

      // Delete first two, keep third
      const result = await deleteObjects.handleAsync({
        keys: [keys[0], keys[1]],
      });
      expect(result.success).toBe(true);

      // Verify first two deleted
      const head0 = await headObject.handleAsync({ key: keys[0] });
      expect(head0.data!.exists).toBe(false);
      const head1 = await headObject.handleAsync({ key: keys[1] });
      expect(head1.data!.exists).toBe(false);

      // Third still exists
      const head2 = await headObject.handleAsync({ key: keys[2] });
      expect(head2.data!.exists).toBe(true);
    });

    it("should succeed with empty keys array", async () => {
      const result = await deleteObjects.handleAsync({ keys: [] });
      expect(result.success).toBe(true);
    });

    it("should succeed with mix of existing and missing keys", async () => {
      const key = randomKey();
      await putObject.handleAsync({
        key,
        buffer: Buffer.from("exists"),
        contentType: "text/plain",
      });

      const result = await deleteObjects.handleAsync({
        keys: [key, `missing/${generateUuidV7()}.bin`],
      });
      expect(result.success).toBe(true);
    });
  });

  // ---------------------------------------------------------------------------
  // PresignPutUrl
  // ---------------------------------------------------------------------------

  describe("PresignPutUrl", () => {
    it("should generate a valid presigned URL containing bucket and key", async () => {
      const key = randomKey();
      const result = await presignPutUrl.handleAsync({
        key,
        contentType: "image/jpeg",
        maxSizeBytes: 10_000_000,
      });

      expect(result.success).toBe(true);
      const url = result.data!.url;
      expect(url).toContain(getBucketName());
      expect(url).toContain(encodeURIComponent(key).replace(/%2F/g, "/"));
      expect(url).toContain("X-Amz-Signature");
    });
  });

  // ---------------------------------------------------------------------------
  // PingStorage
  // ---------------------------------------------------------------------------

  describe("PingStorage", () => {
    it("should return healthy with positive latencyMs", async () => {
      const result = await pingStorage.handleAsync({});
      expect(result.success).toBe(true);
      expect(result.data!.healthy).toBe(true);
      expect(result.data!.latencyMs).toBeGreaterThanOrEqual(0);
    });
  });
});
