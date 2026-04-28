import type { IPutStorageObject } from "./c/put-storage-object.js";
import type { IGetStorageObject } from "./r/get-storage-object.js";
import type { IDeleteStorageObject } from "./d/delete-storage-object.js";
import type { IDeleteStorageObjects } from "./d/delete-storage-objects.js";
import type { IPresignPutUrl } from "./c/presign-put-url.js";
import type { IPresignGetUrl } from "./r/presign-get-url.js";
import type { IHeadStorageObject } from "./r/head-storage-object.js";
import type { IPingStorage } from "./r/ping-storage.js";

export type {
  PutStorageObjectInput,
  PutStorageObjectOutput,
  IPutStorageObject,
} from "./c/put-storage-object.js";

export type {
  GetStorageObjectInput,
  GetStorageObjectOutput,
  IGetStorageObject,
} from "./r/get-storage-object.js";

export type {
  DeleteStorageObjectInput,
  DeleteStorageObjectOutput,
  IDeleteStorageObject,
} from "./d/delete-storage-object.js";
export { DELETE_STORAGE_OBJECT_REDACTION } from "./d/delete-storage-object.js";

export type {
  DeleteStorageObjectsInput,
  DeleteStorageObjectsOutput,
  IDeleteStorageObjects,
} from "./d/delete-storage-objects.js";
export { DELETE_STORAGE_OBJECTS_REDACTION } from "./d/delete-storage-objects.js";

export type {
  PresignPutUrlInput,
  PresignPutUrlOutput,
  IPresignPutUrl,
} from "./c/presign-put-url.js";

export type {
  PresignGetUrlInput,
  PresignGetUrlOutput,
  IPresignGetUrl,
} from "./r/presign-get-url.js";

export type {
  HeadStorageObjectInput,
  HeadStorageObjectOutput,
  IHeadStorageObject,
} from "./r/head-storage-object.js";
export { HEAD_STORAGE_OBJECT_REDACTION } from "./r/head-storage-object.js";

export type { PingStorageInput, PingStorageOutput, IPingStorage } from "./r/ping-storage.js";

/** Bundle of all storage handler types for convenient constructor injection. */
export interface FileStorageHandlers {
  readonly put: IPutStorageObject;
  readonly get: IGetStorageObject;
  readonly delete: IDeleteStorageObject;
  readonly deleteMany: IDeleteStorageObjects;
  readonly presignPut: IPresignPutUrl;
  readonly presignGet: IPresignGetUrl;
  readonly head: IHeadStorageObject;
  readonly ping: IPingStorage;
}
