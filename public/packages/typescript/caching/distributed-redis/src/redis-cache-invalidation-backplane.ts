// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  InputFailures,
  type ICacheInvalidationBackplane,
} from "@dcsv-io/d2-caching-abstractions";
import type { ILogger } from "@dcsv-io/d2-logging";
import { sanitizedErrorRender } from "@dcsv-io/d2-logging";
import {
  canceled,
  ok,
  serviceUnavailable,
  type D2Result,
} from "@dcsv-io/d2-result";

/**
 * Closed-set backplane operation names for warn bindings (§21.11).
 */
export const BackplaneOp = {
  PUBLISH: "Backplane.Publish",
  PUBLISH_MANY: "Backplane.PublishMany",
} as const;

/** Closed-set type for {@link BackplaneOp} values. */
export type BackplaneOpName = (typeof BackplaneOp)[keyof typeof BackplaneOp];
import { falsey } from "@dcsv-io/d2-utilities";
import type Redis from "ioredis";
import { randomUUID } from "node:crypto";

import type { RedisCacheOptions } from "./redis-cache-options.js";

/**
 * Redis pub/sub implementation of {@link ICacheInvalidationBackplane}.
 *
 * **Dual-connection:** the host injects a **command** Redis client used
 * only for PUBLISH. At construction the backplane owns
 * `commandRedis.duplicate()` as the subscriber, starts channel subscribe
 * without an await in the sync constructor, and exposes
 * {@link ready} which resolves when channel subscription is established.
 *
 * Dispose unsubscribes and quits the **owned subscriber only** - never
 * the host command client. Publish after dispose does not throw (match
 * .NET). Handler-subscribe after dispose throws.
 *
 * Port `subscribe(handler)` remains sync and returns `AsyncDisposable`.
 */
export class RedisCacheInvalidationBackplane implements ICacheInvalidationBackplane {
  private readonly commandRedis: Redis;
  private readonly subscriber: Redis;
  private readonly channel: string;
  private readonly logger: ILogger;
  private readonly subscriptions = new Map<string, BackplaneSubscription>();
  private disposed = false;
  private channelReady = false;

  /**
   * Resolves when the owned-subscriber channel subscription is established.
   * Hosts and integration tests await this before delivery-dependent
   * publish/receive assertions. Construction stays synchronous.
   */
  readonly ready: Promise<void>;

  /**
   * @param commandRedis - Host-owned command client (publish path; never
   *   quit by this instance).
   * @param options - Cache options (channel name from `invalidationChannel`).
   * @param logger - Required structured logger.
   */
  constructor(
    commandRedis: Redis,
    options: RedisCacheOptions,
    logger: ILogger,
  ) {
    if (commandRedis == null) {
      throw new TypeError("commandRedis is required");
    }

    if (options == null) {
      throw new TypeError("options is required");
    }

    if (logger == null) {
      throw new TypeError("logger is required");
    }

    if (falsey(options.invalidationChannel)) {
      throw new RangeError(
        `RedisCacheOptions.invalidationChannel must be non-empty, ` +
          `got ${String(options.invalidationChannel)}.`,
      );
    }

    this.commandRedis = commandRedis;
    this.logger = logger;
    this.channel = options.invalidationChannel;
    this.subscriber = commandRedis.duplicate();

    this.subscriber.on("message", (ch, message) => {
      if (ch === this.channel) {
        this.onMessage(message);
      }
    });

    // Start channel subscribe without await in ctor - surface via ready.
    this.ready = this.subscriber.subscribe(this.channel).then(() => {
      this.channelReady = true;
    });
  }

  /**
   * Registers a handler. Sync (port contract) - not channel setup.
   *
   * @throws When the backplane is disposed or `handler` is nullish.
   */
  subscribe(
    handler: (key: string, signal?: AbortSignal) => void | Promise<void>,
  ): AsyncDisposable {
    if (handler == null) {
      throw new TypeError("handler is required");
    }

    this.throwIfDisposed();
    const id = randomUUID();
    const sub = new BackplaneSubscription(id, handler, this);
    this.subscriptions.set(id, sub);

    return sub;
  }

  /**
   * Publishes one invalidation key on the command client. Does not throw
   * when disposed (match .NET); may return `ok` or `serviceUnavailable`.
   */
  async publishInvalidation(
    key: string,
    signal?: AbortSignal,
  ): Promise<D2Result> {
    if (signal?.aborted === true) {
      return canceled();
    }

    if (falsey(key)) {
      return InputFailures.required("key");
    }

    try {
      await this.commandRedis.publish(this.channel, key);

      return ok();
    } catch (err) {
      this.logRedisOp(BackplaneOp.PUBLISH, err, key);

      return serviceUnavailable();
    }
  }

  /**
   * Bulk-publish on the command client. Does not throw when disposed.
   */
  async publishInvalidationMany(
    keys: readonly string[],
    signal?: AbortSignal,
  ): Promise<D2Result> {
    if (signal?.aborted === true) {
      return canceled();
    }

    if (falsey(keys)) {
      return InputFailures.required("keys");
    }

    try {
      await Promise.all(
        keys.map((key) => this.commandRedis.publish(this.channel, key)),
      );

      return ok();
    } catch (err) {
      this.logRedisOp(BackplaneOp.PUBLISH_MANY, err, `${keys.length} keys`);

      return serviceUnavailable();
    }
  }

  /**
   * Unsubscribes the channel on the owned subscriber, quits that
   * subscriber only, cancels handler subscriptions, and clears the map.
   * Idempotent. Never quits the host command client.
   */
  async dispose(): Promise<void> {
    if (this.disposed) {
      return;
    }

    this.disposed = true;

    try {
      await this.subscriber.unsubscribe(this.channel);
    } catch {
      // Best-effort on shutdown; swallow.
    }

    try {
      await this.subscriber.quit();
    } catch {
      // Best-effort on shutdown; swallow.
    }

    for (const sub of this.subscriptions.values()) {
      sub.signalCancellation();
    }

    this.subscriptions.clear();
  }

  /** Delegates to {@link dispose} for `await using` declarations. */
  async [Symbol.asyncDispose](): Promise<void> {
    await this.dispose();
  }

  /** @internal */
  unsubscribeHandler(id: string): void {
    this.subscriptions.delete(id);
  }

  private onMessage(key: string): void {
    if (!this.channelReady || this.disposed) {
      return;
    }

    for (const sub of this.subscriptions.values()) {
      void this.invokeHandler(sub, key);
    }
  }

  private async invokeHandler(
    sub: BackplaneSubscription,
    key: string,
  ): Promise<void> {
    try {
      await sub.handler(key, sub.signal);
    } catch (err) {
      if (sub.signal.aborted) {
        return;
      }

      const exceptionType = sanitizedErrorRender(err).name;
      this.logger.warn("Backplane handler threw", {
        exceptionType,
        key,
      });
    }
  }

  private throwIfDisposed(): void {
    if (this.disposed) {
      throw new Error("RedisCacheInvalidationBackplane is disposed.");
    }
  }

  private logRedisOp(
    op: BackplaneOpName,
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

class BackplaneSubscription implements AsyncDisposable {
  private readonly ac = new AbortController();
  private disposed = false;

  constructor(
    private readonly id: string,
    readonly handler: (
      key: string,
      signal?: AbortSignal,
    ) => void | Promise<void>,
    private readonly backplane: RedisCacheInvalidationBackplane,
  ) {}

  get signal(): AbortSignal {
    return this.ac.signal;
  }

  async [Symbol.asyncDispose](): Promise<void> {
    if (this.disposed) {
      return;
    }

    this.disposed = true;
    this.backplane.unsubscribeHandler(this.id);
    this.ac.abort();
  }

  /** Aborts the handler token (idempotent; safe after subscription dispose). */
  signalCancellation(): void {
    this.ac.abort();
  }
}
