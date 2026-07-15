// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  InputFailures,
  type ICacheInvalidationBackplane,
  type ICacheSerializer,
  type IDistributedCache,
} from "@dcsv-io/d2-caching-abstractions";
import type { ILogger } from "@dcsv-io/d2-logging";
import { sanitizedErrorRender } from "@dcsv-io/d2-logging";
import {
  bubbleFail,
  canceled,
  conflict,
  notFound,
  ok,
  serviceUnavailable,
  someFound,
  type D2Result,
} from "@dcsv-io/d2-result";
import { falsey } from "@dcsv-io/d2-utilities";
import type Redis from "ioredis";

import type { RedisCacheOptions } from "./redis-cache-options.js";
import {
  INCREMENT_WITH_OPTIONAL_TTL,
  RELEASE_LOCK_IF_OWNER,
  SET_ADD_WITH_OPTIONAL_TTL,
} from "./redis-lua-scripts.js";
import {
  createRedisCacheCounters,
  type RedisCacheCounters,
} from "./redis-cache-telemetry.js";

/** Pinned registration-error message when `*AndBroadcast*` lacks a backplane. */
export const BACKPLANE_NOT_REGISTERED_MESSAGE =
  "ICacheInvalidationBackplane is not registered. Use set / remove " +
  "(no broadcast), or pass a backplane to RedisDistributedCache.";

/**
 * Closed-set operation names for redis cache warn bindings (§21.11).
 * Single SoT for every `su` / `suVoid` / `logRedisOp` emit site.
 */
export const RedisCacheOp = {
  GET: "get",
  GET_MANY: "getMany",
  EXISTS: "exists",
  GET_TTL: "getTtl",
  SET: "set",
  SET_MANY: "setMany",
  REMOVE: "remove",
  REMOVE_MANY: "removeMany",
  SET_NX: "setNx",
  INCREMENT: "increment",
  ACQUIRE_LOCK: "acquireLock",
  RELEASE_LOCK: "releaseLock",
  SET_ADD: "setAdd",
  SET_CARDINALITY: "setCardinality",
  SET_REMOVE: "setRemove",
  SET_CONTAINS: "setContains",
} as const;

/** Closed-set type for {@link RedisCacheOp} values. */
export type RedisCacheOpName = (typeof RedisCacheOp)[keyof typeof RedisCacheOp];

const TEXT_ENCODER = new TextEncoder();
const TEXT_DECODER = new TextDecoder();

/** Constructor dependencies for {@link RedisDistributedCache}. */
export interface RedisDistributedCacheDeps {
  /** Host-owned command Redis client (never disposed by this cache). */
  redis: Redis;
  options: RedisCacheOptions;
  serializer: ICacheSerializer;
  logger: ILogger;
  /** Required for `*AndBroadcast*` methods; omit when broadcast is unused. */
  backplane?: ICacheInvalidationBackplane;
}

/**
 * ioredis-backed implementation of {@link IDistributedCache}. Implements
 * Basic + Atomic + Broadcast + Set over a host-owned **command** Redis
 * client. Does not own or dispose the connection.
 *
 * Redis/connection errors map to `serviceUnavailable`. Increment on a
 * non-numeric key maps to `conflict` (WRONGTYPE / not-an-integer).
 * Whole-op serializer failures use `bubbleFail` on get / set / setMany /
 * setNx / broadcast wrappers; `getMany` skips per-entry deserialize
 * failures (mirror of .NET GetMany).
 *
 * @see IDistributedCache port contracts on `@dcsv-io/d2-caching-abstractions`.
 */
export class RedisDistributedCache implements IDistributedCache {
  private readonly redis: Redis;
  private readonly options: RedisCacheOptions;
  private readonly serializer: ICacheSerializer;
  private readonly logger: ILogger;
  private readonly backplane: ICacheInvalidationBackplane | undefined;
  private readonly counters: RedisCacheCounters;

  /**
   * @param deps - Command Redis client, options, serializer, logger, and
   *   optional invalidation backplane. Does not dispose `deps.redis`.
   */
  constructor(deps: RedisDistributedCacheDeps) {
    if (deps.redis == null) {
      throw new TypeError("redis is required");
    }

    if (deps.options == null) {
      throw new TypeError("options is required");
    }

    if (deps.serializer == null) {
      throw new TypeError("serializer is required");
    }

    if (deps.logger == null) {
      throw new TypeError("logger is required");
    }

    assertOptions(deps.options);
    this.redis = deps.redis;
    this.options = deps.options;
    this.serializer = deps.serializer;
    this.logger = deps.logger;
    this.backplane = deps.backplane;
    this.counters = createRedisCacheCounters();
  }

  /** @inheritdoc */
  async get<T>(key: string, signal?: AbortSignal): Promise<D2Result<T>> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(key)) {
      return InputFailures.required<T>("key");
    }

    try {
      const raw = await this.redis.get(this.prefixed(key));

      if (raw === null) {
        this.counters.misses.add(1);

        return notFound();
      }

      this.counters.hits.add(1);
      const deserialized = this.serializer.deserialize<T>(toBytes(raw));

      if (deserialized.success) {
        return ok(deserialized.data as T);
      }

      return bubbleFail(deserialized);
    } catch (err) {
      return this.su<T>(err, RedisCacheOp.GET, key);
    }
  }

  /** @inheritdoc */
  async getMany<T>(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result<ReadonlyMap<string, T>>> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(keys)) {
      return InputFailures.required<ReadonlyMap<string, T>>("keys");
    }

    try {
      const prefixed = keys.map((k) => this.prefixed(k));
      const values = await this.redis.mget(...prefixed);
      const hits = new Map<string, T>();
      let hitCount = 0;

      for (let i = 0; i < keys.length; i++) {
        const raw = values[i];

        if (raw === null || raw === undefined) {
          continue;
        }

        const deserialized = this.serializer.deserialize<T>(toBytes(raw));

        if (deserialized.success) {
          hits.set(keys[i]!, deserialized.data as T);
          hitCount++;
        }
      }

      this.counters.hits.add(hitCount);
      this.counters.misses.add(keys.length - hitCount);

      if (hitCount === 0) {
        return notFound();
      }

      if (hitCount === keys.length) {
        return ok(hits);
      }

      return someFound({ data: hits });
    } catch (err) {
      this.counters.errors.add(1);
      this.logRedisOp(RedisCacheOp.GET_MANY, err, `${keys.length} keys`);

      return serviceUnavailable();
    }
  }

  /** @inheritdoc */
  async exists(key: string, signal?: AbortSignal): Promise<D2Result<boolean>> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(key)) {
      return InputFailures.required<boolean>("key");
    }

    try {
      const n = await this.redis.exists(this.prefixed(key));

      return ok(n > 0);
    } catch (err) {
      return this.su<boolean>(err, RedisCacheOp.EXISTS, key);
    }
  }

  /** @inheritdoc */
  async getTtl(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result<number | undefined>> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(key)) {
      return InputFailures.required<number | undefined>("key");
    }

    const prefixed = this.prefixed(key);

    try {
      const present = await this.redis.exists(prefixed);

      if (present === 0) {
        return notFound();
      }

      const ttl = await this.redis.pttl(prefixed);

      if (ttl < 0) {
        return ok(undefined);
      }

      return ok(ttl);
    } catch (err) {
      return this.su<number | undefined>(err, RedisCacheOp.GET_TTL, key);
    }
  }

  /** @inheritdoc */
  async set<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(key)) {
      return InputFailures.required("key");
    }

    const expCheck = validateOptionalExpirationMs(expirationMs);

    if (expCheck !== undefined) {
      return expCheck;
    }

    try {
      const serialized = this.serializer.serialize(value);

      if (!serialized.success) {
        return bubbleFail(serialized);
      }

      await this.setRaw(this.prefixed(key), serialized.data!, expirationMs);
      this.counters.sets.add(1);

      return ok();
    } catch (err) {
      return this.suVoid(err, RedisCacheOp.SET, key);
    }
  }

  /** @inheritdoc */
  async setMany<T>(
    entries: ReadonlyMap<string, T>,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(entries)) {
      return InputFailures.required("entries");
    }

    const expCheck = validateOptionalExpirationMs(expirationMs);

    if (expCheck !== undefined) {
      return expCheck;
    }

    try {
      const pipeline = this.redis.pipeline();
      const effective = this.effectiveTtl(expirationMs);

      for (const [key, value] of entries) {
        const serialized = this.serializer.serialize(value);

        if (!serialized.success) {
          return bubbleFail(serialized);
        }

        const payload = toUtf8(serialized.data!);
        const pref = this.prefixed(key);

        if (effective > 0) {
          pipeline.set(pref, payload, "PX", effective);
        } else {
          pipeline.set(pref, payload);
        }
      }

      // ioredis Pipeline.exec resolves with [err, result][] (does not throw
      // on per-command failure). Surface any command error as SU — match
      // .NET Task.WhenAll failure on batch StringSet tasks.
      const results = await pipeline.exec();

      if (results == null || results.length === 0) {
        return this.suVoid(
          new Error("Redis pipeline.exec returned no results"),
          RedisCacheOp.SET_MANY,
          `${entries.size} entries`,
        );
      }

      for (const tuple of results) {
        const err = tuple?.[0];

        if (err != null) {
          return this.suVoid(
            err,
            RedisCacheOp.SET_MANY,
            `${entries.size} entries`,
          );
        }
      }

      this.counters.sets.add(entries.size);

      return ok();
    } catch (err) {
      return this.suVoid(err, RedisCacheOp.SET_MANY, `${entries.size} entries`);
    }
  }

  /** @inheritdoc */
  async remove(key: string, signal?: AbortSignal): Promise<D2Result> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(key)) {
      return InputFailures.required("key");
    }

    try {
      await this.redis.del(this.prefixed(key));
      this.counters.removes.add(1);

      return ok();
    } catch (err) {
      return this.suVoid(err, RedisCacheOp.REMOVE, key);
    }
  }

  /** @inheritdoc */
  async removeMany(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(keys)) {
      return InputFailures.required("keys");
    }

    try {
      const prefixed = keys.map((k) => this.prefixed(k));
      await this.redis.del(...prefixed);
      this.counters.removes.add(keys.length);

      return ok();
    } catch (err) {
      return this.suVoid(err, RedisCacheOp.REMOVE_MANY, `${keys.length} keys`);
    }
  }

  /** @inheritdoc */
  async setNx<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(key)) {
      return InputFailures.required<boolean>("key");
    }

    const expCheck = validateOptionalExpirationMs(expirationMs);

    if (expCheck !== undefined) {
      return expCheck as D2Result<boolean>;
    }

    try {
      const serialized = this.serializer.serialize(value);

      if (!serialized.success) {
        return bubbleFail(serialized);
      }

      const written = await this.setNxRaw(
        this.prefixed(key),
        serialized.data!,
        expirationMs,
      );

      if (written) {
        this.counters.sets.add(1);
      }

      return ok(written);
    } catch (err) {
      return this.su<boolean>(err, RedisCacheOp.SET_NX, key);
    }
  }

  /** @inheritdoc */
  async increment(
    key: string,
    amount?: number,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<number>> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(key)) {
      return InputFailures.required<number>("key");
    }

    const expCheck = validateOptionalExpirationMs(expirationMs);

    if (expCheck !== undefined) {
      return expCheck as D2Result<number>;
    }

    if (amount !== undefined && !Number.isSafeInteger(amount)) {
      return InputFailures.invalid<number>("amount");
    }

    const n = amount ?? 1;
    const ttlMs = expirationMs !== undefined ? String(expirationMs) : "0";
    const prefixed = this.prefixed(key);

    try {
      // Bounds + reverse are atomic in INCREMENT_WITH_OPTIONAL_TTL (twin of
      // .NET RedisLuaScripts) — no client-side GET/DECRBY race window.
      const result = await this.redis.eval(
        INCREMENT_WITH_OPTIONAL_TTL,
        1,
        prefixed,
        String(n),
        ttlMs,
      );

      const next = Number(result);

      return ok(next);
    } catch (err) {
      if (isSafeIntegerOverflow(err)) {
        return InputFailures.invalid<number>("amount");
      }

      if (isTypeConflict(err)) {
        return conflict();
      }

      return this.su<number>(err, RedisCacheOp.INCREMENT, key);
    }
  }

  /** @inheritdoc */
  async acquireLock(
    key: string,
    lockId: string,
    expirationMs: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(key)) {
      return InputFailures.required<boolean>("key");
    }

    if (falsey(lockId)) {
      return InputFailures.required<boolean>("lockId");
    }

    if (!Number.isFinite(expirationMs) || expirationMs <= 0) {
      return InputFailures.invalid<boolean>("expirationMs");
    }

    try {
      const result = await this.redis.set(
        this.prefixed(key),
        lockId,
        "PX",
        expirationMs,
        "NX",
      );

      return ok(result === "OK");
    } catch (err) {
      return this.su<boolean>(err, RedisCacheOp.ACQUIRE_LOCK, key);
    }
  }

  /** @inheritdoc */
  async releaseLock(
    key: string,
    lockId: string,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(key)) {
      return InputFailures.required("key");
    }

    if (falsey(lockId)) {
      return InputFailures.required("lockId");
    }

    try {
      await this.redis.eval(
        RELEASE_LOCK_IF_OWNER,
        1,
        this.prefixed(key),
        lockId,
      );

      return ok();
    } catch (err) {
      return this.suVoid(err, RedisCacheOp.RELEASE_LOCK, key);
    }
  }

  /**
   * Writes then publishes a prefixed invalidation key.
   *
   * @throws {Error} {@link BACKPLANE_NOT_REGISTERED_MESSAGE} when no
   *   backplane was supplied at construction.
   */
  async setAndBroadcast<T>(
    key: string,
    value: T,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const setResult = await this.set(key, value, expirationMs, signal);

    if (!setResult.success) {
      return setResult;
    }

    return this.publishOne(key, signal);
  }

  /**
   * Bulk-write then publish prefixed invalidation keys.
   *
   * @throws {Error} {@link BACKPLANE_NOT_REGISTERED_MESSAGE} when no
   *   backplane was supplied at construction.
   */
  async setManyAndBroadcast<T>(
    entries: ReadonlyMap<string, T>,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const setResult = await this.setMany(entries, expirationMs, signal);

    if (!setResult.success) {
      return setResult;
    }

    return this.publishMany([...entries.keys()], signal);
  }

  /**
   * Removes then publishes a prefixed invalidation key.
   *
   * @throws {Error} {@link BACKPLANE_NOT_REGISTERED_MESSAGE} when no
   *   backplane was supplied at construction.
   */
  async removeAndBroadcast(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const removeResult = await this.remove(key, signal);

    if (!removeResult.success) {
      return removeResult;
    }

    return this.publishOne(key, signal);
  }

  /**
   * Bulk-remove then publish prefixed invalidation keys.
   *
   * @throws {Error} {@link BACKPLANE_NOT_REGISTERED_MESSAGE} when no
   *   backplane was supplied at construction.
   */
  async removeManyAndBroadcast(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    const removeResult = await this.removeMany(keys, signal);

    if (!removeResult.success) {
      return removeResult;
    }

    return this.publishMany(keys, signal);
  }

  /** @inheritdoc */
  async setAdd(
    key: string,
    member: string,
    expirationMs?: number,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(key)) {
      return InputFailures.required<boolean>("key");
    }

    if (falsey(member)) {
      return InputFailures.required<boolean>("member");
    }

    const expCheck = validateOptionalExpirationMs(expirationMs);

    if (expCheck !== undefined) {
      return expCheck as D2Result<boolean>;
    }

    const ttlMs = expirationMs !== undefined ? String(expirationMs) : "0";

    try {
      const result = await this.redis.eval(
        SET_ADD_WITH_OPTIONAL_TTL,
        1,
        this.prefixed(key),
        member,
        ttlMs,
      );

      return ok(Number(result) === 1);
    } catch (err) {
      return this.su<boolean>(err, RedisCacheOp.SET_ADD, key);
    }
  }

  /** @inheritdoc */
  async setCardinality(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result<number>> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(key)) {
      return InputFailures.required<number>("key");
    }

    try {
      const count = await this.redis.scard(this.prefixed(key));

      return ok(count);
    } catch (err) {
      return this.su<number>(err, RedisCacheOp.SET_CARDINALITY, key);
    }
  }

  /** @inheritdoc */
  async setRemove(
    key: string,
    member: string,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(key)) {
      return InputFailures.required<boolean>("key");
    }

    if (falsey(member)) {
      return InputFailures.required<boolean>("member");
    }

    try {
      const n = await this.redis.srem(this.prefixed(key), member);

      return ok(n > 0);
    } catch (err) {
      return this.su<boolean>(err, RedisCacheOp.SET_REMOVE, key);
    }
  }

  /** @inheritdoc */
  async setContains(
    key: string,
    member: string,
    signal?: AbortSignal,
  ): Promise<D2Result<boolean>> {
    if (isAborted(signal)) {
      return canceled();
    }

    if (falsey(key)) {
      return InputFailures.required<boolean>("key");
    }

    if (falsey(member)) {
      return InputFailures.required<boolean>("member");
    }

    try {
      const n = await this.redis.sismember(this.prefixed(key), member);

      return ok(n === 1);
    } catch (err) {
      return this.su<boolean>(err, RedisCacheOp.SET_CONTAINS, key);
    }
  }

  private async publishOne(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    if (this.backplane === undefined) {
      throw new Error(BACKPLANE_NOT_REGISTERED_MESSAGE);
    }

    this.counters.broadcasts.add(1);

    return this.backplane.publishInvalidation(this.prefixed(key), signal);
  }

  private async publishMany(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    if (this.backplane === undefined) {
      throw new Error(BACKPLANE_NOT_REGISTERED_MESSAGE);
    }

    this.counters.broadcasts.add(keys.length);
    const prefixed = keys.map((k) => this.prefixed(k));

    return this.backplane.publishInvalidationMany(prefixed, signal);
  }

  private prefixed(key: string): string {
    return falsey(this.options.keyPrefix) ? key : this.options.keyPrefix + key;
  }

  private effectiveTtl(expirationMs?: number): number {
    return expirationMs ?? this.options.defaultExpirationMs;
  }

  private async setRaw(
    prefixedKey: string,
    bytes: Uint8Array,
    expirationMs?: number,
  ): Promise<void> {
    const payload = toUtf8(bytes);
    const effective = this.effectiveTtl(expirationMs);

    if (effective > 0) {
      await this.redis.set(prefixedKey, payload, "PX", effective);
    } else {
      await this.redis.set(prefixedKey, payload);
    }
  }

  private async setNxRaw(
    prefixedKey: string,
    bytes: Uint8Array,
    expirationMs?: number,
  ): Promise<boolean> {
    const payload = toUtf8(bytes);
    const effective = this.effectiveTtl(expirationMs);

    const result =
      effective > 0
        ? await this.redis.set(prefixedKey, payload, "PX", effective, "NX")
        : await this.redis.set(prefixedKey, payload, "NX");

    return result === "OK";
  }

  private su<T>(
    err: unknown,
    op: RedisCacheOpName,
    keyOrCount: string,
  ): D2Result<T> {
    this.counters.errors.add(1);
    this.logRedisOp(op, err, keyOrCount);

    return serviceUnavailable();
  }

  private suVoid(
    err: unknown,
    op: RedisCacheOpName,
    keyOrCount: string,
  ): D2Result {
    this.counters.errors.add(1);
    this.logRedisOp(op, err, keyOrCount);

    return serviceUnavailable();
  }

  private logRedisOp(
    op: RedisCacheOpName,
    err: unknown,
    keyOrCount: string,
  ): void {
    const exceptionType = sanitizedErrorRender(err).name;
    this.logger.warn("Redis cache operation failed", {
      operation: op,
      exceptionType,
      keyOrCount,
    });
  }
}

function isAborted(signal?: AbortSignal): boolean {
  return signal?.aborted === true;
}

function toBytes(raw: string | Buffer): Uint8Array {
  if (typeof raw === "string") {
    return TEXT_ENCODER.encode(raw);
  }

  return new Uint8Array(raw);
}

function toUtf8(bytes: Uint8Array): string {
  return TEXT_DECODER.decode(bytes);
}

function isTypeConflict(err: unknown): boolean {
  if (!(err instanceof Error)) {
    return false;
  }

  const msg = err.message;

  return msg.includes("WRONGTYPE") || msg.includes("not an integer");
}

/** Dual-runtime Lua reverse + error for out-of-JS-safe-integer INCRBY. */
function isSafeIntegerOverflow(err: unknown): boolean {
  if (!(err instanceof Error)) {
    return false;
  }

  return err.message.includes("safe_integer_overflow");
}

function validateOptionalExpirationMs(
  expirationMs: number | undefined,
): D2Result | undefined {
  if (expirationMs === undefined) {
    return undefined;
  }

  if (!Number.isFinite(expirationMs) || expirationMs <= 0) {
    return InputFailures.invalid("expirationMs");
  }

  return undefined;
}

function assertOptions(options: RedisCacheOptions): void {
  if (!Number.isFinite(options.defaultExpirationMs)) {
    throw new RangeError(
      `RedisCacheOptions.defaultExpirationMs must be a finite number, ` +
        `got ${String(options.defaultExpirationMs)}.`,
    );
  }

  if (
    !Number.isFinite(options.commandTimeoutMs) ||
    options.commandTimeoutMs <= 0
  ) {
    throw new RangeError(
      `RedisCacheOptions.commandTimeoutMs must be a finite number > 0, ` +
        `got ${String(options.commandTimeoutMs)}.`,
    );
  }

  if (
    !Number.isFinite(options.connectTimeoutMs) ||
    options.connectTimeoutMs <= 0
  ) {
    throw new RangeError(
      `RedisCacheOptions.connectTimeoutMs must be a finite number > 0, ` +
        `got ${String(options.connectTimeoutMs)}.`,
    );
  }

  if (
    !Number.isSafeInteger(options.connectRetries) ||
    options.connectRetries < 0
  ) {
    throw new RangeError(
      `RedisCacheOptions.connectRetries must be a non-negative safe ` +
        `integer, got ${String(options.connectRetries)}.`,
    );
  }

  if (falsey(options.invalidationChannel)) {
    throw new RangeError(
      `RedisCacheOptions.invalidationChannel must be non-empty, ` +
        `got ${String(options.invalidationChannel)}.`,
    );
  }
}
