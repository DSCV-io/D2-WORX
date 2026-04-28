import type Redis from "ioredis";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { redisErrorResult } from "../../redis-error-result.js";
import type { DistributedCache } from "@d2/interfaces";

type Input = DistributedCache.ExistsInput;
type Output = DistributedCache.ExistsOutput;

export class Exists extends BaseHandler<Input, Output> implements DistributedCache.IExistsHandler {
  private readonly redis: Redis;

  constructor(redis: Redis, context: IHandlerContext) {
    super(context);
    this.redis = redis;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    try {
      const count = await this.redis.exists(input.key);
      return D2Result.ok({ data: { exists: count > 0 } });
    } catch (err: unknown) {
      return redisErrorResult(err);
    }
  }
}
