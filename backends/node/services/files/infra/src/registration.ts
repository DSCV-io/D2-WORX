import type { S3Client } from "@aws-sdk/client-s3";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import type { ServiceCollection } from "@d2/di";
import type { IMessagePublisher } from "@d2/messaging";
import type { FileCallbackClient } from "@d2/protos";
import { IHandlerContextKey } from "@d2/handler";
import {
  // Repository keys
  ICreateFileRecordKey,
  IFindFileByIdKey,
  IFindFilesByContextKey,
  IFindStaleFilesKey,
  IUpdateFileRecordKey,
  IDeleteFileRecordKey,
  IDeleteFileRecordsByIdsKey,
  IPingDbKey,
  // Storage keys
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
  ICallOnFileProcessedKey,
  ICallCanAccessKey,
  // Realtime keys
  IPushFileUpdateKey,
  // Messaging keys
  IPublishFileForProcessingKey,
  IIntakeFileUploadedKey,
  IProcessUploadedFileKey,
  // App-layer keys (for messaging handler dependencies)
  IIntakeFileKey,
  IProcessFileKey,
} from "@d2/files-app";

// Repository handlers
import { CreateFileRecord } from "./repository/handlers/c/create-file-record.js";
import { FindFileById } from "./repository/handlers/r/find-file-by-id.js";
import { FindFilesByContext } from "./repository/handlers/r/find-files-by-context.js";
import { FindStaleFiles } from "./repository/handlers/r/find-stale-files.js";
import { PingDb } from "./repository/handlers/r/ping-db.js";
import { UpdateFileRecord } from "./repository/handlers/u/update-file-record.js";
import { DeleteFileRecord } from "./repository/handlers/d/delete-file-record.js";
import { DeleteFileRecordsByIds } from "./repository/handlers/d/delete-file-records-by-ids.js";

// Storage handlers
import { PutStorageObject } from "./providers/storage/handlers/put-storage-object.js";
import { GetStorageObject } from "./providers/storage/handlers/get-storage-object.js";
import { DeleteStorageObject } from "./providers/storage/handlers/delete-storage-object.js";
import { DeleteStorageObjects } from "./providers/storage/handlers/delete-storage-objects.js";
import { PresignPutUrl } from "./providers/storage/handlers/presign-put-url.js";
import { PresignGetUrl } from "./providers/storage/handlers/presign-get-url.js";
import { HeadStorageObject } from "./providers/storage/handlers/head-storage-object.js";
import { PingStorage } from "./providers/storage/handlers/ping-storage.js";

// Provider handlers
import type { ClamdConfig } from "./providers/scanning/handlers/scan-file.js";
import { ScanFile } from "./providers/scanning/handlers/scan-file.js";
import { ProcessVariants } from "./providers/image-processing/handlers/process-variants.js";
// Outbound handlers
import { CallOnFileProcessed } from "./outbound/handlers/call-on-file-processed.js";
import { CallCanAccess } from "./outbound/handlers/call-can-access.js";
// Realtime handlers
import { PushFileUpdate } from "./realtime/handlers/push-file-update.js";

// Messaging handlers
import { PublishFileForProcessing } from "./messaging/handlers/pub/publish-file-for-processing.js";
import { IntakeFileUploaded } from "./messaging/handlers/sub/intake-file-uploaded.js";
import { ProcessUploadedFile } from "./messaging/handlers/sub/process-uploaded-file.js";

export interface FilesInfraConfig {
  readonly db: NodePgDatabase;
  readonly s3: S3Client;
  readonly bucketName: string;
  readonly clamd: ClamdConfig;
  readonly publisher: IMessagePublisher;
  /** gRPC address of the SignalR Gateway (e.g., "d2-signalr:5200"). */
  readonly signalrGatewayAddress: string;
  /** API key sent on outbound gRPC callbacks to owning services (Auth, Comms). */
  readonly callbackApiKey: string;
  /** API key sent on outbound gRPC calls to the SignalR Gateway. */
  readonly signalrApiKey: string;
  /**
   * Optional S3 client configured with a browser-reachable endpoint.
   * Used by PresignPutUrl and PresignGetUrl to generate URLs that browsers can reach directly
   * (e.g., via a cloudflared tunnel to MinIO). Falls back to `s3` if not provided.
   */
  readonly s3Public?: S3Client;
}

export interface FilesInfraDisposable {
  /** Closes all cached gRPC callback clients. Call during graceful shutdown. */
  dispose(): void;
}

/**
 * Registers files infrastructure services (repository handlers, storage handlers,
 * provider handlers, messaging handlers) with the DI container.
 *
 * All handlers are transient — new instance per resolve.
 *
 * Returns a disposable that must be called during shutdown to close gRPC clients.
 */
export function addFilesInfra(
  services: ServiceCollection,
  config: FilesInfraConfig,
): FilesInfraDisposable {
  const {
    db,
    s3,
    bucketName,
    clamd,
    publisher,
    signalrGatewayAddress,
    callbackApiKey,
    signalrApiKey,
    s3Public,
  } = config;

  // Shared gRPC client cache for outbound handlers
  const callbackClients = new Map<string, FileCallbackClient>();

  // --- Repository handlers ---

  services.addTransient(IPingDbKey, (sp) => new PingDb(db, sp.resolve(IHandlerContextKey)));
  services.addTransient(
    ICreateFileRecordKey,
    (sp) => new CreateFileRecord(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IFindFileByIdKey,
    (sp) => new FindFileById(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IFindFilesByContextKey,
    (sp) => new FindFilesByContext(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IFindStaleFilesKey,
    (sp) => new FindStaleFiles(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IUpdateFileRecordKey,
    (sp) => new UpdateFileRecord(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IDeleteFileRecordKey,
    (sp) => new DeleteFileRecord(db, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IDeleteFileRecordsByIdsKey,
    (sp) => new DeleteFileRecordsByIds(db, sp.resolve(IHandlerContextKey)),
  );

  // --- Storage handlers ---

  services.addTransient(
    IPutStorageObjectKey,
    (sp) => new PutStorageObject(s3, bucketName, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IGetStorageObjectKey,
    (sp) => new GetStorageObject(s3, bucketName, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IDeleteStorageObjectKey,
    (sp) => new DeleteStorageObject(s3, bucketName, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IDeleteStorageObjectsKey,
    (sp) => new DeleteStorageObjects(s3, bucketName, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IPresignPutUrlKey,
    (sp) => new PresignPutUrl(s3Public ?? s3, bucketName, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IPresignGetUrlKey,
    (sp) => new PresignGetUrl(s3Public ?? s3, bucketName, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IHeadStorageObjectKey,
    (sp) => new HeadStorageObject(s3, bucketName, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IPingStorageKey,
    (sp) => new PingStorage(s3, sp.resolve(IHandlerContextKey)),
  );

  // --- Provider handlers ---

  services.addTransient(IScanFileKey, (sp) => new ScanFile(clamd, sp.resolve(IHandlerContextKey)));
  services.addTransient(
    IProcessVariantsKey,
    (sp) => new ProcessVariants(sp.resolve(IHandlerContextKey)),
  );

  // --- Outbound handlers ---

  services.addTransient(
    ICallOnFileProcessedKey,
    (sp) =>
      new CallOnFileProcessed(callbackClients, callbackApiKey, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    ICallCanAccessKey,
    (sp) => new CallCanAccess(callbackClients, callbackApiKey, sp.resolve(IHandlerContextKey)),
  );

  // --- Realtime handlers ---

  services.addTransient(
    IPushFileUpdateKey,
    (sp) =>
      new PushFileUpdate(signalrGatewayAddress, signalrApiKey, sp.resolve(IHandlerContextKey)),
  );

  // --- Messaging handlers ---

  services.addTransient(
    IPublishFileForProcessingKey,
    (sp) => new PublishFileForProcessing(publisher, sp.resolve(IHandlerContextKey)),
  );
  services.addTransient(
    IIntakeFileUploadedKey,
    (sp) =>
      new IntakeFileUploaded(
        sp.resolve(IIntakeFileKey),
        sp.resolve(IPublishFileForProcessingKey),
        sp.resolve(IHandlerContextKey),
      ),
  );
  services.addTransient(
    IProcessUploadedFileKey,
    (sp) => new ProcessUploadedFile(sp.resolve(IProcessFileKey), sp.resolve(IHandlerContextKey)),
  );

  return {
    dispose() {
      for (const client of callbackClients.values()) {
        client.close();
      }
      callbackClients.clear();
    },
  };
}
