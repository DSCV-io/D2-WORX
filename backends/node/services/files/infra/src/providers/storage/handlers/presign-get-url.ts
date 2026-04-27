import { GetObjectCommand, type S3Client } from "@aws-sdk/client-s3";
import { getSignedUrl } from "@aws-sdk/s3-request-presigner";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  PresignGetUrlInput as I,
  PresignGetUrlOutput as O,
  IPresignGetUrl,
} from "@d2/files-app";
import type { FilesStorageOptions } from "../../../options.js";

export class PresignGetUrl extends BaseHandler<I, O> implements IPresignGetUrl {
  override get redaction(): RedactionSpec {
    return { outputFields: ["url"] };
  }

  private readonly s3: S3Client;
  private readonly bucket: string;
  private readonly options: FilesStorageOptions;

  /**
   * @param s3 — S3 client used for presigned URL generation. When a public endpoint
   *   is configured (e.g., cloudflared tunnel), this should be a separate client
   *   pointing at the public URL so browsers can reach MinIO directly.
   */
  constructor(
    s3: S3Client,
    bucket: string,
    options: FilesStorageOptions,
    context: IHandlerContext,
  ) {
    super(context);
    this.s3 = s3;
    this.bucket = bucket;
    this.options = options;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    try {
      const command = new GetObjectCommand({
        Bucket: this.bucket,
        Key: input.key,
      });

      const url = await getSignedUrl(this.s3, command, {
        expiresIn: this.options.presignGetExpirySeconds,
      });

      return D2Result.ok({ data: { url } });
    } catch (err: unknown) {
      this.context.logger.error("PresignGetUrl failed", { err });
      return D2Result.serviceUnavailable();
    }
  }
}
