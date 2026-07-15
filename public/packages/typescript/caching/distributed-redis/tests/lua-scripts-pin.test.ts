// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  INCREMENT_WITH_OPTIONAL_TTL,
  RELEASE_LOCK_IF_OWNER,
  SET_ADD_WITH_OPTIONAL_TTL,
} from "../src/redis-lua-scripts.js";

const EXPECTED = {
  INCREMENT_WITH_OPTIONAL_TTL: [
    "local result = redis.call('INCRBY', KEYS[1], ARGV[1])",
    "if result > 9007199254740991 or result < -9007199254740991 then",
    "    redis.call('DECRBY', KEYS[1], ARGV[1])",
    "    return redis.error_reply('ERR safe_integer_overflow')",
    "end",
    "if ARGV[2] ~= '0' and redis.call('PTTL', KEYS[1]) < 0 then",
    "    redis.call('PEXPIRE', KEYS[1], ARGV[2])",
    "end",
    "return result",
  ].join("\n"),
  RELEASE_LOCK_IF_OWNER: [
    "if redis.call('GET', KEYS[1]) == ARGV[1] then",
    "    return redis.call('DEL', KEYS[1])",
    "else",
    "    return 0",
    "end",
  ].join("\n"),
  SET_ADD_WITH_OPTIONAL_TTL: [
    "local added = redis.call('SADD', KEYS[1], ARGV[1])",
    "if ARGV[2] ~= '0' and redis.call('PTTL', KEYS[1]) < 0 then",
    "    redis.call('PEXPIRE', KEYS[1], ARGV[2])",
    "end",
    "return added",
  ].join("\n"),
} as const;

describe("Lua script pins", () => {
  it.each([
    ["INCREMENT_WITH_OPTIONAL_TTL", INCREMENT_WITH_OPTIONAL_TTL],
    ["RELEASE_LOCK_IF_OWNER", RELEASE_LOCK_IF_OWNER],
    ["SET_ADD_WITH_OPTIONAL_TTL", SET_ADD_WITH_OPTIONAL_TTL],
  ] as const)("luaScripts_matchDotNetBodies_theory (%s)", (name, body) => {
    expect(body).toBe(EXPECTED[name]);
  });
});
