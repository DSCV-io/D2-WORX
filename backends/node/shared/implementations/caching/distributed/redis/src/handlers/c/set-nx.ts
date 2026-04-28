import type Redis from "ioredis";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { TK } from "@d2/i18n";
import { D2Result, ErrorCodes, HttpStatusCode } from "@d2/result";
import { redisErrorResult } from "../../redis-error-result.js";
import type { DistributedCache } from "@d2/interfaces";
import { JsonCacheSerializer, type ICacheSerializer } from "../../serialization.js";

export class SetNx<TValue>
  extends BaseHandler<DistributedCache.SetNxInput<TValue>, DistributedCache.SetNxOutput>
  implements DistributedCache.ISetNxHandler<TValue>
{
  private readonly redis: Redis;
  private readonly serializer: ICacheSerializer<TValue>;

  constructor(redis: Redis, context: IHandlerContext, serializer?: ICacheSerializer<TValue>) {
    super(context);
    this.redis = redis;
    this.serializer = serializer ?? new JsonCacheSerializer<TValue>();
  }

  override get redaction(): RedactionSpec {
    return { inputFields: ["value"] };
  }

  protected async executeAsync(
    input: DistributedCache.SetNxInput<TValue>,
  ): Promise<D2Result<DistributedCache.SetNxOutput | undefined>> {
    try {
      let serialized: string | Buffer;
      try {
        serialized = this.serializer.serialize(input.value);
      } catch (err: unknown) {
        this.context.logger.warn(
          `Cache serialization failed: ${err instanceof Error ? err.message : String(err)}`,
        );
        return D2Result.fail({
          messages: [TK.common.errors.COULD_NOT_BE_SERIALIZED],
          statusCode: HttpStatusCode.InternalServerError,
          errorCode: ErrorCodes.COULD_NOT_BE_SERIALIZED,
        });
      }

      let wasSet: "OK" | null;
      if (input.expirationMs !== undefined) {
        wasSet = await this.redis.set(input.key, serialized, "PX", input.expirationMs, "NX");
      } else {
        wasSet = await this.redis.set(input.key, serialized, "NX");
      }

      return D2Result.ok({ data: { wasSet: wasSet === "OK" } });
    } catch (err: unknown) {
      return redisErrorResult(err);
    }
  }
}
