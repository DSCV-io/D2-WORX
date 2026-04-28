import { DeleteObjectCommand, type S3Client } from "@aws-sdk/client-s3";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import {
  DELETE_STORAGE_OBJECT_REDACTION,
  type DeleteStorageObjectInput as I,
  type DeleteStorageObjectOutput as O,
  type IDeleteStorageObject,
} from "@d2/files-app";

export class DeleteStorageObject extends BaseHandler<I, O> implements IDeleteStorageObject {
  private readonly s3: S3Client;
  private readonly bucket: string;

  constructor(s3: S3Client, bucket: string, context: IHandlerContext) {
    super(context);
    this.s3 = s3;
    this.bucket = bucket;
  }

  override get redaction(): RedactionSpec {
    return DELETE_STORAGE_OBJECT_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    try {
      await this.s3.send(
        new DeleteObjectCommand({
          Bucket: this.bucket,
          Key: input.key,
        }),
      );
      return D2Result.ok({ data: {} });
    } catch (err: unknown) {
      this.context.logger.error("DeleteStorageObject failed", { err });
      return D2Result.serviceUnavailable();
    }
  }
}
