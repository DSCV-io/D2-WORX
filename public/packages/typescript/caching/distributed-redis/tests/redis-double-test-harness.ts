// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { ILogger, LogBindings } from "@d2/logging";
import type Redis from "ioredis";

import {
  INCREMENT_WITH_OPTIONAL_TTL,
  RELEASE_LOCK_IF_OWNER,
  SET_ADD_WITH_OPTIONAL_TTL,
} from "../src/redis-lua-scripts.js";

/** No-op logger for unit tests (required ctor seam). */
export class NoOpTestLogger implements ILogger {
  trace(_message: string, _bindings?: LogBindings): void {}
  debug(_message: string, _bindings?: LogBindings): void {}
  info(_message: string, _bindings?: LogBindings): void {}
  warn(_message: string, _bindings?: LogBindings): void {}
  error(_message: string, _bindings?: LogBindings): void {}
  fatal(_message: string, _bindings?: LogBindings): void {}
  child(_bindings: LogBindings): ILogger {
    return this;
  }
}

/** Factory for {@link NoOpTestLogger}. */
export function createNoOpTestLogger(): NoOpTestLogger {
  return new NoOpTestLogger();
}

/** In-memory store row for {@link RedisTestDouble}. */
interface TestStoreEntry {
  value: string;
  expiresAt?: number;
  kind: "string" | "set";
  members?: Set<string>;
}

/** Pub/sub message callback shape for the test bus. */
type TestMessageHandler = (channel: string, message: string) => void;

/** Shared in-memory pub/sub bus for command + subscriber doubles. */
interface TestPubSubBus {
  handlers: Set<TestMessageHandler>;
}

/**
 * Faithful in-memory Redis command double covering the cache/backplane
 * command surface. Replace-trigger: prefer integration coverage when a
 * branch is exercised against real Redis.
 */
export class RedisTestDouble {
  readonly store = new Map<string, TestStoreEntry>();
  readonly published: Array<{ channel: string; message: string }> = [];
  readonly quitCalls: string[] = [];
  readonly children: RedisTestDouble[] = [];
  /**
   * Sticky redis-down fault: every command throws this until cleared.
   * Sticky (not one-shot) so multi-command ops (e.g. increment GET+eval)
   * keep failing after a best-effort pre-check swallows the first throw.
   */
  throwOnNext?: Error;
  /**
   * When set, the next `get` throws this error once (then clears). Used to
   * force the increment pre-check skip so the post-INCRBY reverse path runs.
   * Does not set sticky `throwOnNext` — eval/decrby still run.
   */
  throwOnNextGet?: Error;
  /** Count of `get` invocations (pre-INCRBY safe-int check coverage). */
  getCallCount = 0;
  /** Count of `eval` invocations (assert pre-check refuses without Lua). */
  evalCallCount = 0;
  /**
   * When set on the command double, every `duplicate()` child awaits this
   * before completing `subscribe` (unit tests for pre-ready message drop).
   */
  childSubscribeHold?: Promise<void>;
  /** Optional hold for this instance's own `subscribe` (copied to children). */
  subscribeHold?: Promise<void>;
  throwOnUnsubscribe?: Error;
  throwOnQuit?: Error;
  private readonly clock: () => number;
  private readonly bus: TestPubSubBus;
  private readonly channels = new Set<string>();
  private readonly label: string;

  constructor(
    clock: () => number = Date.now,
    label = "command",
    bus?: TestPubSubBus,
  ) {
    this.clock = clock;
    this.label = label;
    this.bus = bus ?? { handlers: new Set() };
  }

  asRedis(): Redis {
    return this as unknown as Redis;
  }

  /** Shared pub/sub bus with the parent command double. */
  duplicate(): RedisTestDouble {
    const child = new RedisTestDouble(this.clock, "subscriber", this.bus);
    child.subscribeHold = this.childSubscribeHold;
    this.children.push(child);

    return child;
  }

  async get(key: string): Promise<string | null> {
    this.getCallCount++;

    if (this.throwOnNextGet !== undefined) {
      const err = this.throwOnNextGet;
      this.throwOnNextGet = undefined;
      throw err;
    }

    this.maybeThrow();
    const entry = this.liveString(key);

    return entry?.value ?? null;
  }

  async mget(...keys: string[]): Promise<Array<string | null>> {
    this.maybeThrow();
    const out: Array<string | null> = [];

    for (const key of keys) {
      out.push(await this.get(key));
    }

    return out;
  }

  async set(
    key: string,
    value: string,
    ...args: Array<string | number>
  ): Promise<string | null> {
    this.maybeThrow();
    let px: number | undefined;
    let nx = false;

    for (let i = 0; i < args.length; i++) {
      const a = args[i];

      if (a === "PX") {
        px = Number(args[i + 1]);
        i++;
      } else if (a === "NX") {
        nx = true;
      }
    }

    if (nx && this.live(key) !== undefined) {
      return null;
    }

    const entry: TestStoreEntry = {
      value: String(value),
      kind: "string",
    };

    if (px !== undefined && px > 0) {
      entry.expiresAt = this.clock() + px;
    }

    this.store.set(key, entry);

    return "OK";
  }

  async del(...keys: string[]): Promise<number> {
    this.maybeThrow();
    let n = 0;

    for (const key of keys) {
      if (this.store.delete(key)) {
        n++;
      }
    }

    return n;
  }

  /**
   * DECRBY used by overflow reverse path after INCRBY leaves a non-safe
   * integer. Tracks calls for unit assertions.
   */
  readonly decrbyCalls: Array<{ key: string; amount: number }> = [];

  async decrby(key: string, amount: number | string): Promise<number> {
    this.maybeThrow();
    const delta = Number(amount);

    this.decrbyCalls.push({ key, amount: delta });

    let entry = this.live(key);

    if (entry !== undefined && entry.kind !== "string") {
      throw wrongType();
    }

    if (entry === undefined) {
      const next = -delta;
      entry = { value: String(next), kind: "string" };
      this.store.set(key, entry);

      return next;
    }

    if (!/^-?\d+$/.test(entry.value)) {
      throw notInteger();
    }

    const next = Number(entry.value) - delta;
    entry.value = String(next);

    return next;
  }

  async exists(key: string): Promise<number> {
    this.maybeThrow();

    return this.live(key) !== undefined ? 1 : 0;
  }

  async pttl(key: string): Promise<number> {
    this.maybeThrow();
    const entry = this.live(key);

    if (entry === undefined) {
      return -2;
    }

    if (entry.expiresAt === undefined) {
      return -1;
    }

    return Math.max(0, entry.expiresAt - this.clock());
  }

  async publish(channel: string, message: string): Promise<number> {
    this.maybeThrow();
    this.published.push({ channel, message });

    for (const handler of this.bus.handlers) {
      handler(channel, message);
    }

    return this.bus.handlers.size;
  }

  async subscribe(...channels: string[]): Promise<number> {
    this.maybeThrow();

    if (this.subscribeHold !== undefined) {
      await this.subscribeHold;
    }

    for (const ch of channels) {
      this.channels.add(ch);
    }

    return this.channels.size;
  }

  async unsubscribe(...channels: string[]): Promise<number> {
    if (this.throwOnUnsubscribe !== undefined) {
      throw this.throwOnUnsubscribe;
    }

    for (const ch of channels) {
      this.channels.delete(ch);
    }

    return this.channels.size;
  }

  on(event: string, handler: TestMessageHandler): this {
    if (event === "message") {
      this.bus.handlers.add(handler);
    }

    return this;
  }

  off(event: string, handler: TestMessageHandler): this {
    if (event === "message") {
      this.bus.handlers.delete(handler);
    }

    return this;
  }

  async quit(): Promise<"OK"> {
    if (this.throwOnQuit !== undefined) {
      throw this.throwOnQuit;
    }

    this.quitCalls.push(this.label);

    return "OK";
  }

  async disconnect(): Promise<void> {
    this.quitCalls.push(`${this.label}:disconnect`);
  }

  async eval(
    script: string,
    numKeys: number,
    ...args: Array<string | number>
  ): Promise<number> {
    this.maybeThrow();
    this.evalCallCount++;
    void numKeys;
    const key = String(args[0]);
    const argv1 = String(args[1]);
    const argv2 = args[2] !== undefined ? String(args[2]) : "0";

    if (script === INCREMENT_WITH_OPTIONAL_TTL) {
      return this.evalIncrement(key, argv1, argv2);
    }

    if (script === RELEASE_LOCK_IF_OWNER) {
      return this.evalReleaseLock(key, argv1);
    }

    if (script === SET_ADD_WITH_OPTIONAL_TTL) {
      return this.evalSetAdd(key, argv1, argv2);
    }

    throw new Error(
      `unknown script in RedisTestDouble: ${script.slice(0, 40)}`,
    );
  }

  async scard(key: string): Promise<number> {
    this.maybeThrow();
    const entry = this.live(key);

    if (entry === undefined) {
      return 0;
    }

    if (entry.kind !== "set") {
      throw wrongType();
    }

    return entry.members!.size;
  }

  async srem(key: string, member: string): Promise<number> {
    this.maybeThrow();
    const entry = this.live(key);

    if (entry === undefined || entry.kind !== "set") {
      return 0;
    }

    return entry.members!.delete(member) ? 1 : 0;
  }

  async sismember(key: string, member: string): Promise<number> {
    this.maybeThrow();
    const entry = this.live(key);

    if (entry === undefined || entry.kind !== "set") {
      return 0;
    }

    return entry.members!.has(member) ? 1 : 0;
  }

  pipeline(): RedisTestPipeline {
    return new RedisTestPipeline(this);
  }

  private evalIncrement(key: string, amount: string, ttlMs: string): number {
    let entry = this.live(key);
    const delta = Number(amount);

    if (entry !== undefined && entry.kind !== "string") {
      throw wrongType();
    }

    if (entry !== undefined) {
      if (!/^-?\d+$/.test(entry.value)) {
        throw notInteger();
      }

      const next = Number(entry.value) + delta;

      // Twin of Lua: reverse in-script if outside JS max safe integer.
      if (!Number.isSafeInteger(next)) {
        throw safeIntegerOverflow();
      }

      entry.value = String(next);

      // PTTL < 0 only when no expiresAt (mirror Redis -1 no-TTL).
      if (ttlMs !== "0" && entry.expiresAt === undefined) {
        entry.expiresAt = this.clock() + Number(ttlMs);
      }

      return next;
    }

    const next = delta;

    if (!Number.isSafeInteger(next)) {
      throw safeIntegerOverflow();
    }

    entry = { value: String(next), kind: "string" };

    if (ttlMs !== "0") {
      entry.expiresAt = this.clock() + Number(ttlMs);
    }

    this.store.set(key, entry);

    return next;
  }

  private evalReleaseLock(key: string, lockId: string): number {
    const entry = this.liveString(key);

    if (entry === undefined || entry.value !== lockId) {
      return 0;
    }

    this.store.delete(key);

    return 1;
  }

  private evalSetAdd(key: string, member: string, ttlMs: string): number {
    let entry = this.live(key);

    if (entry !== undefined && entry.kind !== "set") {
      throw wrongType();
    }

    if (entry === undefined) {
      entry = { value: "", kind: "set", members: new Set() };
      this.store.set(key, entry);
    }

    const added = entry.members!.has(member) ? 0 : 1;
    entry.members!.add(member);

    if (ttlMs !== "0" && entry.expiresAt === undefined) {
      entry.expiresAt = this.clock() + Number(ttlMs);
    }

    return added;
  }

  private live(key: string): TestStoreEntry | undefined {
    const entry = this.store.get(key);

    if (entry === undefined) {
      return undefined;
    }

    if (entry.expiresAt !== undefined && entry.expiresAt <= this.clock()) {
      this.store.delete(key);

      return undefined;
    }

    return entry;
  }

  private liveString(key: string): TestStoreEntry | undefined {
    const entry = this.live(key);

    if (entry === undefined) {
      return undefined;
    }

    if (entry.kind !== "string") {
      throw wrongType();
    }

    return entry;
  }

  private maybeThrow(): void {
    if (this.throwOnNext !== undefined) {
      // Sticky: do not clear — models connection-down for multi-command ops.
      throw this.throwOnNext;
    }
  }
}

/** Pipeline double used by setMany. */
export class RedisTestPipeline {
  private readonly ops: Array<() => Promise<unknown>> = [];

  constructor(private readonly redis: RedisTestDouble) {}

  set(key: string, value: string, ...args: Array<string | number>): this {
    this.ops.push(() => this.redis.set(key, value, ...args));

    return this;
  }

  /**
   * Mirrors ioredis Pipeline.exec: resolves with per-command
   * `[err, result]` tuples and does **not** reject when a command fails.
   */
  async exec(): Promise<Array<[Error | null, unknown]>> {
    const results: Array<[Error | null, unknown]> = [];

    for (const op of this.ops) {
      try {
        results.push([null, await op()]);
      } catch (err) {
        results.push([err as Error, null]);
      }
    }

    return results;
  }
}

/** Creates a command-surface {@link RedisTestDouble}. */
export function createRedisTestDouble(clock?: () => number): RedisTestDouble {
  return new RedisTestDouble(clock);
}

function wrongType(): Error {
  const err = new Error(
    "WRONGTYPE Operation against a key holding the wrong kind of value",
  );
  err.name = "ReplyError";

  return err;
}

function notInteger(): Error {
  const err = new Error("ERR value is not an integer or out of range");
  err.name = "ReplyError";

  return err;
}

function safeIntegerOverflow(): Error {
  const err = new Error("ERR safe_integer_overflow");
  err.name = "ReplyError";

  return err;
}
