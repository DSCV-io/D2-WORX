import { randomUUID } from "node:crypto";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { DistributedCache } from "@d2/interfaces";
import type { File, FileStatus } from "@d2/files-domain";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import type { IGetStaleFilesHandler } from "../../../../interfaces/repository/handlers/r/get-stale-files.js";
import type { IDeleteFileRecordsByIdsHandler } from "../../../../interfaces/repository/handlers/d/delete-file-records-by-ids.js";
import type { FileStorageHandlers } from "../../../../interfaces/storage/handlers/index.js";
import type { FilesJobOptions } from "../../../../files-job-options.js";
import { buildRawStorageKey, buildVariantStorageKey } from "@d2/files-domain";

type Input = Commands.RunCleanupInput;
type Output = Commands.RunCleanupOutput;

const LOCK_KEY = "lock:job:files-cleanup";

/**
 * Unified cleanup job for stale files across all non-terminal statuses.
 *
 * Acquires a distributed lock, then cleans up:
 * - pending files older than pendingThresholdMinutes
 * - processing files older than processingThresholdMinutes
 * - rejected files older than rejectedThresholdDays
 */
export class RunCleanup extends BaseHandler<Input, Output> implements Commands.IRunCleanupHandler {
  private readonly acquireLock: DistributedCache.IAcquireLockHandler;
  private readonly releaseLock: DistributedCache.IReleaseLockHandler;
  private readonly getStaleFiles: IGetStaleFilesHandler;
  private readonly deleteByIds: IDeleteFileRecordsByIdsHandler;
  private readonly storage: FileStorageHandlers;
  private readonly options: FilesJobOptions;

  constructor(
    acquireLock: DistributedCache.IAcquireLockHandler,
    releaseLock: DistributedCache.IReleaseLockHandler,
    getStaleFiles: IGetStaleFilesHandler,
    deleteByIds: IDeleteFileRecordsByIdsHandler,
    storage: FileStorageHandlers,
    options: FilesJobOptions,
    context: IHandlerContext,
  ) {
    super(context);
    this.acquireLock = acquireLock;
    this.releaseLock = releaseLock;
    this.getStaleFiles = getStaleFiles;
    this.deleteByIds = deleteByIds;
    this.storage = storage;
    this.options = options;
  }

  protected async executeAsync(_input: Input): Promise<D2Result<Output | undefined>> {
    const start = performance.now();
    const lockId = randomUUID();

    const lockResult = await this.acquireLock.handleAsync({
      key: LOCK_KEY,
      lockId,
      expirationMs: this.options.jobLockTtlMs,
    });

    if (!lockResult.success || !lockResult.data?.acquired) {
      return D2Result.ok({
        data: {
          lockAcquired: false,
          pendingCleaned: 0,
          processingCleaned: 0,
          rejectedCleaned: 0,
          durationMs: Math.round(performance.now() - start),
        },
      });
    }

    try {
      // Pending: threshold in minutes
      const pendingCutoff = new Date();
      pendingCutoff.setMinutes(pendingCutoff.getMinutes() - this.options.pendingThresholdMinutes);
      const pendingCleaned = await this.cleanStatus("pending", pendingCutoff);

      // Processing: threshold in minutes
      const processingCutoff = new Date();
      processingCutoff.setMinutes(
        processingCutoff.getMinutes() - this.options.processingThresholdMinutes,
      );
      const processingCleaned = await this.cleanStatus("processing", processingCutoff);

      // Rejected: threshold in days
      const rejectedCutoff = new Date();
      rejectedCutoff.setDate(rejectedCutoff.getDate() - this.options.rejectedThresholdDays);
      const rejectedCleaned = await this.cleanStatus("rejected", rejectedCutoff);

      return D2Result.ok({
        data: {
          lockAcquired: true,
          pendingCleaned,
          processingCleaned,
          rejectedCleaned,
          durationMs: Math.round(performance.now() - start),
        },
      });
    } finally {
      await this.releaseLock.handleAsync({ key: LOCK_KEY, lockId });
    }
  }

  private async cleanStatus(status: FileStatus, cutoffDate: Date): Promise<number> {
    const result = await this.getStaleFiles.handleAsync({
      status,
      cutoffDate,
      limit: this.options.cleanupBatchSize,
    });
    if (!result.success || !result.data?.files.length) return 0;

    const files = result.data.files;

    // Collect all storage keys
    const keysToDelete: string[] = [];
    for (const file of files) {
      keysToDelete.push(buildRawStorageKey(file));
      if (file.variants) {
        for (const variant of file.variants) {
          keysToDelete.push(buildVariantStorageKey(file, variant.size, variant.contentType));
        }
      }
    }

    // Delete from storage — log warning on failure but continue to DB cleanup
    if (keysToDelete.length > 0) {
      const storageResult = await this.storage.deleteMany.handleAsync({ keys: keysToDelete });
      if (!storageResult.success) {
        this.context.logger.warn("Storage cleanup partially failed — orphaned objects may remain", {
          status,
          fileCount: files.length,
          keyCount: keysToDelete.length,
        });
      }
    }

    // Delete DB records — CAS guard on `status` so any row that ProcessFile
    // (or another writer) has flipped past the candidate state in the gap
    // between GetStaleFiles and here is left intact. Worst case the row is
    // re-evaluated on the next cleanup pass.
    const ids = files.map((f: File) => f.id);
    const deleteResult = await this.deleteByIds.handleAsync({ ids, expectedStatus: status });
    if (!deleteResult.success) {
      this.context.logger.warn("DB cleanup failed", { status, fileCount: files.length });
      return 0;
    }

    return deleteResult.data?.rowsAffected ?? 0;
  }
}
