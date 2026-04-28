import { DeleteObjectsCommand, type S3Client } from "@aws-sdk/client-s3";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { TK } from "@d2/i18n";
import { D2Result } from "@d2/result";
import {
  DELETE_STORAGE_OBJECTS_REDACTION,
  type DeleteStorageObjectsInput as I,
  type DeleteStorageObjectsOutput as O,
  type IDeleteStorageObjects,
} from "@d2/files-app";

export class DeleteStorageObjects extends BaseHandler<I, O> implements IDeleteStorageObjects {
  private readonly s3: S3Client;
  private readonly bucket: string;

  constructor(s3: S3Client, bucket: string, context: IHandlerContext) {
    super(context);
    this.s3 = s3;
    this.bucket = bucket;
  }

  override get redaction(): RedactionSpec {
    return DELETE_STORAGE_OBJECTS_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    if (input.keys.length === 0) {
      return D2Result.ok({ data: {} });
    }

    try {
      const response = await this.s3.send(
        new DeleteObjectsCommand({
          Bucket: this.bucket,
          Delete: {
            Objects: input.keys.map((key) => ({ Key: key })),
            Quiet: true,
          },
        }),
      );
      if (response.Errors && response.Errors.length > 0) {
        this.context.logger.warn("Partial S3 delete failure", {
          failedCount: response.Errors.length,
        });
        return D2Result.serviceUnavailable({
          messages: [TK.files.errors.PARTIAL_STORAGE_DELETE],
        });
      }
      return D2Result.ok({ data: {} });
    } catch (err: unknown) {
      this.context.logger.error("DeleteStorageObjects failed", {
        keyCount: input.keys.length,
        err,
      });
      return D2Result.serviceUnavailable();
    }
  }
}
