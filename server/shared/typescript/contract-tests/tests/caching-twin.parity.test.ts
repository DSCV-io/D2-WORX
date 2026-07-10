// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Dual-runtime caching twin parity (KOM-01..08).
 *
 * .NET emitter: Integration/ContractFixtures/CachingTwinFixtureEmitter.cs
 * Fixture catalog: fixtures/caching-twin/constants.json
 *
 * Lua normalization (both sides): CRLF → LF + trim, matching the .NET
 * `NormalizeLua` helper and the TS template-literal `.trim()` constants.
 * Full tiered registration / LoggerMessage template strings intentionally
 * diverge — only the shared prefix + EventId semantics are compared.
 */

import { describe, expect, it } from "vitest";

import { LOCAL_CACHE_DEFAULTS } from "@d2/caching-abstractions";
import {
  INCREMENT_WITH_OPTIONAL_TTL,
  REDIS_CACHE_DEFAULTS,
  REDIS_CACHE_METER_NAME,
  RELEASE_LOCK_IF_OWNER,
  SET_ADD_WITH_OPTIONAL_TTL,
} from "@d2/caching-distributed-redis";
import { LOCAL_CACHE_METER_NAME } from "@d2/caching-local-default";
import {
  BACKPLANE_NOT_REGISTERED_MESSAGE,
  TIERED_ERROR_CODE_UNKNOWN,
  TieredCacheOp,
} from "@d2/caching-tiered";

import { canonicalize, loadFixture } from "../src/index.js";

interface Instrument {
  readonly name: string;
  readonly unit: string;
  readonly description: string;
}

interface Meter {
  readonly name: string;
  readonly version: string;
  readonly instruments: readonly Instrument[];
}

interface CachingTwinConstants {
  readonly localDefaults: {
    readonly maxEntries: number;
    readonly defaultExpirationMs: number;
    readonly keyPrefix: string;
  };
  readonly localMeter: Meter;
  readonly redisDefaults: {
    readonly defaultExpirationMs: number;
    readonly keyPrefix: string;
    readonly invalidationChannel: string;
    readonly commandTimeoutMs: number;
    readonly connectTimeoutMs: number;
    readonly connectRetries: number;
    readonly abortOnConnectFail: boolean;
  };
  readonly redisMeter: Meter;
  readonly luaScripts: {
    readonly INCREMENT_WITH_OPTIONAL_TTL: string;
    readonly RELEASE_LOCK_IF_OWNER: string;
    readonly SET_ADD_WITH_OPTIONAL_TTL: string;
  };
  readonly tieredSemantics: {
    readonly backplaneNotRegisteredMessagePrefix: string;
    readonly eventIds: readonly number[];
    readonly logLevels: readonly string[];
    readonly dotnetBindingFields: {
      readonly event1: readonly string[];
      readonly event2: readonly string[];
    };
    readonly errorCodeBindingPresent: boolean;
  };
}

/**
 * Instrument tuples for createLocalCacheCounters (not exported as data).
 * Must stay byte-equal to local-cache-telemetry.ts + .NET LocalCacheTelemetry.
 */
const TS_LOCAL_INSTRUMENTS: readonly Instrument[] = [
  {
    name: "d2.cache.local.hits",
    unit: "{hit}",
    description: "Local cache hits.",
  },
  {
    name: "d2.cache.local.misses",
    unit: "{miss}",
    description: "Local cache misses.",
  },
  {
    name: "d2.cache.local.sets",
    unit: "{write}",
    description: "Local cache writes.",
  },
  {
    name: "d2.cache.local.removes",
    unit: "{removal}",
    description: "Local cache removals (explicit).",
  },
  {
    name: "d2.cache.local.evictions",
    unit: "{eviction}",
    description: "Entries evicted by capacity / expiration.",
  },
];

/**
 * Instrument tuples for createRedisCacheCounters (not exported as data).
 * Must stay byte-equal to redis-cache-telemetry.ts + .NET RedisCacheTelemetry.
 */
const TS_REDIS_INSTRUMENTS: readonly Instrument[] = [
  {
    name: "d2.cache.redis.hits",
    unit: "{hit}",
    description: "Redis cache hits.",
  },
  {
    name: "d2.cache.redis.misses",
    unit: "{miss}",
    description: "Redis cache misses.",
  },
  {
    name: "d2.cache.redis.sets",
    unit: "{write}",
    description: "Redis cache writes.",
  },
  {
    name: "d2.cache.redis.removes",
    unit: "{removal}",
    description: "Redis cache removals.",
  },
  {
    name: "d2.cache.redis.broadcasts",
    unit: "{broadcast}",
    description: "Invalidation messages published to backplane.",
  },
  {
    name: "d2.cache.redis.errors",
    unit: "{error}",
    description: "Redis-side failures.",
  },
];

const TS_METER_VERSION = "1.0.0";

/** Closed-set TieredCacheOp values (TS surface; .NET uses LoggerMessage names). */
const TS_TIERED_OP_SET = [
  TieredCacheOp.SET,
  TieredCacheOp.SET_MANY,
  TieredCacheOp.REMOVE,
  TieredCacheOp.REMOVE_MANY,
].sort();

describe("caching-twin parity (.NET fixtures ↔ TS constants) KOM-01..08", () => {
  const fixture = loadFixture<CachingTwinConstants>(
    "caching-twin",
    "constants",
  );
  const data = fixture.data;

  describe("localDefaults (KOM-01)", () => {
    it("maxEntries matches", () => {
      expect(LOCAL_CACHE_DEFAULTS.maxEntries).toBe(
        data.localDefaults.maxEntries,
      );
    });

    it("defaultExpirationMs matches", () => {
      expect(LOCAL_CACHE_DEFAULTS.defaultExpirationMs).toBe(
        data.localDefaults.defaultExpirationMs,
      );
    });

    it("keyPrefix matches", () => {
      expect(LOCAL_CACHE_DEFAULTS.keyPrefix).toBe(data.localDefaults.keyPrefix);
    });

    it("canonical map is byte-equal", () => {
      expect(canonicalize({ ...LOCAL_CACHE_DEFAULTS })).toEqual(
        canonicalize(data.localDefaults),
      );
    });
  });

  describe("localMeter (KOM-02/03)", () => {
    it("meter name matches", () => {
      expect(LOCAL_CACHE_METER_NAME).toBe(data.localMeter.name);
    });

    it("meter version is 1.0.0", () => {
      expect(TS_METER_VERSION).toBe(data.localMeter.version);
    });

    it("instruments (name, unit, description) match", () => {
      expect(canonicalize(TS_LOCAL_INSTRUMENTS)).toEqual(
        canonicalize(data.localMeter.instruments),
      );
    });
  });

  describe("redisDefaults (KOM-05/07)", () => {
    it("defaultExpirationMs matches", () => {
      expect(REDIS_CACHE_DEFAULTS.defaultExpirationMs).toBe(
        data.redisDefaults.defaultExpirationMs,
      );
    });

    it("keyPrefix matches", () => {
      expect(REDIS_CACHE_DEFAULTS.keyPrefix).toBe(data.redisDefaults.keyPrefix);
    });

    it("invalidationChannel matches", () => {
      expect(REDIS_CACHE_DEFAULTS.invalidationChannel).toBe(
        data.redisDefaults.invalidationChannel,
      );
    });

    it("commandTimeoutMs matches", () => {
      expect(REDIS_CACHE_DEFAULTS.commandTimeoutMs).toBe(
        data.redisDefaults.commandTimeoutMs,
      );
    });

    it("connectTimeoutMs matches", () => {
      expect(REDIS_CACHE_DEFAULTS.connectTimeoutMs).toBe(
        data.redisDefaults.connectTimeoutMs,
      );
    });

    it("connectRetries matches", () => {
      expect(REDIS_CACHE_DEFAULTS.connectRetries).toBe(
        data.redisDefaults.connectRetries,
      );
    });

    it("abortOnConnectFail matches", () => {
      expect(REDIS_CACHE_DEFAULTS.abortOnConnectFail).toBe(
        data.redisDefaults.abortOnConnectFail,
      );
    });

    it("canonical map is byte-equal", () => {
      const ts = {
        defaultExpirationMs: REDIS_CACHE_DEFAULTS.defaultExpirationMs,
        keyPrefix: REDIS_CACHE_DEFAULTS.keyPrefix,
        invalidationChannel: REDIS_CACHE_DEFAULTS.invalidationChannel,
        commandTimeoutMs: REDIS_CACHE_DEFAULTS.commandTimeoutMs,
        connectTimeoutMs: REDIS_CACHE_DEFAULTS.connectTimeoutMs,
        connectRetries: REDIS_CACHE_DEFAULTS.connectRetries,
        abortOnConnectFail: REDIS_CACHE_DEFAULTS.abortOnConnectFail,
      };
      expect(canonicalize(ts)).toEqual(canonicalize(data.redisDefaults));
    });
  });

  describe("redisMeter (KOM-04)", () => {
    it("meter name matches", () => {
      expect(REDIS_CACHE_METER_NAME).toBe(data.redisMeter.name);
    });

    it("meter version is 1.0.0", () => {
      expect(TS_METER_VERSION).toBe(data.redisMeter.version);
    });

    it("instruments (name, unit, description) match", () => {
      expect(canonicalize(TS_REDIS_INSTRUMENTS)).toEqual(
        canonicalize(data.redisMeter.instruments),
      );
    });
  });

  describe("luaScripts (KOM-06)", () => {
    // Both sides normalize via trim + LF. TS constants already .trim()'d;
    // fixture was emitted through NormalizeLua on the .NET emitter.
    it("INCREMENT_WITH_OPTIONAL_TTL body matches (LF-normalized + trim)", () => {
      expect(INCREMENT_WITH_OPTIONAL_TTL).toBe(
        data.luaScripts.INCREMENT_WITH_OPTIONAL_TTL,
      );
    });

    it("RELEASE_LOCK_IF_OWNER body matches (LF-normalized + trim)", () => {
      expect(RELEASE_LOCK_IF_OWNER).toBe(data.luaScripts.RELEASE_LOCK_IF_OWNER);
    });

    it("SET_ADD_WITH_OPTIONAL_TTL body matches (LF-normalized + trim)", () => {
      expect(SET_ADD_WITH_OPTIONAL_TTL).toBe(
        data.luaScripts.SET_ADD_WITH_OPTIONAL_TTL,
      );
    });
  });

  describe("tieredSemantics (KOM-08 — semantic only)", () => {
    it("BACKPLANE_NOT_REGISTERED_MESSAGE contains shared prefix", () => {
      expect(BACKPLANE_NOT_REGISTERED_MESSAGE).toContain(
        data.tieredSemantics.backplaneNotRegisteredMessagePrefix,
      );
    });

    it("prefix is the closed shared fragment", () => {
      expect(data.tieredSemantics.backplaneNotRegisteredMessagePrefix).toBe(
        "ICacheInvalidationBackplane is not registered",
      );
    });

    it("eventIds are [1, 2]", () => {
      expect(data.tieredSemantics.eventIds).toEqual([1, 2]);
    });

    it("logLevels are both Warning", () => {
      expect(data.tieredSemantics.logLevels).toEqual(["Warning", "Warning"]);
    });

    it("TS TieredCacheOp is a fixed closed set {set,setMany,remove,removeMany}", () => {
      expect(TS_TIERED_OP_SET).toEqual(
        ["remove", "removeMany", "set", "setMany"].sort(),
      );
    });

    it("dotnet binding fields include Key/ErrorCode/Operation (not template text)", () => {
      expect(data.tieredSemantics.dotnetBindingFields.event1).toEqual([
        "Key",
        "ErrorCode",
      ]);
      expect(data.tieredSemantics.dotnetBindingFields.event2).toEqual([
        "Operation",
        "KeyOrCount",
        "ErrorCode",
      ]);
    });

    it("errorCode binding present on both runtimes", () => {
      expect(data.tieredSemantics.errorCodeBindingPresent).toBe(true);
      // TS sentinel proves the closed-set errorCode SoT is exported.
      expect(TIERED_ERROR_CODE_UNKNOWN).toBe("unknown");
    });
  });
});
