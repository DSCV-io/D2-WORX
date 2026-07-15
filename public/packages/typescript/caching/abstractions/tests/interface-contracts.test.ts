// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  conflict,
  notFound,
  ok,
  validationFailed,
  type D2Result,
} from "@dcsv-io/d2-result";
import { describe, expect, it } from "vitest";

import type {
  ICacheAtomic,
  ICacheBasic,
  ICacheBroadcast,
  ICacheInvalidationBackplane,
  ICacheSerializer,
  ICacheSet,
  IDistributedCache,
  ILocalCache,
  ITieredCache,
} from "../src/index.js";
import * as barrel from "../src/index.js";

/**
 * Type-conformance fixtures for every public interface type.
 * These are structural stubs (not §1.32 collaborator doubles) — full
 * method lists so assignability fails if a PublicAPI method is missing.
 * Runtime op behavior is owned by packages 02-04.
 */

// ---------------------------------------------------------------------------
// Type-level marker composition gates (exercised by `type-check:test`)
// ---------------------------------------------------------------------------

type AssertTrue<T extends true> = T;

type _LocalExtendsBasicAndAtomic = AssertTrue<
  ILocalCache extends ICacheBasic & ICacheAtomic ? true : false
>;
type _LocalExcludesBroadcast = AssertTrue<
  ILocalCache extends ICacheBroadcast ? false : true
>;
type _LocalExcludesSet = AssertTrue<
  ILocalCache extends ICacheSet ? false : true
>;

type _DistributedIncludesSetAndBroadcast = AssertTrue<
  IDistributedCache extends ICacheBasic &
    ICacheAtomic &
    ICacheBroadcast &
    ICacheSet
    ? true
    : false
>;

type _TieredIncludesBroadcast = AssertTrue<
  ITieredCache extends ICacheBasic & ICacheAtomic & ICacheBroadcast
    ? true
    : false
>;
type _TieredExcludesSet = AssertTrue<
  ITieredCache extends ICacheSet ? false : true
>;

// acquireLock third param (expirationMs) must be required — not optional.
// If made optional, `undefined extends Parameters<...>[2]` becomes true
// and AssertTrue fails under type-check:test.
type _AcquireLockExpRequired = AssertTrue<
  undefined extends Parameters<ICacheAtomic["acquireLock"]>[2] ? false : true
>;

// Force the aliases into the type graph (no runtime effect).
const _typeLevelMarkerPins: [
  _LocalExtendsBasicAndAtomic,
  _LocalExcludesBroadcast,
  _LocalExcludesSet,
  _DistributedIncludesSetAndBroadcast,
  _TieredIncludesBroadcast,
  _TieredExcludesSet,
  _AcquireLockExpRequired,
] = [true, true, true, true, true, true, true];
void _typeLevelMarkerPins;

// ---------------------------------------------------------------------------
// Stubs
// ---------------------------------------------------------------------------

const basicStub: ICacheBasic = {
  get: async <T>(_key: string, _signal?: AbortSignal): Promise<D2Result<T>> =>
    notFound<T>(),
  getMany: async <T>(
    _keys: readonly string[],
    _signal?: AbortSignal,
  ): Promise<D2Result<ReadonlyMap<string, T>>> =>
    ok(new Map() as ReadonlyMap<string, T>),
  exists: async (_key, _signal?) => ok(false),
  getTtl: async (_key, _signal?) => ok(undefined),
  set: async (_key, _value, _expirationMs?, _signal?) => ok(),
  setMany: async (_entries, _expirationMs?, _signal?) => ok(),
  remove: async (_key, _signal?) => ok(),
  removeMany: async (_keys, _signal?) => ok(),
};

const atomicStub: ICacheAtomic = {
  setNx: async (_key, _value, _expirationMs?, _signal?) => ok(true),
  increment: async (_key, _amount?, _expirationMs?, _signal?) => ok(1),
  acquireLock: async (_key, _lockId, _expirationMs, _signal?) => ok(true),
  releaseLock: async (_key, _lockId, _signal?) => ok(),
};

const broadcastStub: ICacheBroadcast = {
  setAndBroadcast: async (_key, _value, _expirationMs?, _signal?) => ok(),
  setManyAndBroadcast: async (_entries, _expirationMs?, _signal?) => ok(),
  removeAndBroadcast: async (_key, _signal?) => ok(),
  removeManyAndBroadcast: async (_keys, _signal?) => ok(),
};

const setStub: ICacheSet = {
  setAdd: async (_key, _member, _expirationMs?, _signal?) => ok(true),
  setCardinality: async (_key, _signal?) => ok(0),
  setRemove: async (_key, _member, _signal?) => ok(false),
  setContains: async (_key, _member, _signal?) => ok(false),
};

function emptyAsyncDisposable(): AsyncDisposable {
  let disposed = false;
  return {
    async [Symbol.asyncDispose]() {
      disposed = true;
      void disposed;
    },
  };
}

const backplaneStub: ICacheInvalidationBackplane = {
  subscribe: (_handler) => emptyAsyncDisposable(),
  publishInvalidation: async (_key, _signal?) => ok(),
  publishInvalidationMany: async (_keys, _signal?) => ok(),
  async [Symbol.asyncDispose]() {
    /* no-op */
  },
};

const serializerStub: ICacheSerializer = {
  contentType: "application/json",
  serialize: <T>(_value: T): D2Result<Uint8Array> => ok(new Uint8Array()),
  deserialize: <T>(_bytes: Uint8Array): D2Result<T> => notFound<T>(),
};

// ---------------------------------------------------------------------------
// C1. ICacheBasic
// ---------------------------------------------------------------------------

describe("ICacheBasic contract", () => {
  it("iCacheBasic_stub_assignable_exposes_get_getMany_exists_getTtl_set_setMany_remove_removeMany", () => {
    const cache: ICacheBasic = basicStub;

    expect(typeof cache.get).toBe("function");
    expect(typeof cache.getMany).toBe("function");
    expect(typeof cache.exists).toBe("function");
    expect(typeof cache.getTtl).toBe("function");
    expect(typeof cache.set).toBe("function");
    expect(typeof cache.setMany).toBe("function");
    expect(typeof cache.remove).toBe("function");
    expect(typeof cache.removeMany).toBe("function");
  });

  it("basicStub_get_canReturnNotFound", async () => {
    const result = await basicStub.get<string>("missing");

    expect(result.success).toBe(false);
  });

  it("basicStub_acceptsEmptyKeysCollectionShape", async () => {
    const result = await basicStub.getMany<string>([]);

    expect(result.success).toBe(true);
    expect(result.data).toBeInstanceOf(Map);
  });

  it("basicStub_methods_arePromiseD2Result", async () => {
    const getP = basicStub.get<string>("k");
    const existsP = basicStub.exists("k");
    const removeP = basicStub.remove("k");

    expect(getP).toBeInstanceOf(Promise);
    expect(existsP).toBeInstanceOf(Promise);
    expect(removeP).toBeInstanceOf(Promise);

    const [getR, existsR, removeR] = await Promise.all([
      getP,
      existsP,
      removeP,
    ]);

    expect(typeof getR.success).toBe("boolean");
    expect(typeof existsR.success).toBe("boolean");
    expect(typeof removeR.success).toBe("boolean");
  });

  it("basicStub_canReturnValidationFailedShape", async () => {
    const failStub: ICacheBasic = {
      ...basicStub,
      get: async <T>() => validationFailed<T>(),
    };
    const result = await failStub.get<string>("");

    expect(result.success).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// C2. ICacheAtomic
// ---------------------------------------------------------------------------

describe("ICacheAtomic contract", () => {
  it("iCacheAtomic_stub_assignable_exposes_setNx_increment_acquireLock_releaseLock", () => {
    const cache: ICacheAtomic = atomicStub;

    expect(typeof cache.setNx).toBe("function");
    expect(typeof cache.increment).toBe("function");
    expect(typeof cache.acquireLock).toBe("function");
    expect(typeof cache.releaseLock).toBe("function");
  });

  it("atomicStub_acquireLock_requiresExpirationMsInSignature", async () => {
    // Runtime smoke only — required-expirationMs is pinned at compile time by
    // `_AcquireLockExpRequired` above (type-check:test). This call still needs
    // the third arg under the real signature.
    const result = await atomicStub.acquireLock("lock:k", "id-1", 5_000);

    expect(result.success).toBe(true);
    expect(result.data).toBe(true);
  });

  it("atomicStub_increment_canReturnConflict", async () => {
    const conflictStub: ICacheAtomic = {
      ...atomicStub,
      increment: async () => conflict<number>(),
    };
    const result = await conflictStub.increment("counter");

    expect(result.success).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// C3. ICacheBroadcast
// ---------------------------------------------------------------------------

describe("ICacheBroadcast contract", () => {
  it("iCacheBroadcast_stub_assignable_exposes_setAndBroadcast_setManyAndBroadcast_removeAndBroadcast_removeManyAndBroadcast", () => {
    const cache: ICacheBroadcast = broadcastStub;

    expect(typeof cache.setAndBroadcast).toBe("function");
    expect(typeof cache.setManyAndBroadcast).toBe("function");
    expect(typeof cache.removeAndBroadcast).toBe("function");
    expect(typeof cache.removeManyAndBroadcast).toBe("function");
  });

  it("broadcastStub_methods_arePromiseD2Result", async () => {
    const p = broadcastStub.removeAndBroadcast("k");

    expect(p).toBeInstanceOf(Promise);
    const result = await p;
    expect(typeof result.success).toBe("boolean");
  });
});

// ---------------------------------------------------------------------------
// C4. ICacheSet
// ---------------------------------------------------------------------------

describe("ICacheSet contract", () => {
  it("iCacheSet_stub_assignable_exposes_setAdd_setCardinality_setRemove_setContains", () => {
    const cache: ICacheSet = setStub;

    expect(typeof cache.setAdd).toBe("function");
    expect(typeof cache.setCardinality).toBe("function");
    expect(typeof cache.setRemove).toBe("function");
    expect(typeof cache.setContains).toBe("function");
  });

  it("setStub_setAdd_acceptsOptionalExpirationMs", async () => {
    const withTtl = await setStub.setAdd("s", "m", 60_000);
    const withoutTtl = await setStub.setAdd("s", "m");

    expect(withTtl.success).toBe(true);
    expect(withoutTtl.success).toBe(true);
  });

  it("setStub_setCardinality_returnsPromiseD2ResultNumber", async () => {
    const p = setStub.setCardinality("s");

    expect(p).toBeInstanceOf(Promise);
    const result = await p;
    expect(result.success).toBe(true);
    expect(typeof result.data).toBe("number");
  });
});

// ---------------------------------------------------------------------------
// C5. Markers
// ---------------------------------------------------------------------------

describe("marker composition", () => {
  it("markerComposition_localCache_extendsBasicAndAtomic_excludesBroadcastAndSet", () => {
    const local: ILocalCache = { ...basicStub, ...atomicStub };

    expect(typeof local.get).toBe("function");
    expect(typeof local.acquireLock).toBe("function");
    // Compile-time: ILocalCache does not require broadcast/set members.
    expect("setAndBroadcast" in local || "setAdd" in local).toBe(false);
  });

  it("markerComposition_distributed_includesSet_andBroadcast", () => {
    const distributed: IDistributedCache = {
      ...basicStub,
      ...atomicStub,
      ...broadcastStub,
      ...setStub,
    };

    expect(typeof distributed.setAndBroadcast).toBe("function");
    expect(typeof distributed.setAdd).toBe("function");
  });

  it("markerComposition_tiered_includesBroadcast_excludesSet", () => {
    const tiered: ITieredCache = {
      ...basicStub,
      ...atomicStub,
      ...broadcastStub,
    };

    expect(typeof tiered.setAndBroadcast).toBe("function");
    expect("setAdd" in tiered).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// C6. ICacheInvalidationBackplane
// ---------------------------------------------------------------------------

describe("ICacheInvalidationBackplane contract", () => {
  it("backplaneStub_assignable_exposes_subscribe_publishInvalidation_publishInvalidationMany_andAsyncDispose", () => {
    const bp: ICacheInvalidationBackplane = backplaneStub;

    expect(typeof bp.subscribe).toBe("function");
    expect(typeof bp.publishInvalidation).toBe("function");
    expect(typeof bp.publishInvalidationMany).toBe("function");
    expect(typeof bp[Symbol.asyncDispose]).toBe("function");
  });

  it("backplaneStub_subscribe_returnsAsyncDisposable", () => {
    const sub = backplaneStub.subscribe(async () => {
      /* no-op */
    });

    expect(typeof sub[Symbol.asyncDispose]).toBe("function");
  });

  it("backplaneStub_asyncDispose_isIdempotentSafe", async () => {
    const sub = backplaneStub.subscribe(() => {
      /* no-op */
    });

    await sub[Symbol.asyncDispose]();
    await expect(sub[Symbol.asyncDispose]()).resolves.toBeUndefined();
    await backplaneStub[Symbol.asyncDispose]();
    await expect(backplaneStub[Symbol.asyncDispose]()).resolves.toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// C7. ICacheSerializer
// ---------------------------------------------------------------------------

describe("ICacheSerializer contract", () => {
  it("serializerStub_assignable_exposes_contentType_serialize_deserialize", () => {
    const s: ICacheSerializer = serializerStub;

    expect(typeof s.contentType).toBe("string");
    expect(typeof s.serialize).toBe("function");
    expect(typeof s.deserialize).toBe("function");
  });

  it("serializerStub_contentType_isString", () => {
    expect(serializerStub.contentType).toBe("application/json");
  });

  it("serializerStub_roundTripShape_usesUint8Array", () => {
    const bytes = serializerStub.serialize({ a: 1 });

    expect(bytes.success).toBe(true);
    expect(bytes.data).toBeInstanceOf(Uint8Array);
  });
});

// ---------------------------------------------------------------------------
// D. Package exports / barrel
// ---------------------------------------------------------------------------

describe("package index re-exports", () => {
  it("packageIndex_reexports_allPublicSurfaces", () => {
    expect(barrel.InputFailures).toBeDefined();
    expect(typeof barrel.InputFailures.required).toBe("function");
    expect(typeof barrel.InputFailures.invalid).toBe("function");
    expect(barrel.LOCAL_CACHE_DEFAULTS).toBeDefined();
    expect(typeof barrel.createLocalCacheOptions).toBe("function");

    // Type-only exports are erased at runtime; pin that the barrel
    // module object is non-empty and the runtime helpers are present.
    const keys = Object.keys(barrel);
    expect(keys).toEqual(
      expect.arrayContaining([
        "InputFailures",
        "LOCAL_CACHE_DEFAULTS",
        "createLocalCacheOptions",
      ]),
    );
  });
});
