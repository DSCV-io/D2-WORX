import type { IHandler, RedactionSpec } from "@d2/handler";

export interface DeleteStorageObjectInput {
  readonly key: string;
}

export interface DeleteStorageObjectOutput {}

/** `key` may embed user/file identifiers — treat as PII; suppress full input. */
export const DELETE_STORAGE_OBJECT_REDACTION: RedactionSpec = {
  suppressInput: true,
};

/** Deletes a single object by key from object storage. */
export interface IDeleteStorageObject
  extends IHandler<DeleteStorageObjectInput, DeleteStorageObjectOutput> {
  readonly redaction: RedactionSpec;
}
