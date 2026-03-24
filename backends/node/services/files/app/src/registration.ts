import type { ServiceCollection, ServiceKey } from "@d2/di";
import { IHandlerContextKey } from "@d2/handler";
import { createRedisAcquireLockKey, createRedisReleaseLockKey } from "@d2/cache-redis";
import type { DistributedCache } from "@d2/interfaces";
import type { FilesJobOptions } from "./files-job-options.js";
import { DEFAULT_FILES_JOB_OPTIONS } from "./files-job-options.js";
import type { ContextKeyConfigMap } from "./context-key-config.js";

// Infra keys
import {
  ICreateFileRecordKey,
  IFindFileByIdKey,
  IFindFilesByContextKey,
  IFindStaleFilesKey,
  IUpdateFileRecordKey,
  IDeleteFileRecordKey,
  IDeleteFileRecordsByIdsKey,
  IPutStorageObjectKey,
  IGetStorageObjectKey,
  IDeleteStorageObjectKey,
  IDeleteStorageObjectsKey,
  IPresignPutUrlKey,
  IPresignGetUrlKey,
  IHeadStorageObjectKey,
  IPingStorageKey,
  IScanFileKey,
  IProcessVariantsKey,
  ICallOnFileProcessedKey,
  ICallCanAccessKey,
  INotifyFileProcessedKey,
  IPushFileUpdateKey,
  ICheckFileAccessKey,
  IResolveFileAccessKey,
  IPingDbKey,
  // App keys
  IUploadFileKey,
  IIntakeFileKey,
  IProcessFileKey,
  IDeleteFileKey,
  IRunCleanupKey,
  IGetFileMetadataKey,
  IListFilesKey,
  ICheckHealthKey,
  IContextKeyConfigMapKey,
} from "./service-keys.js";

// Handler implementations
import { UploadFile } from "./implementations/cqrs/handlers/c/upload-file.js";
import { IntakeFile } from "./implementations/cqrs/handlers/c/intake-file.js";
import { ProcessFile } from "./implementations/cqrs/handlers/c/process-file.js";
import { DeleteFile } from "./implementations/cqrs/handlers/c/delete-file.js";
import { RunCleanup } from "./implementations/cqrs/handlers/c/run-cleanup.js";
import { GetFileMetadata } from "./implementations/cqrs/handlers/q/get-file-metadata.js";
import { ListFiles } from "./implementations/cqrs/handlers/q/list-files.js";
import { CheckHealth } from "./implementations/cqrs/handlers/q/check-health.js";
import { NotifyFileProcessed } from "./implementations/cqrs/handlers/c/notify-file-processed.js";
import { CheckFileAccess } from "./implementations/cqrs/handlers/q/check-file-access.js";
import { ResolveFileAccess } from "./implementations/cqrs/handlers/u/resolve-file-access.js";

/** DI key for the files-scoped AcquireLock handler (registered in composition root). */
export const IFilesAcquireLockKey: ServiceKey<DistributedCache.IAcquireLockHandler> =
  createRedisAcquireLockKey("files");
/** DI key for the files-scoped ReleaseLock handler (registered in composition root). */
export const IFilesReleaseLockKey: ServiceKey<DistributedCache.IReleaseLockHandler> =
  createRedisReleaseLockKey("files");

/**
 * Registers files application-layer services (CQRS handlers)
 * with the DI container. Mirrors .NET's `services.AddFilesApp()` pattern.
 *
 * All CQRS handlers are transient — new instance per resolve.
 */
export function addFilesApp(
  services: ServiceCollection,
  contextKeyConfigs: ContextKeyConfigMap,
  jobOptions: FilesJobOptions = DEFAULT_FILES_JOB_OPTIONS,
): void {
  // Register config as singleton
  services.addSingleton(IContextKeyConfigMapKey, () => contextKeyConfigs);

  // --- Utility Handlers ---

  services.addTransient(
    IResolveFileAccessKey,
    (sp) =>
      new ResolveFileAccess(sp.resolve(IHandlerContextKey), sp.tryResolve(ICheckFileAccessKey)),
  );

  // --- Command Handlers ---

  services.addTransient(
    IUploadFileKey,
    (sp) =>
      new UploadFile(
        {
          create: sp.resolve(ICreateFileRecordKey),
          findById: sp.resolve(IFindFileByIdKey),
          findByContext: sp.resolve(IFindFilesByContextKey),
          update: sp.resolve(IUpdateFileRecordKey),
          delete: sp.resolve(IDeleteFileRecordKey),
          deleteByIds: sp.resolve(IDeleteFileRecordsByIdsKey),
        },
        {
          put: sp.resolve(IPutStorageObjectKey),
          get: sp.resolve(IGetStorageObjectKey),
          delete: sp.resolve(IDeleteStorageObjectKey),
          deleteMany: sp.resolve(IDeleteStorageObjectsKey),
          presignPut: sp.resolve(IPresignPutUrlKey),
          presignGet: sp.resolve(IPresignGetUrlKey),
          head: sp.resolve(IHeadStorageObjectKey),
          ping: sp.resolve(IPingStorageKey),
        },
        contextKeyConfigs,
        sp.resolve(IHandlerContextKey),
        sp.resolve(IResolveFileAccessKey),
      ),
  );

  services.addTransient(
    IIntakeFileKey,
    (sp) =>
      new IntakeFile(
        {
          create: sp.resolve(ICreateFileRecordKey),
          findById: sp.resolve(IFindFileByIdKey),
          findByContext: sp.resolve(IFindFilesByContextKey),
          update: sp.resolve(IUpdateFileRecordKey),
          delete: sp.resolve(IDeleteFileRecordKey),
          deleteByIds: sp.resolve(IDeleteFileRecordsByIdsKey),
        },
        {
          head: sp.resolve(IHeadStorageObjectKey),
          delete: sp.resolve(IDeleteStorageObjectKey),
        },
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    IProcessFileKey,
    (sp) =>
      new ProcessFile(
        {
          create: sp.resolve(ICreateFileRecordKey),
          findById: sp.resolve(IFindFileByIdKey),
          findByContext: sp.resolve(IFindFilesByContextKey),
          update: sp.resolve(IUpdateFileRecordKey),
          delete: sp.resolve(IDeleteFileRecordKey),
          deleteByIds: sp.resolve(IDeleteFileRecordsByIdsKey),
        },
        {
          put: sp.resolve(IPutStorageObjectKey),
          get: sp.resolve(IGetStorageObjectKey),
          delete: sp.resolve(IDeleteStorageObjectKey),
          deleteMany: sp.resolve(IDeleteStorageObjectsKey),
          presignPut: sp.resolve(IPresignPutUrlKey),
          presignGet: sp.resolve(IPresignGetUrlKey),
          head: sp.resolve(IHeadStorageObjectKey),
          ping: sp.resolve(IPingStorageKey),
        },
        sp.resolve(IScanFileKey),
        sp.resolve(IProcessVariantsKey),
        sp.resolve(INotifyFileProcessedKey),
        sp.resolve(IPushFileUpdateKey),
        contextKeyConfigs,
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    IDeleteFileKey,
    (sp) =>
      new DeleteFile(
        {
          create: sp.resolve(ICreateFileRecordKey),
          findById: sp.resolve(IFindFileByIdKey),
          findByContext: sp.resolve(IFindFilesByContextKey),
          update: sp.resolve(IUpdateFileRecordKey),
          delete: sp.resolve(IDeleteFileRecordKey),
          deleteByIds: sp.resolve(IDeleteFileRecordsByIdsKey),
        },
        {
          put: sp.resolve(IPutStorageObjectKey),
          get: sp.resolve(IGetStorageObjectKey),
          delete: sp.resolve(IDeleteStorageObjectKey),
          deleteMany: sp.resolve(IDeleteStorageObjectsKey),
          presignPut: sp.resolve(IPresignPutUrlKey),
          presignGet: sp.resolve(IPresignGetUrlKey),
          head: sp.resolve(IHeadStorageObjectKey),
          ping: sp.resolve(IPingStorageKey),
        },
        contextKeyConfigs,
        sp.resolve(IHandlerContextKey),
        sp.resolve(IResolveFileAccessKey),
      ),
  );

  services.addTransient(
    IRunCleanupKey,
    (sp) =>
      new RunCleanup(
        sp.resolve(IFilesAcquireLockKey),
        sp.resolve(IFilesReleaseLockKey),
        sp.resolve(IFindStaleFilesKey),
        sp.resolve(IDeleteFileRecordsByIdsKey),
        {
          put: sp.resolve(IPutStorageObjectKey),
          get: sp.resolve(IGetStorageObjectKey),
          delete: sp.resolve(IDeleteStorageObjectKey),
          deleteMany: sp.resolve(IDeleteStorageObjectsKey),
          presignPut: sp.resolve(IPresignPutUrlKey),
          presignGet: sp.resolve(IPresignGetUrlKey),
          head: sp.resolve(IHeadStorageObjectKey),
          ping: sp.resolve(IPingStorageKey),
        },
        jobOptions,
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    INotifyFileProcessedKey,
    (sp) =>
      new NotifyFileProcessed(sp.resolve(ICallOnFileProcessedKey), sp.resolve(IHandlerContextKey)),
  );

  // --- Query Handlers ---

  services.addTransient(
    IGetFileMetadataKey,
    (sp) =>
      new GetFileMetadata(
        {
          create: sp.resolve(ICreateFileRecordKey),
          findById: sp.resolve(IFindFileByIdKey),
          findByContext: sp.resolve(IFindFilesByContextKey),
          update: sp.resolve(IUpdateFileRecordKey),
          delete: sp.resolve(IDeleteFileRecordKey),
          deleteByIds: sp.resolve(IDeleteFileRecordsByIdsKey),
        },
        contextKeyConfigs,
        sp.resolve(IHandlerContextKey),
        sp.resolve(IResolveFileAccessKey),
      ),
  );

  services.addTransient(
    IListFilesKey,
    (sp) =>
      new ListFiles(
        {
          create: sp.resolve(ICreateFileRecordKey),
          findById: sp.resolve(IFindFileByIdKey),
          findByContext: sp.resolve(IFindFilesByContextKey),
          update: sp.resolve(IUpdateFileRecordKey),
          delete: sp.resolve(IDeleteFileRecordKey),
          deleteByIds: sp.resolve(IDeleteFileRecordsByIdsKey),
        },
        contextKeyConfigs,
        sp.resolve(IHandlerContextKey),
        sp.resolve(IResolveFileAccessKey),
      ),
  );

  services.addTransient(
    ICheckHealthKey,
    (sp) =>
      new CheckHealth(
        sp.resolve(IPingDbKey),
        {
          put: sp.resolve(IPutStorageObjectKey),
          get: sp.resolve(IGetStorageObjectKey),
          delete: sp.resolve(IDeleteStorageObjectKey),
          deleteMany: sp.resolve(IDeleteStorageObjectsKey),
          presignPut: sp.resolve(IPresignPutUrlKey),
          presignGet: sp.resolve(IPresignGetUrlKey),
          head: sp.resolve(IHeadStorageObjectKey),
          ping: sp.resolve(IPingStorageKey),
        },
        sp.resolve(IHandlerContextKey),
      ),
  );

  services.addTransient(
    ICheckFileAccessKey,
    (sp) => new CheckFileAccess(sp.resolve(ICallCanAccessKey), sp.resolve(IHandlerContextKey)),
  );
}
