// -----------------------------------------------------------------------
// <copyright file="RedisLuaScripts.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Caching.Distributed.Redis;

/// <summary>
/// Lua scripts used internally by the Redis cache impl for atomic
/// compound operations. Kept here so the source is reviewable in one
/// place rather than scattered through op handlers.
/// </summary>
internal static class RedisLuaScripts
{
    /// <summary>
    /// Atomic INCRBY + optional PEXPIRE. ARGV[1] = amount; ARGV[2] = TTL
    /// in milliseconds (0 = no expiration). Sets the TTL only when the
    /// key is created or when a TTL is explicitly requested — Redis
    /// PEXPIRE on every call would clobber existing TTLs each increment.
    /// </summary>
    internal const string INCREMENT_WITH_OPTIONAL_TTL = """
        local result = redis.call('INCRBY', KEYS[1], ARGV[1])
        if ARGV[2] ~= '0' then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        return result
        """;

    /// <summary>
    /// Atomic compare-and-delete for distributed lock release. Only DELs
    /// the key if the stored value matches ARGV[1] (the lockId). Returns
    /// 1 if removed, 0 if mismatch / absent.
    /// </summary>
    internal const string RELEASE_LOCK_IF_OWNER = """
        if redis.call('GET', KEYS[1]) == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        else
            return 0
        end
        """;

    /// <summary>
    /// Atomic SADD + optional PEXPIRE on first add. ARGV[1] = member;
    /// ARGV[2] = TTL in milliseconds (0 = no expiration). Sets the TTL
    /// only when the set is being created (TTL not yet set), so existing
    /// sets keep their TTL.
    /// </summary>
    internal const string SET_ADD_WITH_OPTIONAL_TTL = """
        local added = redis.call('SADD', KEYS[1], ARGV[1])
        if ARGV[2] ~= '0' and redis.call('PTTL', KEYS[1]) < 0 then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        return added
        """;
}
