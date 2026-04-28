// @d2/files-app — CQRS handlers for the Files service.
// Zero infra imports — this package is pure application logic.

// --- Runtime Configuration ---
export type {
  UploadResolution,
  ReadResolution,
  ContextKeyConfig,
  ContextKeyConfigMap,
} from "./context-key-config.js";
export { parseContextKeyConfigs } from "./context-key-config.js";

export type { FilesJobOptions } from "./files-job-options.js";
export { DEFAULT_FILES_JOB_OPTIONS } from "./files-job-options.js";

// --- Storage Key Utilities (re-exported from @d2/files-domain) ---
export {
  getExtensionForContentType,
  buildRawStorageKey,
  buildVariantStorageKey,
} from "@d2/files-domain";

export type { RawStorageKeyFile, VariantStorageKeyFile } from "@d2/files-domain";

// --- CQRS Handler Interfaces (app-layer contracts) ---
export {
  Commands as FilesCommands,
  Queries as FilesQueries,
  Utilities as FilesUtilities,
} from "./interfaces/cqrs/handlers/index.js";

// --- Messaging Handler Interfaces ---
export {
  Publishers as FilesPublishers,
  Subscribers as FilesSubscribers,
} from "./interfaces/messaging/handlers/index.js";

// --- Interfaces (Storage Handler Contracts) ---
export type {
  PutStorageObjectInput,
  PutStorageObjectOutput,
  IPutStorageObject,
  GetStorageObjectInput,
  GetStorageObjectOutput,
  IGetStorageObject,
  DeleteStorageObjectInput,
  DeleteStorageObjectOutput,
  IDeleteStorageObject,
  DeleteStorageObjectsInput,
  DeleteStorageObjectsOutput,
  IDeleteStorageObjects,
  PresignPutUrlInput,
  PresignPutUrlOutput,
  IPresignPutUrl,
  PresignGetUrlInput,
  PresignGetUrlOutput,
  IPresignGetUrl,
  HeadStorageObjectInput,
  HeadStorageObjectOutput,
  IHeadStorageObject,
  PingStorageInput,
  PingStorageOutput,
  IPingStorage,
  FileStorageHandlers,
} from "./interfaces/storage/handlers/index.js";
export {
  DELETE_STORAGE_OBJECT_REDACTION,
  DELETE_STORAGE_OBJECTS_REDACTION,
  HEAD_STORAGE_OBJECT_REDACTION,
} from "./interfaces/storage/handlers/index.js";

// --- Interfaces (Scanning Contracts) ---
export type {
  ScanFileInput,
  ScanFileOutput,
  IScanFile,
} from "./interfaces/scanning/handlers/index.js";

// --- Interfaces (Image-processing Contracts) ---
export type {
  ProcessedVariant,
  ProcessVariantsInput,
  ProcessVariantsOutput,
  IProcessVariants,
} from "./interfaces/image-processing/handlers/index.js";

// --- Interfaces (Outbound Contracts) ---
export type {
  CallOnFileProcessedInput,
  CallOnFileProcessedOutput,
  ICallOnFileProcessed,
  CallCanAccessInput,
  CallCanAccessOutput,
  ICallCanAccess,
} from "./interfaces/outbound/handlers/index.js";
export {
  CALL_ON_FILE_PROCESSED_REDACTION,
  CALL_CAN_ACCESS_REDACTION,
} from "./interfaces/outbound/handlers/index.js";

// --- Interfaces (Realtime Contracts) ---
export type {
  PushFileUpdateInput,
  PushFileUpdateOutput,
  IPushFileUpdate,
} from "./interfaces/realtime/handlers/index.js";
export { PUSH_FILE_UPDATE_REDACTION } from "./interfaces/realtime/handlers/index.js";

// --- CQRS Contracts (app-implemented, delegate to gRPC callbacks) ---
export type {
  NotifyFileProcessedInput,
  NotifyFileProcessedOutput,
  INotifyFileProcessedHandler,
} from "./interfaces/cqrs/handlers/c/notify-file-processed.js";

export type {
  CheckFileAccessInput,
  CheckFileAccessOutput,
  ICheckFileAccessHandler,
} from "./interfaces/cqrs/handlers/q/check-file-access.js";

// --- CQRS Utility Contracts ---
export type {
  ResolveFileAccessInput,
  ResolveFileAccessOutput,
  IResolveFileAccessHandler,
} from "./interfaces/cqrs/handlers/u/resolve-file-access.js";

// --- Interfaces (Messaging Contracts) ---
export type {
  PublishFileForProcessingInput,
  PublishFileForProcessingOutput,
  IPublishFileForProcessingHandler,
} from "./interfaces/messaging/handlers/pub/publish-file-for-processing.js";

export type {
  IntakeFileUploadedInput,
  IntakeFileUploadedOutput,
  IIntakeFileUploadedHandler,
} from "./interfaces/messaging/handlers/sub/intake-file-uploaded.js";

export type {
  ProcessUploadedFileInput,
  ProcessUploadedFileOutput,
  IProcessUploadedFileHandler,
} from "./interfaces/messaging/handlers/sub/process-uploaded-file.js";

// --- Interfaces (Repository Handler Bundles) ---
export type {
  // Bundle type
  FileRepoHandlers,
  // Individual handler types
  ICreateFileRecordHandler,
  IGetFileByIdHandler,
  IGetFilesByContextHandler,
  IGetStaleFilesHandler,
  IUpdateFileRecordHandler,
  IDeleteFileRecordHandler,
  IDeleteFileRecordsByIdsHandler,
  IPingDbHandler,
  // Individual I/O types
  CreateFileRecordInput,
  CreateFileRecordOutput,
  GetFileByIdInput,
  GetFileByIdOutput,
  GetFilesByContextInput,
  GetFilesByContextOutput,
  GetStaleFilesInput,
  GetStaleFilesOutput,
  UpdateFileRecordInput,
  UpdateFileRecordOutput,
  DeleteFileRecordInput,
  DeleteFileRecordOutput,
  DeleteFileRecordsByIdsInput,
  DeleteFileRecordsByIdsOutput,
  PingDbInput,
  PingDbOutput,
} from "./interfaces/repository/handlers/index.js";

// --- Command Handlers ---
export { UploadFile } from "./implementations/cqrs/handlers/c/upload-file.js";
export type {
  UploadFileInput,
  UploadFileOutput,
} from "./interfaces/cqrs/handlers/c/upload-file.js";

export { IntakeFile } from "./implementations/cqrs/handlers/c/intake-file.js";
export type {
  IntakeFileInput,
  IntakeFileOutput,
} from "./interfaces/cqrs/handlers/c/intake-file.js";

export { ProcessFile } from "./implementations/cqrs/handlers/c/process-file.js";
export type {
  ProcessFileInput,
  ProcessFileOutput,
} from "./interfaces/cqrs/handlers/c/process-file.js";

export { DeleteFile } from "./implementations/cqrs/handlers/c/delete-file.js";
export type {
  DeleteFileInput,
  DeleteFileOutput,
} from "./interfaces/cqrs/handlers/c/delete-file.js";

export { RunCleanup } from "./implementations/cqrs/handlers/c/run-cleanup.js";
export type {
  RunCleanupInput,
  RunCleanupOutput,
} from "./interfaces/cqrs/handlers/c/run-cleanup.js";

export { NotifyFileProcessed } from "./implementations/cqrs/handlers/c/notify-file-processed.js";

// --- Query Handlers ---
export { GetFileMetadata } from "./implementations/cqrs/handlers/q/get-file-metadata.js";
export type {
  GetFileMetadataInput,
  GetFileMetadataOutput,
} from "./interfaces/cqrs/handlers/q/get-file-metadata.js";

export { ListFiles } from "./implementations/cqrs/handlers/q/list-files.js";
export type { ListFilesInput, ListFilesOutput } from "./interfaces/cqrs/handlers/q/list-files.js";

export { CheckHealth } from "./implementations/cqrs/handlers/q/check-health.js";
export type {
  CheckHealthInput,
  CheckHealthOutput,
  ComponentHealth,
} from "./interfaces/cqrs/handlers/q/check-health.js";

export { CheckFileAccess } from "./implementations/cqrs/handlers/q/check-file-access.js";

export { GetFileVariantUrl } from "./implementations/cqrs/handlers/q/get-file-variant-url.js";
export type {
  GetFileVariantUrlInput,
  GetFileVariantUrlOutput,
} from "./interfaces/cqrs/handlers/q/get-file-variant-url.js";

export { DownloadFileVariant } from "./implementations/cqrs/handlers/q/download-file-variant.js";
export type {
  DownloadFileVariantInput,
  DownloadFileVariantOutput,
} from "./interfaces/cqrs/handlers/q/download-file-variant.js";

// --- Utility Handlers ---
export { ResolveFileAccess } from "./implementations/cqrs/handlers/u/resolve-file-access.js";

// --- DI Registration ---
export { addFilesApp } from "./registration.js";
export {
  // Infra keys (repo)
  ICreateFileRecordKey,
  IGetFileByIdKey,
  IGetFilesByContextKey,
  IGetStaleFilesKey,
  IUpdateFileRecordKey,
  IDeleteFileRecordKey,
  IDeleteFileRecordsByIdsKey,
  IPingDbKey,
  // Infra keys (storage)
  IPutStorageObjectKey,
  IGetStorageObjectKey,
  IDeleteStorageObjectKey,
  IDeleteStorageObjectsKey,
  IPresignPutUrlKey,
  IPresignGetUrlKey,
  IHeadStorageObjectKey,
  IPingStorageKey,
  // Provider keys
  IScanFileKey,
  IProcessVariantsKey,
  // Outbound keys (infra-implemented)
  ICallOnFileProcessedKey,
  ICallCanAccessKey,
  // Realtime keys (infra-implemented)
  IPushFileUpdateKey,
  // CQRS keys (app-implemented)
  INotifyFileProcessedKey,
  ICheckFileAccessKey,
  IGetFileVariantUrlKey,
  IDownloadFileVariantKey,
  // Utility handler keys
  IResolveFileAccessKey,
  // App keys
  IUploadFileKey,
  IIntakeFileKey,
  IProcessFileKey,
  IDeleteFileKey,
  IRunCleanupKey,
  IGetFileMetadataKey,
  IListFilesKey,
  ICheckHealthKey,
  // Messaging keys
  IPublishFileForProcessingKey,
  IIntakeFileUploadedKey,
  IProcessUploadedFileKey,
  // Config key
  IContextKeyConfigMapKey,
} from "./service-keys.js";
