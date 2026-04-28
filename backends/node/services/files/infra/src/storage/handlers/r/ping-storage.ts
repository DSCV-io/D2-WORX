import { ListBucketsCommand, type S3Client } from "@aws-sdk/client-s3";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { PingStorageInput as I, PingStorageOutput as O, IPingStorage } from "@d2/files-app";

export class PingStorage extends BaseHandler<I, O> implements IPingStorage {
  private readonly s3: S3Client;

  constructor(s3: S3Client, context: IHandlerContext) {
    super(context);
    this.s3 = s3;
  }

  protected async executeAsync(_input: I): Promise<D2Result<O | undefined>> {
    const start = performance.now();
    try {
      await this.s3.send(new ListBucketsCommand({}));
      const latencyMs = Math.round(performance.now() - start);
      return D2Result.ok({ data: { healthy: true, latencyMs } });
    } catch (err: unknown) {
      const latencyMs = Math.round(performance.now() - start);
      const error = err instanceof Error ? err.message : String(err);
      return D2Result.ok({ data: { healthy: false, latencyMs, error } });
    }
  }
}
