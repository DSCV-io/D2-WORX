// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import Redis from "ioredis";
import { falsey } from "@dcsv-io/d2-utilities";

import type { RedisCacheOptions } from "./redis-cache-options.js";

/** Fixed throw message when `connectionString` is falsey (never echo input). */
export const CONNECTION_STRING_REQUIRED_MESSAGE =
  "RedisCacheOptions.ConnectionString is required.";

/**
 * Builds a host-owned **command** ioredis client from options.
 * Maps timeout / retry knobs. Never logs `connectionString`.
 *
 * @throws {Error} with {@link CONNECTION_STRING_REQUIRED_MESSAGE} when
 *   `connectionString` is falsey.
 */
export function connectRedis(options: RedisCacheOptions): Redis {
  if (falsey(options.connectionString)) {
    throw new Error(CONNECTION_STRING_REQUIRED_MESSAGE);
  }

  return new Redis(options.connectionString!, {
    connectTimeout: options.connectTimeoutMs,
    commandTimeout: options.commandTimeoutMs,
    maxRetriesPerRequest: options.connectRetries,
    lazyConnect: false,
    enableOfflineQueue: !options.abortOnConnectFail,
  });
}
