import { createServiceKey } from "@d2/di";

// Import interface types for infra-level keys
import type {
  ICreateFileRecordHandler,
  IGetFileByIdHandler,
  IGetFilesByContextHandler,
  IGetStaleFilesHandler,
  IUpdateFileRecordHandler,
  IDeleteFileRecordHandler,
  IDeleteFileRecordsByIdsHandler,
  IPingDbHandler,
} from "./interfaces/repository/handlers/index.js";
import type {
  IPutStorageObject,
  IGetStorageObject,
  IDeleteStorageObject,
  IDeleteStorageObjects,
  IPresignPutUrl,
  IPresignGetUrl,
  IHeadStorageObject,
  IPingStorage,
} from "./interfaces/storage/handlers/index.js";
import type { IScanFile } from "./interfaces/scanning/handlers/index.js";
import type { IProcessVariants } from "./interfaces/image-processing/handlers/index.js";
import type { ICallOnFileProcessed, ICallCanAccess } from "./interfaces/outbound/handlers/index.js";
import type { IPushFileUpdate } from "./interfaces/realtime/handlers/index.js";

// Import app-layer handler interfaces
import type { Commands, Queries, Utilities } from "./interfaces/cqrs/handlers/index.js";

// Import messaging handler interfaces
import type { Publishers, Subscribers } from "./interfaces/messaging/handlers/index.js";

// Import config type
import type { ContextKeyConfigMap } from "./context-key-config.js";

// =============================================================================
// Infrastructure-layer keys (interfaces defined here, implemented in files-infra)
// =============================================================================

// --- File Repository ---
export const ICreateFileRecordKey = createServiceKey<ICreateFileRecordHandler>(
  "Files.Repo.CreateFileRecord",
);
export const IGetFileByIdKey = createServiceKey<IGetFileByIdHandler>("Files.Repo.GetFileById");
export const IGetFilesByContextKey = createServiceKey<IGetFilesByContextHandler>(
  "Files.Repo.GetFilesByContext",
);
export const IGetStaleFilesKey = createServiceKey<IGetStaleFilesHandler>(
  "Files.Repo.GetStaleFiles",
);
export const IUpdateFileRecordKey = createServiceKey<IUpdateFileRecordHandler>(
  "Files.Repo.UpdateFileRecord",
);
export const IDeleteFileRecordKey = createServiceKey<IDeleteFileRecordHandler>(
  "Files.Repo.DeleteFileRecord",
);
export const IDeleteFileRecordsByIdsKey = createServiceKey<IDeleteFileRecordsByIdsHandler>(
  "Files.Repo.DeleteFileRecordsByIds",
);

// --- Health Check Repository ---
export const IPingDbKey = createServiceKey<IPingDbHandler>("Files.Repo.PingDb");

// --- Object Storage ---
export const IPutStorageObjectKey = createServiceKey<IPutStorageObject>(
  "Files.Infra.PutStorageObject",
);
export const IGetStorageObjectKey = createServiceKey<IGetStorageObject>(
  "Files.Infra.GetStorageObject",
);
export const IDeleteStorageObjectKey = createServiceKey<IDeleteStorageObject>(
  "Files.Infra.DeleteStorageObject",
);
export const IDeleteStorageObjectsKey = createServiceKey<IDeleteStorageObjects>(
  "Files.Infra.DeleteStorageObjects",
);
export const IPresignPutUrlKey = createServiceKey<IPresignPutUrl>("Files.Infra.PresignPutUrl");
export const IPresignGetUrlKey = createServiceKey<IPresignGetUrl>("Files.Infra.PresignGetUrl");
export const IHeadStorageObjectKey = createServiceKey<IHeadStorageObject>(
  "Files.Infra.HeadStorageObject",
);
export const IPingStorageKey = createServiceKey<IPingStorage>("Files.Infra.PingStorage");

// =============================================================================
// Scanning keys (interfaces defined here, implemented in files-infra)
// =============================================================================

export const IScanFileKey = createServiceKey<IScanFile>("Files.Provider.ScanFile");

// =============================================================================
// Image-processing keys (interfaces defined here, implemented in files-infra)
// =============================================================================

export const IProcessVariantsKey = createServiceKey<IProcessVariants>(
  "Files.Provider.ProcessVariants",
);

// =============================================================================
// Outbound keys (interfaces defined here, implemented in files-infra)
// =============================================================================

export const ICallOnFileProcessedKey = createServiceKey<ICallOnFileProcessed>(
  "Files.Outbound.CallOnFileProcessed",
);
export const ICallCanAccessKey = createServiceKey<ICallCanAccess>("Files.Outbound.CallCanAccess");

// =============================================================================
// Realtime keys (interfaces defined here, implemented in files-infra)
// =============================================================================

export const IPushFileUpdateKey = createServiceKey<IPushFileUpdate>(
  "Files.Realtime.PushFileUpdate",
);

// =============================================================================
// Application-layer keys (defined and implemented in files-app)
// =============================================================================

// --- Command Handlers ---
export const IUploadFileKey = createServiceKey<Commands.IUploadFileHandler>("Files.App.UploadFile");
export const IIntakeFileKey = createServiceKey<Commands.IIntakeFileHandler>("Files.App.IntakeFile");
export const IProcessFileKey =
  createServiceKey<Commands.IProcessFileHandler>("Files.App.ProcessFile");
export const IDeleteFileKey = createServiceKey<Commands.IDeleteFileHandler>("Files.App.DeleteFile");
export const IRunCleanupKey = createServiceKey<Commands.IRunCleanupHandler>("Files.App.RunCleanup");
export const INotifyFileProcessedKey = createServiceKey<Commands.INotifyFileProcessedHandler>(
  "Files.App.NotifyFileProcessed",
);

// --- Utility Handlers ---
export const IResolveFileAccessKey = createServiceKey<Utilities.IResolveFileAccessHandler>(
  "Files.App.ResolveFileAccess",
);

// --- Query Handlers ---
export const IGetFileMetadataKey = createServiceKey<Queries.IGetFileMetadataHandler>(
  "Files.App.GetFileMetadata",
);
export const IListFilesKey = createServiceKey<Queries.IListFilesHandler>("Files.App.ListFiles");
export const ICheckHealthKey =
  createServiceKey<Queries.ICheckHealthHandler>("Files.App.CheckHealth");
export const ICheckFileAccessKey = createServiceKey<Queries.ICheckFileAccessHandler>(
  "Files.App.CheckFileAccess",
);
export const IGetFileVariantUrlKey = createServiceKey<Queries.IGetFileVariantUrlHandler>(
  "Files.App.GetFileVariantUrl",
);
export const IDownloadFileVariantKey = createServiceKey<Queries.IDownloadFileVariantHandler>(
  "Files.App.DownloadFileVariant",
);

// --- Messaging Handlers ---
export const IPublishFileForProcessingKey =
  createServiceKey<Publishers.IPublishFileForProcessingHandler>(
    "Files.Messaging.PublishFileForProcessing",
  );
export const IIntakeFileUploadedKey = createServiceKey<Subscribers.IIntakeFileUploadedHandler>(
  "Files.Messaging.IntakeFileUploaded",
);
export const IProcessUploadedFileKey = createServiceKey<Subscribers.IProcessUploadedFileHandler>(
  "Files.Messaging.ProcessUploadedFile",
);

// =============================================================================
// Config key (singleton, registered in addFilesApp)
// =============================================================================

export const IContextKeyConfigMapKey = createServiceKey<ContextKeyConfigMap>(
  "Files.Config.ContextKeyConfigMap",
);
