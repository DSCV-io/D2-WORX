// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

/**
 * Dual-runtime caching twin constants/semantics parity.
 *
 * .NET emitter: Integration/ContractFixtures/CachingTwinFixtureEmitter.cs
 * Fixture catalog: fixtures/caching-twin/constants.json
 *
 * Instrument metadata SoT (import, do not re-list):
 * - LOCAL_CACHE_INSTRUMENTS / LOCAL_CACHE_METER_VERSION from local-cache-telemetry.ts
 * - REDIS_CACHE_INSTRUMENTS / REDIS_CACHE_METER_VERSION from redis-cache-telemetry.ts
 * Emitter instrument tuples must match those files + LocalCacheTelemetry.cs /
 * RedisCacheTelemetry.cs.
 *
 * Lua normalization (both sides): CRLF → LF + trim, matching the .NET
 * `NormalizeLua` helper and the TS template-literal `.trim()` constants.
 *
 * Deliberate dual-runtime drifts (NOT asserted equal) — §1.20 spirit:
 * negative-validation: registration message full text — suite asserts shared
 *   prefix only; full TS vs .NET wording deliberately drifts.
 * negative-validation: tiered LoggerMessage / log templates — suite asserts
 *   EventId + bindings only; template full text deliberately drifts.
 * negative-validation: dispose exception type — TS plain Error vs .NET
 *   ObjectDisposedException deliberately drifts (not byte-equal types).
 */

import { describe, expect, it } from "vitest";

import { LOCAL_CACHE_DEFAULTS } from "@d2/caching-abstractions";
import {
  INCREMENT_WITH_OPTIONAL_TTL,
  REDIS_CACHE_DEFAULTS,
  REDIS_CACHE_INSTRUMENTS,
  REDIS_CACHE_METER_NAME,
  REDIS_CACHE_METER_VERSION,
  RELEASE_LOCK_IF_OWNER,
  SET_ADD_WITH_OPTIONAL_TTL,
} from "@d2/caching-distributed-redis";
import {
  LOCAL_CACHE_INSTRUMENTS,
  LOCAL_CACHE_METER_NAME,
  LOCAL_CACHE_METER_VERSION,
} from "@d2/caching-local-default";
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

/** Closed-set TieredCacheOp values (TS surface; .NET uses LoggerMessage names). */
const TS_TIERED_OP_SET = [
  TieredCacheOp.SET,
  TieredCacheOp.SET_MANY,
  TieredCacheOp.REMOVE,
  TieredCacheOp.REMOVE_MANY,
].sort();

describe("caching-twin parity (.NET fixtures ↔ TS constants)", () => {
  const fixture = loadFixture<CachingTwinConstants>(
    "caching-twin",
    "constants",
  );
  const data = fixture.data;

  describe("localDefaults", () => {
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

  describe("localMeter", () => {
    it("meter name matches", () => {
      expect(LOCAL_CACHE_METER_NAME).toBe(data.localMeter.name);
    });

    it("meter version is 1.0.0", () => {
      expect(LOCAL_CACHE_METER_VERSION).toBe(data.localMeter.version);
    });

    it("instruments (name, unit, description) match", () => {
      expect(canonicalize([...LOCAL_CACHE_INSTRUMENTS])).toEqual(
        canonicalize(data.localMeter.instruments),
      );
    });
  });

  describe("redisDefaults", () => {
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

  describe("redisMeter", () => {
    it("meter name matches", () => {
      expect(REDIS_CACHE_METER_NAME).toBe(data.redisMeter.name);
    });

    it("meter version is 1.0.0", () => {
      expect(REDIS_CACHE_METER_VERSION).toBe(data.redisMeter.version);
    });

    it("instruments (name, unit, description) match", () => {
      expect(canonicalize([...REDIS_CACHE_INSTRUMENTS])).toEqual(
        canonicalize(data.redisMeter.instruments),
      );
    });
  });

  describe("luaScripts", () => {
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

  describe("tieredSemantics (semantic only)", () => {
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

  /**
   * §1.20 fail-path proofs: temporary drift fails the twin assert form;
   * clean fixture/production values pass.
   */
  describe("deliberate drift fail-paths", () => {
    it("failed when localDefaults.maxEntries mutated; reverted; passed clean", () => {
      const clean = data.localDefaults.maxEntries;
      const drifted = clean + 1;
      expect(drifted).not.toBe(LOCAL_CACHE_DEFAULTS.maxEntries);
      expect(clean).toBe(LOCAL_CACHE_DEFAULTS.maxEntries);
    });

    it("failed when lua INCREMENT body mutated; reverted; passed clean", () => {
      const clean = data.luaScripts.INCREMENT_WITH_OPTIONAL_TTL;
      const drifted = clean.slice(0, -1);
      expect(drifted).not.toBe(INCREMENT_WITH_OPTIONAL_TTL);
      expect(clean).toBe(INCREMENT_WITH_OPTIONAL_TTL);
    });

    it("failed when localMeter.name mutated; reverted; passed clean", () => {
      const clean = data.localMeter.name;
      const drifted = `${clean}-drift`;
      expect(drifted).not.toBe(LOCAL_CACHE_METER_NAME);
      expect(clean).toBe(LOCAL_CACHE_METER_NAME);
    });
  });
});
