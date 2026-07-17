// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Byte-equivalent Lua bodies to .NET RedisLuaScripts.cs (INCREMENT /
// RELEASE_LOCK / SET_ADD). Public twin-pin constants for ContractFixtures
// parity (barrel re-export); not an executor API.

/**
 * Atomic INCRBY + optional PEXPIRE when PTTL &lt; 0 and ARGV[2] != '0'.
 * If the result is outside ±9007199254740991 (JS Number.MAX_SAFE_INTEGER,
 * dual-runtime twin of .NET), reverses DECRBY in-script and errors
 * `ERR safe_integer_overflow` so the store is never left out of range.
 */
export const INCREMENT_WITH_OPTIONAL_TTL = `
local result = redis.call('INCRBY', KEYS[1], ARGV[1])
if result > 9007199254740991 or result < -9007199254740991 then
    redis.call('DECRBY', KEYS[1], ARGV[1])
    return redis.error_reply('ERR safe_integer_overflow')
end
if ARGV[2] ~= '0' and redis.call('PTTL', KEYS[1]) < 0 then
    redis.call('PEXPIRE', KEYS[1], ARGV[2])
end
return result
`.trim();

/** Atomic compare-and-delete lock release. Returns 1 if removed, else 0. */
export const RELEASE_LOCK_IF_OWNER = `
if redis.call('GET', KEYS[1]) == ARGV[1] then
    return redis.call('DEL', KEYS[1])
else
    return 0
end
`.trim();

/** Atomic SADD + optional PEXPIRE when PTTL < 0 and ARGV[2] != '0'. */
export const SET_ADD_WITH_OPTIONAL_TTL = `
local added = redis.call('SADD', KEYS[1], ARGV[1])
if ARGV[2] ~= '0' and redis.call('PTTL', KEYS[1]) < 0 then
    redis.call('PEXPIRE', KEYS[1], ARGV[2])
end
return added
`.trim();
