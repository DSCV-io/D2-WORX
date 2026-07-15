// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import {
  RedisContainer,
  type StartedRedisContainer,
} from "@testcontainers/redis";
import type Redis from "ioredis";
import { randomUUID } from "node:crypto";

import {
  connectRedis,
  createRedisCacheOptions,
  JsonCacheSerializer,
  RedisCacheInvalidationBackplane,
  RedisDistributedCache,
  type RedisCacheOptions,
} from "../../src/index.js";
import { createNoOpTestLogger } from "../redis-double-test-harness.js";

export interface RedisIntegrationFixture {
  container: StartedRedisContainer;
  connectionString: string;
  createCommandClient(): Redis;
  createCache(
    options?: Partial<RedisCacheOptions>,
    backplane?: RedisCacheInvalidationBackplane,
  ): RedisDistributedCache;
  createBackplane(
    command: Redis,
    options?: Partial<RedisCacheOptions>,
  ): RedisCacheInvalidationBackplane;
  uniquePrefix(): string;
  uniqueChannel(): string;
}

/**
 * Starts a Testcontainers Redis instance for the integration suite.
 */
export async function startRedisTestFixture(): Promise<RedisIntegrationFixture> {
  const container = await new RedisContainer("redis:7.2-alpine").start();
  const connectionString = container.getConnectionUrl();

  return {
    container,
    connectionString,
    createCommandClient(): Redis {
      return connectRedis(
        createRedisCacheOptions({
          connectionString,
          commandTimeoutMs: 5_000,
          connectTimeoutMs: 10_000,
        }),
      );
    },
    createCache(options, backplane) {
      const opts = createRedisCacheOptions({
        connectionString,
        defaultExpirationMs: 0,
        ...options,
      });
      const redis = connectRedis(opts);

      return new RedisDistributedCache({
        redis,
        options: opts,
        serializer: new JsonCacheSerializer(),
        logger: createNoOpTestLogger(),
        backplane,
      });
    },
    createBackplane(command, options) {
      return new RedisCacheInvalidationBackplane(
        command,
        createRedisCacheOptions({
          connectionString,
          invalidationChannel: options?.invalidationChannel ?? uniqueChannel(),
          ...options,
        }),
        createNoOpTestLogger(),
      );
    },
    uniquePrefix(): string {
      return `t:${randomUUID()}:`;
    },
    uniqueChannel,
  };
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

/**
 * Attempt-budget absence assert: fails immediately if `predicate` becomes true
 * within the budget (e.g. unwanted second pub/sub delivery). Not a fixed
 * sleep-then-assert success path.
 */
export async function assertNeverWithinTestBudget(
  predicate: () => boolean | Promise<boolean>,
  attempts = 20,
  delayMs = 25,
): Promise<void> {
  for (let i = 0; i < attempts; i++) {
    if (await predicate()) {
      throw new Error("assertNeverWithinTestBudget: condition became true");
    }

    await new Promise((r) => setTimeout(r, delayMs));
  }

  if (await predicate()) {
    throw new Error(
      "assertNeverWithinTestBudget: condition true after budget end",
    );
  }
}
