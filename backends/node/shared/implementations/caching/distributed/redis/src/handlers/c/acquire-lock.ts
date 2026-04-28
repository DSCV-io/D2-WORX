import type Redis from "ioredis";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { redisErrorResult } from "../../redis-error-result.js";
import type { DistributedCache } from "@d2/interfaces";

type Input = DistributedCache.AcquireLockInput;
type Output = DistributedCache.AcquireLockOutput;

export class AcquireLock
  extends BaseHandler<Input, Output>
  implements DistributedCache.IAcquireLockHandler
{
  private readonly redis: Redis;

  constructor(redis: Redis, context: IHandlerContext) {
    super(context);
    this.redis = redis;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    if (!Number.isFinite(input.expirationMs) || input.expirationMs <= 0) {
      return D2Result.validationFailed({
        inputErrors: [["expirationMs", "Must be a finite positive number."]],
      });
    }

    try {
      const wasSet = await this.redis.set(input.key, input.lockId, "PX", input.expirationMs, "NX");
      return D2Result.ok({ data: { acquired: wasSet === "OK" } });
    } catch (err: unknown) {
      return redisErrorResult(err);
    }
  }
}
