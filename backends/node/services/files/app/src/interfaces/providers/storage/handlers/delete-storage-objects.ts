import type { IHandler, RedactionSpec } from "@d2/handler";

export interface DeleteStorageObjectsInput {
  readonly keys: string[];
}

export interface DeleteStorageObjectsOutput {}

/** `keys` may embed user/file identifiers — treat as PII; suppress full input. */
export const DELETE_STORAGE_OBJECTS_REDACTION: RedactionSpec = {
  suppressInput: true,
};

/** Deletes multiple objects by key from object storage. Silently ignores missing keys. */
export interface IDeleteStorageObjects
  extends IHandler<DeleteStorageObjectsInput, DeleteStorageObjectsOutput> {
  readonly redaction: RedactionSpec;
}
