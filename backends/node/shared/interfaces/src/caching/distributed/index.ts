export type { GetInput, GetOutput, IGetHandler } from "./handlers/r/get.js";
export type { ExistsInput, ExistsOutput, IExistsHandler } from "./handlers/r/exists.js";
export type { GetTtlInput, GetTtlOutput, IGetTtlHandler } from "./handlers/r/get-ttl.js";
export type { SetInput, SetOutput, ISetHandler } from "./handlers/u/set.js";
export type { IncrementInput, IncrementOutput, IIncrementHandler } from "./handlers/u/increment.js";
export type { SetNxInput, SetNxOutput, ISetNxHandler } from "./handlers/c/set-nx.js";
export type {
  AcquireLockInput,
  AcquireLockOutput,
  IAcquireLockHandler,
} from "./handlers/c/acquire-lock.js";
export type { RemoveInput, RemoveOutput, IRemoveHandler } from "./handlers/d/remove.js";
export type {
  ReleaseLockInput,
  ReleaseLockOutput,
  IReleaseLockHandler,
} from "./handlers/d/release-lock.js";
export type { PingInput, PingOutput, IPingHandler } from "./handlers/q/ping.js";
export {
  IDistributedCacheGetKey,
  IDistributedCacheSetKey,
  IDistributedCacheSetNxKey,
  IDistributedCacheRemoveKey,
  IDistributedCacheExistsKey,
  IDistributedCacheGetTtlKey,
  IDistributedCacheIncrementKey,
  IDistributedCacheAcquireLockKey,
  IDistributedCacheReleaseLockKey,
  IDistributedCachePingKey,
} from "./service-keys.js";
