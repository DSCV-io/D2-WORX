// -----------------------------------------------------------------------
// <copyright file="JwksProviderOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Jwks;

/// <summary>
/// JWKS-specific knobs on <see cref="AuthOptions"/>. Composed under
/// <c>AuthOptions.Jwks</c>; not configured directly by callers.
/// </summary>
/// <remarks>
/// Use the parameterless ctor for all defaults; use the parameterized ctor
/// (positional or named args) to override one or more values without an
/// object initializer. The <c>with</c>-expression also works for record-style
/// selective overrides.
/// </remarks>
public sealed record JwksProviderOptions
{
    /// <summary>
    /// Default refresh cooldown — 30 seconds. Internal so consumers don't
    /// reference it directly.
    /// </summary>
    internal static readonly TimeSpan SR_DefaultRefreshCooldown = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default HTTP request timeout — 5 seconds. Internal — see
    /// <see cref="SR_DefaultRefreshCooldown"/>.
    /// </summary>
    internal static readonly TimeSpan SR_DefaultHttpRequestTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Default consecutive-failure count before the circuit-breaker opens — 5.
    /// Internal — see <see cref="SR_DefaultRefreshCooldown"/>.
    /// </summary>
    internal const int _DEFAULT_CIRCUIT_BREAKER_FAILURE_THRESHOLD = 5;

    /// <summary>
    /// Default circuit-breaker cooldown duration — 30 seconds. Internal — see
    /// <see cref="SR_DefaultRefreshCooldown"/>.
    /// </summary>
    internal static readonly TimeSpan SR_DefaultCircuitBreakerCooldown = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default backplane channel key. Internal — see
    /// <see cref="SR_DefaultRefreshCooldown"/>.
    /// </summary>
    internal const string _DEFAULT_BACKPLANE_CHANNEL_KEY = "d2.security.key-rotated:jwks";

    /// <summary>
    /// Initializes a new <see cref="JwksProviderOptions"/>. Each parameter
    /// is nullable; passing <c>null</c> (or omitting the argument) yields the
    /// documented default for that property. Use <c>new()</c> for all defaults.
    /// </summary>
    /// <param name="refreshCooldown">
    /// Override for <see cref="RefreshCooldown"/>; <c>null</c> = default 30 seconds.
    /// </param>
    /// <param name="httpRequestTimeout">
    /// Override for <see cref="HttpRequestTimeout"/>; <c>null</c> = default 5 seconds.
    /// </param>
    /// <param name="circuitBreakerFailureThreshold">
    /// Override for <see cref="CircuitBreakerFailureThreshold"/>; <c>null</c> = default 5.
    /// </param>
    /// <param name="circuitBreakerCooldown">
    /// Override for <see cref="CircuitBreakerCooldown"/>; <c>null</c> = default 30 seconds.
    /// </param>
    /// <param name="backplaneChannelKey">
    /// Override for <see cref="BackplaneChannelKey"/>; <c>null</c> = default
    /// <c>"d2.security.key-rotated:jwks"</c>. Empty / whitespace strings are
    /// rejected at validation time (see <c>AddD2Auth</c>).
    /// </param>
    public JwksProviderOptions(
        TimeSpan? refreshCooldown = null,
        TimeSpan? httpRequestTimeout = null,
        int? circuitBreakerFailureThreshold = null,
        TimeSpan? circuitBreakerCooldown = null,
        string? backplaneChannelKey = null)
    {
        RefreshCooldown = refreshCooldown ?? SR_DefaultRefreshCooldown;
        HttpRequestTimeout = httpRequestTimeout ?? SR_DefaultHttpRequestTimeout;
        CircuitBreakerFailureThreshold =
            circuitBreakerFailureThreshold ?? _DEFAULT_CIRCUIT_BREAKER_FAILURE_THRESHOLD;
        CircuitBreakerCooldown = circuitBreakerCooldown ?? SR_DefaultCircuitBreakerCooldown;
        BackplaneChannelKey = backplaneChannelKey ?? _DEFAULT_BACKPLANE_CHANNEL_KEY;
    }

    /// <summary>
    /// Gets the minimum interval between consecutive forced JWKS refreshes.
    /// Default 30 seconds. Calls to <see cref="HttpJwksProvider.RefreshAsync"/>
    /// within this window after the previous refresh return success without
    /// forcing another upstream fetch — prevents reactive-refresh-on-unknown-kid
    /// stampedes during sustained validation failures.
    /// </summary>
    public TimeSpan RefreshCooldown { get; init; }

    /// <summary>
    /// Gets the per-request timeout applied to the named OIDC discovery
    /// <see cref="System.Net.Http.HttpClient"/>. Default 5 seconds. Without
    /// this override, the BCL default of 100 seconds applies — a hung
    /// upstream Edge would tie up the calling thread for the full window.
    /// </summary>
    public TimeSpan HttpRequestTimeout { get; init; }

    /// <summary>
    /// Gets the consecutive-failure count at which the JWKS-fetch circuit
    /// breaker opens. Default 5. Once open, calls fail fast with
    /// <c>AuthFailures.JwksUnavailable</c> for the
    /// <see cref="CircuitBreakerCooldown"/> window — avoids per-call HTTP
    /// roundtrips during sustained Edge outage.
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; init; }

    /// <summary>
    /// Gets the duration the JWKS-fetch circuit breaker stays open before
    /// allowing a half-open probe. Default 30 seconds.
    /// </summary>
    public TimeSpan CircuitBreakerCooldown { get; init; }

    /// <summary>
    /// Gets the cache backplane channel pattern for cluster-wide
    /// <c>key-rotated</c> events targeting the JWKS domain. The
    /// <see cref="JwksBackplaneSubscriber"/> matches received invalidation
    /// keys against this string and triggers a refresh on any match. Default
    /// <c>"d2.security.key-rotated:jwks"</c>. Empty / whitespace values are
    /// rejected at host build time via <c>ValidateOnStart</c> — an unmatched
    /// channel key would silently drop every rotation event.
    /// </summary>
    /// <remarks>
    /// <strong>Cross-service contract.</strong> Edge MUST publish key-rotation
    /// events on this exact channel string for subscribers to react. The
    /// default is the canonical D² channel; only override if Edge's publisher
    /// (in <c>D2.Shared.KeyCustodian</c>) is configured with the same override.
    /// Mismatch causes silent drop of every rotation event — fall-back to
    /// ConfigurationManager's <c>AutomaticRefreshInterval</c> (default 24h).
    /// </remarks>
    public string BackplaneChannelKey { get; init; }
}
