import type { IHandler } from "@d2/handler";

export interface PutStorageObjectInput {
  readonly key: string;
  readonly buffer: Buffer;
  readonly contentType: string;
}

export interface PutStorageObjectOutput {}

/** Uploads a buffer to object storage at the given key. */
export type IPutStorageObject = IHandler<PutStorageObjectInput, PutStorageObjectOutput>;
