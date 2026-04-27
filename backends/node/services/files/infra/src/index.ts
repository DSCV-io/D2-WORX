// @d2/files-infra — Infrastructure implementations for the Files service.
// Drizzle repositories, S3/MinIO storage, ClamAV scanning, Sharp image processing,
// gRPC callback clients, RabbitMQ consumers.

// --- DI Registration ---
export { addFilesInfra } from "./registration.js";
export type { FilesInfraConfig, FilesInfraDisposable } from "./registration.js";

// --- Infra-layer Options ---
export type { FilesStorageOptions, FilesScanningOptions, FilesOutboundOptions } from "./options.js";
export {
  DEFAULT_FILES_STORAGE_OPTIONS,
  DEFAULT_FILES_SCANNING_OPTIONS,
  DEFAULT_FILES_OUTBOUND_OPTIONS,
} from "./options.js";

// --- Drizzle Schema ---
export { file } from "./repository/schema/index.js";
export type { FileRow, NewFile } from "./repository/schema/index.js";

// --- Migrations ---
export { runMigrations } from "./repository/migrate.js";

// --- Repository Handlers ---
export { CreateFileRecord } from "./repository/handlers/c/create-file-record.js";
export { GetFileById } from "./repository/handlers/r/get-file-by-id.js";
export { GetFilesByContext } from "./repository/handlers/r/get-files-by-context.js";
export { GetStaleFiles } from "./repository/handlers/r/get-stale-files.js";
export { PingDb } from "./repository/handlers/r/ping-db.js";
export { UpdateFileRecord } from "./repository/handlers/u/update-file-record.js";
export { DeleteFileRecord } from "./repository/handlers/d/delete-file-record.js";
export { DeleteFileRecordsByIds } from "./repository/handlers/d/delete-file-records-by-ids.js";

// --- Mapper ---
export { toFile } from "./repository/mappers/file-mapper.js";

// --- Storage Handlers ---
export { PutStorageObject } from "./providers/storage/handlers/put-storage-object.js";
export { GetStorageObject } from "./providers/storage/handlers/get-storage-object.js";
export { DeleteStorageObject } from "./providers/storage/handlers/delete-storage-object.js";
export { DeleteStorageObjects } from "./providers/storage/handlers/delete-storage-objects.js";
export { PresignPutUrl } from "./providers/storage/handlers/presign-put-url.js";
export { PresignGetUrl } from "./providers/storage/handlers/presign-get-url.js";
export { HeadStorageObject } from "./providers/storage/handlers/head-storage-object.js";
export { PingStorage } from "./providers/storage/handlers/ping-storage.js";

// --- Provider Handlers ---
export { ScanFile } from "./providers/scanning/handlers/scan-file.js";
export type { ClamdConfig } from "./providers/scanning/handlers/scan-file.js";
export { ProcessVariants } from "./providers/image-processing/handlers/process-variants.js";

// --- Outbound Handlers ---
export { CallOnFileProcessed } from "./outbound/handlers/call-on-file-processed.js";
export { CallCanAccess } from "./outbound/handlers/call-can-access.js";

// --- Realtime Handlers ---
export { PushFileUpdate } from "./realtime/handlers/push-file-update.js";

// --- Messaging Handlers ---
export { PublishFileForProcessing } from "./messaging/handlers/pub/publish-file-for-processing.js";
export { IntakeFileUploaded } from "./messaging/handlers/sub/intake-file-uploaded.js";
export { ProcessUploadedFile } from "./messaging/handlers/sub/process-uploaded-file.js";

// --- Consumers ---
export { createFileUploadedConsumer } from "./messaging/consumers/file-uploaded-consumer.js";
export type { FileUploadedConsumerDeps } from "./messaging/consumers/file-uploaded-consumer.js";
export { createFileProcessingConsumer } from "./messaging/consumers/file-processing-consumer.js";
export type { FileProcessingConsumerDeps } from "./messaging/consumers/file-processing-consumer.js";
