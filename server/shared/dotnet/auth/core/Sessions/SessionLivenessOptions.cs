// -----------------------------------------------------------------------
// <copyright file="SessionLivenessOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Sessions;

/// <summary>
/// Session-liveness-specific knobs on <see cref="AuthOptions"/>. Composed
/// under <see cref="AuthOptions.Sessions"/>.
/// </summary>
/// <remarks>
/// Use the parameterless ctor for all defaults; use the parameterized ctor
/// (positional or named args) to override one or more values without an
/// object initializer. The <c>with</c>-expression also works for record-style
/// selective overrides.
/// </remarks>
public sealed record SessionLivenessOptions
{
    /// <summary>Default cache-key prefix. Internal — see <see cref="CacheKeyPrefix"/>.</summary>
    internal const string _DEFAULT_CACHE_KEY_PREFIX = "session:";

    /// <summary>
    /// Initializes a new <see cref="SessionLivenessOptions"/>. Each parameter
    /// is nullable; passing <c>null</c> (or omitting the argument) yields the
    /// documented default for that property. Use <c>new()</c> for all defaults.
    /// </summary>
    /// <param name="cacheKeyPrefix">
    /// Override for <see cref="CacheKeyPrefix"/>; <c>null</c> = default
    /// <c>"session:"</c>.
    /// </param>
    public SessionLivenessOptions(string? cacheKeyPrefix = null)
    {
        CacheKeyPrefix = cacheKeyPrefix ?? _DEFAULT_CACHE_KEY_PREFIX;
    }

    /// <summary>
    /// Gets the cache key prefix used for session-liveness sentinel entries.
    /// Edge writes <c>session:{sessionId:N}</c> on session creation; backend
    /// services check existence under this prefix on every authenticated
    /// request. Default <c>"session:"</c>.
    /// </summary>
    public string CacheKeyPrefix { get; init; }
}
