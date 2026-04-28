import { HeadObjectCommand, type S3Client } from "@aws-sdk/client-s3";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import {
  HEAD_STORAGE_OBJECT_REDACTION,
  type HeadStorageObjectInput as I,
  type HeadStorageObjectOutput as O,
  type IHeadStorageObject,
} from "@d2/files-app";

export class HeadStorageObject extends BaseHandler<I, O> implements IHeadStorageObject {
  private readonly s3: S3Client;
  private readonly bucket: string;

  constructor(s3: S3Client, bucket: string, context: IHandlerContext) {
    super(context);
    this.s3 = s3;
    this.bucket = bucket;
  }

  override get redaction(): RedactionSpec {
    return HEAD_STORAGE_OBJECT_REDACTION;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    try {
      const response = await this.s3.send(
        new HeadObjectCommand({
          Bucket: this.bucket,
          Key: input.key,
        }),
      );

      return D2Result.ok({
        data: {
          exists: true,
          contentType: response.ContentType,
          sizeBytes: response.ContentLength,
        },
      });
    } catch (err: unknown) {
      if (isNotFoundError(err)) {
        return D2Result.ok({ data: { exists: false } });
      }
      this.context.logger.error("HeadStorageObject failed", { err });
      return D2Result.serviceUnavailable();
    }
  }
}

function isNotFoundError(err: unknown): boolean {
  return (
    err instanceof Error &&
    "name" in err &&
    (err.name === "NotFound" || err.name === "NoSuchKey" || err.name === "404")
  );
}
