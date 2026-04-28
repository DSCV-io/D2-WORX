import type { IHandler, RedactionSpec } from "@d2/handler";

export interface HeadStorageObjectInput {
  readonly key: string;
}

export interface HeadStorageObjectOutput {
  readonly exists: boolean;
  readonly contentType?: string;
  readonly sizeBytes?: number;
}

/** `key` may embed user/file identifiers — treat as PII; suppress full input. */
export const HEAD_STORAGE_OBJECT_REDACTION: RedactionSpec = {
  suppressInput: true,
};

/** Checks if an object exists in storage and returns its metadata. */
export interface IHeadStorageObject extends IHandler<
  HeadStorageObjectInput,
  HeadStorageObjectOutput
> {
  readonly redaction: RedactionSpec;
}
