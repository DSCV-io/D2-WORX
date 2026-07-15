// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  connectRedis,
  createRedisCacheOptions,
  JsonCacheSerializer,
  RedisCacheInvalidationBackplane,
  RedisDistributedCache,
  type RedisCacheOptions,
} from "@d2/caching-distributed-redis";
import { DefaultLocalCache } from "@d2/caching-local-default";
import {
  RedisContainer,
  type StartedRedisContainer,
} from "@testcontainers/redis";
import { randomUUID } from "node:crypto";

import { DefaultTieredCache } from "../../src/index.js";
import { createNoOpTestLogger } from "../tiered-double-test-harness.js";

type RedisClient = ReturnType<typeof connectRedis>;

export interface TieredIntegrationFixture {
  container: StartedRedisContainer;
  connectionString: string;
  createCommandClient(): RedisClient;
  createTieredContext(opts?: {
    channel?: string;
    keyPrefix?: string;
  }): Promise<TieredTestContext>;
  uniquePrefix(): string;
  uniqueChannel(): string;
}

export interface TieredTestContext {
  l1: DefaultLocalCache;
  l2: RedisDistributedCache;
  backplane: RedisCacheInvalidationBackplane;
  tiered: DefaultTieredCache;
  redis: RedisClient;
  dispose(): Promise<void>;
}

/**
 * Starts a Testcontainers Redis instance for the tiered integration suite.
 */
export async function startTieredTestFixture(): Promise<TieredIntegrationFixture> {
  const container = await new RedisContainer("redis:7.2-alpine").start();
  const connectionString = container.getConnectionUrl();

  return {
    container,
    connectionString,
    createCommandClient(): RedisClient {
      return connectRedis(
        createRedisCacheOptions({
          connectionString,
          commandTimeoutMs: 5_000,
          connectTimeoutMs: 10_000,
        }),
      );
    },
    async createTieredContext(opts) {
      const channel = opts?.channel ?? uniqueChannel();
      const keyPrefix = opts?.keyPrefix ?? uniquePrefix();
      const redisOpts: RedisCacheOptions = createRedisCacheOptions({
        connectionString,
        keyPrefix,
        invalidationChannel: channel,
        defaultExpirationMs: 300_000,
        commandTimeoutMs: 5_000,
        connectTimeoutMs: 10_000,
      });
      const redis = connectRedis(redisOpts);
      const logger = createNoOpTestLogger();
      const backplane = new RedisCacheInvalidationBackplane(
        redis,
        redisOpts,
        logger,
      );
      await backplane.ready;
      const l1 = new DefaultLocalCache({
        defaultExpirationMs: 300_000,
      });
      const l2 = new RedisDistributedCache({
        redis,
        options: redisOpts,
        serializer: new JsonCacheSerializer(),
        logger,
      });
      const tiered = new DefaultTieredCache({
        l1,
        l2,
        logger,
        backplane,
      });

      return {
        l1,
        l2,
        backplane,
        tiered,
        redis,
        async dispose() {
          await tiered.dispose();
          await backplane.dispose();
          l1.dispose();
          await redis.quit();
        },
      };
    },
    uniquePrefix(): string {
      return uniquePrefix();
    },
    uniqueChannel,
  };
}

function uniquePrefix(): string {
  return `t:${randomUUID()}:`;
}

function uniqueChannel(): string {
  return `d2:cache:it:${randomUUID()}`;
}

/** Deferred signal for pub/sub delivery waits (no bare fixed sleep). */
export function createDeferredTestSignal<T = void>(): {
  promise: Promise<T>;
  resolve: (value: T) => void;
} {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((r) => {
    resolve = r;
  });

  return { promise, resolve };
}

/** Poll with attempt budget until predicate is true. */
export async function waitUntilTestBudget(
  predicate: () => boolean | Promise<boolean>,
  attempts = 50,
  delayMs = 20,
): Promise<void> {
  for (let i = 0; i < attempts; i++) {
    if (await predicate()) {
      return;
    }

    await new Promise((r) => setTimeout(r, delayMs));
  }

  throw new Error("waitUntilTestBudget exhausted");
}
