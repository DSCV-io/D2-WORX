import type { IHandler, RedactionSpec } from "@d2/handler";
import type { File } from "@d2/files-domain";

export interface GetFileMetadataInput {
  readonly fileId: string;
}

export interface GetFileMetadataOutput {
  readonly file: File;
}

export const GET_FILE_METADATA_REDACTION: RedactionSpec = {
  suppressOutput: true,
};

export interface IGetFileMetadataHandler extends IHandler<
  GetFileMetadataInput,
  GetFileMetadataOutput
> {}
