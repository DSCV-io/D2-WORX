import type { ServiceCollection } from "@d2/di";
import type Redis from "ioredis";
import type { IHandlerContext } from "@d2/handler";
import { DistributedCache } from "@d2/interfaces";
import { Get } from "./handlers/r/get.js";
import { Set } from "./handlers/u/set.js";
import { SetNx } from "./handlers/c/set-nx.js";
import { Remove } from "./handlers/d/remove.js";
import { Exists } from "./handlers/r/exists.js";
import { GetTtl } from "./handlers/r/get-ttl.js";
import { Increment } from "./handlers/u/increment.js";
import { AcquireLock } from "./handlers/c/acquire-lock.js";
import { ReleaseLock } from "./handlers/d/release-lock.js";
import { PingCache } from "./handlers/q/ping.js";

/**
 * Registers all Redis-backed distributed cache handlers with the DI container
 * under the generic service keys defined in `@d2/interfaces`.
 *
 * Handlers are registered as singletons (instances) since they are stateless
 * wrappers over the shared Redis connection.
 *
 * Usage in a composition root:
 * ```ts
 * addRedisCaching(services, redis, serviceContext);
 * // Then resolve anywhere: sp.resolve(DistributedCache.IDistributedCacheGetKey)
 * ```
 */
export function addRedisCaching(
  services: ServiceCollection,
  redis: Redis,
  context: IHandlerContext,
): void {
  services.addInstance(DistributedCache.IDistributedCacheGetKey, new Get<string>(redis, context));
  services.addInstance(DistributedCache.IDistributedCacheSetKey, new Set<string>(redis, context));
  services.addInstance(
    DistributedCache.IDistributedCacheSetNxKey,
    new SetNx<string>(redis, context),
  );
  services.addInstance(DistributedCache.IDistributedCacheRemoveKey, new Remove(redis, context));
  services.addInstance(DistributedCache.IDistributedCacheExistsKey, new Exists(redis, context));
  services.addInstance(DistributedCache.IDistributedCacheGetTtlKey, new GetTtl(redis, context));
  services.addInstance(
    DistributedCache.IDistributedCacheIncrementKey,
    new Increment(redis, context),
  );
  services.addInstance(
    DistributedCache.IDistributedCacheAcquireLockKey,
    new AcquireLock(redis, context),
  );
  services.addInstance(
    DistributedCache.IDistributedCacheReleaseLockKey,
    new ReleaseLock(redis, context),
  );
  services.addInstance(DistributedCache.IDistributedCachePingKey, new PingCache(redis, context));
}
