import { PutObjectCommand, type S3Client } from "@aws-sdk/client-s3";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  PutStorageObjectInput as I,
  PutStorageObjectOutput as O,
  IPutStorageObject,
} from "@d2/files-app";

export class PutStorageObject extends BaseHandler<I, O> implements IPutStorageObject {
  private readonly s3: S3Client;
  private readonly bucket: string;

  constructor(s3: S3Client, bucket: string, context: IHandlerContext) {
    super(context);
    this.s3 = s3;
    this.bucket = bucket;
  }

  override get redaction(): RedactionSpec {
    return { suppressInput: true };
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    try {
      await this.s3.send(
        new PutObjectCommand({
          Bucket: this.bucket,
          Key: input.key,
          Body: input.buffer,
          ContentType: input.contentType,
        }),
      );
      return D2Result.ok({ data: {} });
    } catch (err: unknown) {
      this.context.logger.error("PutStorageObject failed", { err });
      return D2Result.serviceUnavailable();
    }
  }
}
