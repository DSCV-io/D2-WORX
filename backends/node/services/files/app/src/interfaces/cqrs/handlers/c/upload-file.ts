import type { IHandler, RedactionSpec } from "@d2/handler";

export interface UploadFileInput {
  readonly contextKey: string;
  readonly relatedEntityId: string;
  readonly contentType: string;
  readonly displayName: string;
  readonly sizeBytes: number;
}

export interface UploadFileOutput {
  readonly fileId: string;
  readonly presignedUrl: string;
}

export const UPLOAD_FILE_REDACTION: RedactionSpec = {
  inputFields: ["displayName"],
  outputFields: ["presignedUrl"],
};

export interface IUploadFileHandler extends IHandler<UploadFileInput, UploadFileOutput> {}
