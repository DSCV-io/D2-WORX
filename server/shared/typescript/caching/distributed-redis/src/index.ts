// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

export {
  RedisDistributedCache,
  type RedisDistributedCacheDeps,
} from "./redis-distributed-cache.js";
export { RedisCacheInvalidationBackplane } from "./redis-cache-invalidation-backplane.js";
export { JsonCacheSerializer } from "./json-cache-serializer.js";
export {
  REDIS_CACHE_INSTRUMENTS,
  REDIS_CACHE_METER_NAME,
  REDIS_CACHE_METER_VERSION,
  type CacheInstrumentMeta,
} from "./redis-cache-telemetry.js";
export {
  REDIS_CACHE_DEFAULTS,
  createRedisCacheOptions,
  type RedisCacheOptions,
} from "./redis-cache-options.js";
export { connectRedis } from "./connect-redis.js";
// Public twin-pin surface for dual-runtime ContractFixtures parity.
// Not an executor surface — constants only, byte-equivalent to .NET RedisLuaScripts.
export {
  INCREMENT_WITH_OPTIONAL_TTL,
  RELEASE_LOCK_IF_OWNER,
  SET_ADD_WITH_OPTIONAL_TTL,
} from "./redis-lua-scripts.js";
