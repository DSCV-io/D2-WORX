import { GetObjectCommand, type S3Client } from "@aws-sdk/client-s3";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  GetStorageObjectInput as I,
  GetStorageObjectOutput as O,
  IGetStorageObject,
} from "@d2/files-app";

export class GetStorageObject extends BaseHandler<I, O> implements IGetStorageObject {
  private readonly s3: S3Client;
  private readonly bucket: string;

  constructor(s3: S3Client, bucket: string, context: IHandlerContext) {
    super(context);
    this.s3 = s3;
    this.bucket = bucket;
  }

  override get redaction(): RedactionSpec {
    return { suppressInput: true, suppressOutput: true };
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    try {
      const response = await this.s3.send(
        new GetObjectCommand({
          Bucket: this.bucket,
          Key: input.key,
        }),
      );

      const body = response.Body;
      if (!body) {
        return D2Result.notFound();
      }

      const buffer = Buffer.from(await body.transformToByteArray());
      return D2Result.ok({ data: { buffer } });
    } catch (err: unknown) {
      if (isNotFoundError(err)) {
        return D2Result.notFound();
      }
      this.context.logger.error("GetStorageObject failed", { err });
      return D2Result.serviceUnavailable();
    }
  }
}

function isNotFoundError(err: unknown): boolean {
  return (
    err instanceof Error &&
    "name" in err &&
    (err.name === "NoSuchKey" || err.name === "NotFound" || err.name === "404")
  );
}
