import { createServiceKey } from "@d2/di";
import type { IGetHandler } from "./handlers/r/get.js";
import type { ISetHandler } from "./handlers/u/set.js";
import type { ISetNxHandler } from "./handlers/c/set-nx.js";
import type { IRemoveHandler } from "./handlers/d/remove.js";
import type { IExistsHandler } from "./handlers/r/exists.js";
import type { IGetTtlHandler } from "./handlers/r/get-ttl.js";
import type { IIncrementHandler } from "./handlers/u/increment.js";
import type { IAcquireLockHandler } from "./handlers/c/acquire-lock.js";
import type { IReleaseLockHandler } from "./handlers/d/release-lock.js";
import type { IPingHandler } from "./handlers/q/ping.js";

export const IDistributedCacheGetKey =
  createServiceKey<IGetHandler<string>>("DistributedCache.Get");
export const IDistributedCacheSetKey =
  createServiceKey<ISetHandler<string>>("DistributedCache.Set");
export const IDistributedCacheSetNxKey =
  createServiceKey<ISetNxHandler<string>>("DistributedCache.SetNx");
export const IDistributedCacheRemoveKey =
  createServiceKey<IRemoveHandler>("DistributedCache.Remove");
export const IDistributedCacheExistsKey =
  createServiceKey<IExistsHandler>("DistributedCache.Exists");
export const IDistributedCacheGetTtlKey =
  createServiceKey<IGetTtlHandler>("DistributedCache.GetTtl");
export const IDistributedCacheIncrementKey = createServiceKey<IIncrementHandler>(
  "DistributedCache.Increment",
);
export const IDistributedCacheAcquireLockKey = createServiceKey<IAcquireLockHandler>(
  "DistributedCache.AcquireLock",
);
export const IDistributedCacheReleaseLockKey = createServiceKey<IReleaseLockHandler>(
  "DistributedCache.ReleaseLock",
);
export const IDistributedCachePingKey = createServiceKey<IPingHandler>("DistributedCache.Ping");
