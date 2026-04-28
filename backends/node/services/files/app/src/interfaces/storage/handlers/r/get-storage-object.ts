import type { IHandler } from "@d2/handler";

export interface GetStorageObjectInput {
  readonly key: string;
}

export interface GetStorageObjectOutput {
  readonly buffer: Buffer;
}

/** Downloads the object at the given key from object storage. */
export type IGetStorageObject = IHandler<GetStorageObjectInput, GetStorageObjectOutput>;
