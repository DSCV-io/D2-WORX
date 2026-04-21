import { PutObjectCommand, type S3Client } from "@aws-sdk/client-s3";
import { getSignedUrl } from "@aws-sdk/s3-request-presigner";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  PresignPutUrlInput as I,
  PresignPutUrlOutput as O,
  IPresignPutUrl,
} from "@d2/files-app";

const DEFAULT_EXPIRY_SECONDS = 900; // 15 minutes

export class PresignPutUrl extends BaseHandler<I, O> implements IPresignPutUrl {
  override get redaction(): RedactionSpec {
    return { outputFields: ["url"] };
  }

  private readonly s3: S3Client;
  private readonly bucket: string;

  /**
   * @param s3 — S3 client used for presigned URL generation. When a public endpoint
   *   is configured (e.g., cloudflared tunnel), this should be a separate client
   *   pointing at the public URL so browsers can reach MinIO directly.
   */
  constructor(s3: S3Client, bucket: string, context: IHandlerContext) {
    super(context);
    this.s3 = s3;
    this.bucket = bucket;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    try {
      // Do NOT set ContentLength — the presigned URL is for the client to PUT any size
      // up to maxSizeBytes. Setting ContentLength would require the upload to be exactly
      // that size. The app layer validates sizeBytes <= maxSizeBytes before reaching here.
      const command = new PutObjectCommand({
        Bucket: this.bucket,
        Key: input.key,
        ContentType: input.contentType,
      });

      const url = await getSignedUrl(this.s3, command, {
        expiresIn: DEFAULT_EXPIRY_SECONDS,
      });

      return D2Result.ok({ data: { url } });
    } catch (err: unknown) {
      this.context.logger.error("PresignPutUrl failed", { err });
      return D2Result.serviceUnavailable();
    }
  }
}
