// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { afterEach, describe, expect, it, vi } from "vitest";

const { RedisMock } = vi.hoisted(() => {
  const RedisMock = vi.fn(function MockRedis(
    this: { url: string; opts: unknown },
    url: string,
    opts: unknown,
  ) {
    this.url = url;
    this.opts = opts;
  });

  return { RedisMock };
});

vi.mock("ioredis", () => ({
  default: RedisMock,
}));

import { connectRedis } from "../src/connect-redis.js";
import { createRedisCacheOptions } from "../src/index.js";

describe("connectRedis success path", () => {
  afterEach(() => {
    RedisMock.mockClear();
  });

  it("connectRedis_withConnectionString_constructsCommandClient", () => {
    const options = createRedisCacheOptions({
      connectionString: "redis://127.0.0.1:6379/0",
      connectTimeoutMs: 1_000,
      commandTimeoutMs: 500,
      connectRetries: 2,
      abortOnConnectFail: false,
    });
    const client = connectRedis(options) as unknown as {
      url: string;
      opts: {
        connectTimeout: number;
        commandTimeout: number;
        maxRetriesPerRequest: number;
        lazyConnect: boolean;
        enableOfflineQueue: boolean;
      };
    };

    expect(RedisMock).toHaveBeenCalledOnce();
    expect(client.url).toBe("redis://127.0.0.1:6379/0");
    expect(client.opts.connectTimeout).toBe(1_000);
    expect(client.opts.commandTimeout).toBe(500);
    expect(client.opts.maxRetriesPerRequest).toBe(2);
    expect(client.opts.lazyConnect).toBe(false);
    expect(client.opts.enableOfflineQueue).toBe(true);
  });

  it("connectRedis_abortOnConnectFail_disablesOfflineQueue", () => {
    const options = createRedisCacheOptions({
      connectionString: "redis://localhost:1",
      abortOnConnectFail: true,
    });
    const client = connectRedis(options) as unknown as {
      opts: { enableOfflineQueue: boolean };
    };

    expect(client.opts.enableOfflineQueue).toBe(false);
  });
});
