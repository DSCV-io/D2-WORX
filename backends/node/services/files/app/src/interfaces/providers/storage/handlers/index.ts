import type { IPutStorageObject } from "./put-storage-object.js";
import type { IGetStorageObject } from "./get-storage-object.js";
import type { IDeleteStorageObject } from "./delete-storage-object.js";
import type { IDeleteStorageObjects } from "./delete-storage-objects.js";
import type { IPresignPutUrl } from "./presign-put-url.js";
import type { IPresignGetUrl } from "./presign-get-url.js";
import type { IHeadStorageObject } from "./head-storage-object.js";
import type { IPingStorage } from "./ping-storage.js";

export type {
  PutStorageObjectInput,
  PutStorageObjectOutput,
  IPutStorageObject,
} from "./put-storage-object.js";

export type {
  GetStorageObjectInput,
  GetStorageObjectOutput,
  IGetStorageObject,
} from "./get-storage-object.js";

export type {
  DeleteStorageObjectInput,
  DeleteStorageObjectOutput,
  IDeleteStorageObject,
} from "./delete-storage-object.js";
export { DELETE_STORAGE_OBJECT_REDACTION } from "./delete-storage-object.js";

export type {
  DeleteStorageObjectsInput,
  DeleteStorageObjectsOutput,
  IDeleteStorageObjects,
} from "./delete-storage-objects.js";
export { DELETE_STORAGE_OBJECTS_REDACTION } from "./delete-storage-objects.js";

export type { PresignPutUrlInput, PresignPutUrlOutput, IPresignPutUrl } from "./presign-put-url.js";

export type { PresignGetUrlInput, PresignGetUrlOutput, IPresignGetUrl } from "./presign-get-url.js";

export type {
  HeadStorageObjectInput,
  HeadStorageObjectOutput,
  IHeadStorageObject,
} from "./head-storage-object.js";
export { HEAD_STORAGE_OBJECT_REDACTION } from "./head-storage-object.js";

export type { PingStorageInput, PingStorageOutput, IPingStorage } from "./ping-storage.js";

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
