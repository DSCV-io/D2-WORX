// --- Handler type imports (used by bundle interface below) ---
import type { ICreateFileRecordHandler } from "./c/create-file-record.js";
import type { IGetFileByIdHandler } from "./r/get-file-by-id.js";
import type { IGetFilesByContextHandler } from "./r/get-files-by-context.js";
import type { IUpdateFileRecordHandler } from "./u/update-file-record.js";
import type { IDeleteFileRecordHandler } from "./d/delete-file-record.js";
import type { IDeleteFileRecordsByIdsHandler } from "./d/delete-file-records-by-ids.js";

// --- Create (C) ---
export type {
  CreateFileRecordInput,
  CreateFileRecordOutput,
  ICreateFileRecordHandler,
} from "./c/create-file-record.js";

// --- Read (R) ---
export type {
  GetFileByIdInput,
  GetFileByIdOutput,
  IGetFileByIdHandler,
} from "./r/get-file-by-id.js";

export type {
  GetFilesByContextInput,
  GetFilesByContextOutput,
  IGetFilesByContextHandler,
} from "./r/get-files-by-context.js";

export type {
  GetStaleFilesInput,
  GetStaleFilesOutput,
  IGetStaleFilesHandler,
} from "./r/get-stale-files.js";

export type { PingDbInput, PingDbOutput, IPingDbHandler } from "./r/ping-db.js";

// --- Update (U) ---
export type {
  UpdateFileRecordInput,
  UpdateFileRecordOutput,
  IUpdateFileRecordHandler,
} from "./u/update-file-record.js";

// --- Delete (D) ---
export type {
  DeleteFileRecordInput,
  DeleteFileRecordOutput,
  IDeleteFileRecordHandler,
} from "./d/delete-file-record.js";

export type {
  DeleteFileRecordsByIdsInput,
  DeleteFileRecordsByIdsOutput,
  IDeleteFileRecordsByIdsHandler,
} from "./d/delete-file-records-by-ids.js";

// ---------------------------------------------------------------------------
// Bundle type — passed to app-layer handlers as a single object
// ---------------------------------------------------------------------------

export interface FileRepoHandlers {
  create: ICreateFileRecordHandler;
  getById: IGetFileByIdHandler;
  getByContext: IGetFilesByContextHandler;
  update: IUpdateFileRecordHandler;
  delete: IDeleteFileRecordHandler;
  deleteByIds: IDeleteFileRecordsByIdsHandler;
}
