import type Redis from "ioredis";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { TK } from "@d2/i18n";
import { D2Result, ErrorCodes, HttpStatusCode } from "@d2/result";
import { redisErrorResult } from "../../redis-error-result.js";
import type { DistributedCache } from "@d2/interfaces";
import { JsonCacheSerializer, type ICacheSerializer } from "../../serialization.js";

export class Get<TValue>
  extends BaseHandler<DistributedCache.GetInput, DistributedCache.GetOutput<TValue>>
  implements DistributedCache.IGetHandler<TValue>
{
  private readonly redis: Redis;
  private readonly serializer: ICacheSerializer<TValue>;

  constructor(redis: Redis, context: IHandlerContext, serializer?: ICacheSerializer<TValue>) {
    super(context);
    this.redis = redis;
    this.serializer = serializer ?? new JsonCacheSerializer<TValue>();
  }

  override get redaction(): RedactionSpec {
    return { outputFields: ["value"] };
  }

  protected async executeAsync(
    input: DistributedCache.GetInput,
  ): Promise<D2Result<DistributedCache.GetOutput<TValue> | undefined>> {
    try {
      const raw = await this.redis.getBuffer(input.key);
      if (raw === null) {
        return D2Result.notFound();
      }
      try {
        const value = this.serializer.deserialize(raw);
        return D2Result.ok({ data: { value } });
      } catch (err: unknown) {
        this.context.logger.warn(
          `Cache deserialization failed for key "${input.key}": ${err instanceof Error ? err.message : String(err)}`,
        );
        return D2Result.fail({
          messages: [TK.common.errors.COULD_NOT_BE_DESERIALIZED],
          statusCode: HttpStatusCode.InternalServerError,
          errorCode: ErrorCodes.COULD_NOT_BE_DESERIALIZED,
        });
      }
    } catch (err: unknown) {
      return redisErrorResult(err);
    }
  }
}
