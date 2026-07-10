// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Byte-equivalent Lua bodies to .NET RedisLuaScripts.cs (INCREMENT /
// RELEASE_LOCK / SET_ADD). Internal only - not a public executor surface.

/** Atomic INCRBY + optional PEXPIRE when PTTL < 0 and ARGV[2] != '0'. */
export const INCREMENT_WITH_OPTIONAL_TTL = `
local result = redis.call('INCRBY', KEYS[1], ARGV[1])
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
