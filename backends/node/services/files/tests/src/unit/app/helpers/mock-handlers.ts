import { vi } from "vitest";
import { D2Result } from "@d2/result";
import type { FileRepoHandlers, FileStorageHandlers } from "@d2/files-app";
import type {
  IScanFile,
  IProcessVariants,
  ICheckFileAccessHandler,
  INotifyFileProcessedHandler,
  IResolveFileAccessHandler,
  ICallOnFileProcessed,
  ICallCanAccess,
  IPushFileUpdate,
} from "@d2/files-app";
import type { DistributedCache } from "@d2/interfaces";
import type { IGetStaleFilesHandler, IDeleteFileRecordsByIdsHandler } from "@d2/files-app";
import type { IHandlerContext, IRequestContext } from "@d2/handler";

/**
 * Creates a mock FileRepoHandlers bundle with all handlers returning ok() by default.
 */
export function createMockRepo(): FileRepoHandlers {
  return {
    create: { handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { file: {} } })) },
    getById: { handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { file: null } })) },
    getByContext: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { files: [], total: 0 } })),
    },
    update: { handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { file: {} } })) },
    delete: { handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })) },
    deleteByIds: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { rowsAffected: 0 } })),
    },
  } as unknown as FileRepoHandlers;
}

/**
 * Creates a mock FileStorageHandlers bundle with all handlers returning ok() by default.
 */
export function createMockStorage(): FileStorageHandlers {
  return {
    put: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })),
    },
    get: {
      handleAsync: vi
        .fn()
        .mockResolvedValue(D2Result.ok({ data: { buffer: Buffer.from("fake") } })),
    },
    delete: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })),
    },
    deleteMany: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: {} })),
    },
    presignPut: {
      handleAsync: vi
        .fn()
        .mockResolvedValue(D2Result.ok({ data: { url: "https://minio.local/presigned" } })),
    },
    head: {
      handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { exists: false } })),
    },
    ping: {
      handleAsync: vi
        .fn()
        .mockResolvedValue(D2Result.ok({ data: { healthy: true, latencyMs: 1 } })),
    },
  } as unknown as FileStorageHandlers;
}

/**
 * Creates a mock IScanFile provider that returns clean by default.
 */
export function createMockScanFile(): IScanFile {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { clean: true } })),
  } as unknown as IScanFile;
}

/**
 * Creates a mock IProcessVariants provider.
 */
export function createMockProcessVariants(): IProcessVariants {
  return {
    handleAsync: vi.fn().mockResolvedValue(
      D2Result.ok({
        data: {
          variants: [
            {
              size: "thumb",
              buffer: Buffer.from("processed"),
              width: 64,
              height: 64,
              sizeBytes: 9,
              contentType: "image/jpeg",
            },
          ],
        },
      }),
    ),
  } as unknown as IProcessVariants;
}

/**
 * Creates a mock ICheckFileAccessHandler.
 */
export function createMockAccessChecker(allowed = true): ICheckFileAccessHandler {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { allowed } })),
  } as unknown as ICheckFileAccessHandler;
}

/**
 * Creates a mock IResolveFileAccessHandler.
 */
export function createMockResolveFileAccess(allowed = true): IResolveFileAccessHandler {
  return {
    handleAsync: allowed
      ? vi.fn().mockResolvedValue(D2Result.ok())
      : vi.fn().mockResolvedValue(D2Result.forbidden()),
  } as unknown as IResolveFileAccessHandler;
}

/**
 * Creates a mock INotifyFileProcessedHandler.
 */
export function createMockNotifier(success = true): INotifyFileProcessedHandler {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { success } })),
  } as unknown as INotifyFileProcessedHandler;
}

/**
 * Creates a mock AcquireLock handler.
 */
export function createMockAcquireLock(acquired = true): DistributedCache.IAcquireLockHandler {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { acquired } })),
  } as unknown as DistributedCache.IAcquireLockHandler;
}

/**
 * Creates a mock ReleaseLock handler.
 */
export function createMockReleaseLock(): DistributedCache.IReleaseLockHandler {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { released: true } })),
  } as unknown as DistributedCache.IReleaseLockHandler;
}

/**
 * Creates a mock GetStaleFiles handler.
 */
export function createMockGetStaleFiles(files: unknown[] = []): IGetStaleFilesHandler {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { files } })),
  } as unknown as IGetStaleFilesHandler;
}

/**
 * Creates a mock DeleteFileRecordsByIds handler.
 */
export function createMockDeleteByIds(): IDeleteFileRecordsByIdsHandler {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { rowsAffected: 0 } })),
  } as unknown as IDeleteFileRecordsByIdsHandler;
}

/**
 * Creates a mock ICallOnFileProcessed gRPC provider.
 */
export function createMockCallOnFileProcessed(success = true): ICallOnFileProcessed {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { success } })),
  } as unknown as ICallOnFileProcessed;
}

/**
 * Creates a mock ICallCanAccess gRPC provider.
 */
export function createMockCallCanAccess(allowed = true): ICallCanAccess {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { allowed } })),
  } as unknown as ICallCanAccess;
}

/**
 * Creates a mock IPushFileUpdate handler.
 */
export function createMockPushFileUpdate(): IPushFileUpdate {
  return {
    handleAsync: vi.fn().mockResolvedValue(D2Result.ok({ data: { delivered: true } })),
  } as unknown as IPushFileUpdate;
}

/**
 * Creates a mock PingDb handler.
 */
export function createMockPingDb(healthy = true) {
  return {
    handleAsync: vi
      .fn()
      .mockResolvedValue(
        D2Result.ok({ data: { healthy, latencyMs: 1, error: healthy ? undefined : "down" } }),
      ),
  };
}

/**
 * Creates a minimal mock IHandlerContext.
 */
export function createMockContext(requestOverrides?: Partial<IRequestContext>): IHandlerContext {
  const request: IRequestContext = {
    isAuthenticated: true,
    isOrgEmulating: null,
    isUserImpersonating: null,
    isTrustedService: null,
    userId: "user-123",
    targetOrgId: "org-456",
    ...requestOverrides,
  };
  return {
    request,
    logger: {
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
      debug: vi.fn(),
      trace: vi.fn(),
      fatal: vi.fn(),
      child: vi.fn().mockReturnThis(),
    },
  } as unknown as IHandlerContext;
}
