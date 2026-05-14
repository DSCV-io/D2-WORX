// -----------------------------------------------------------------------
// <copyright file="HttpJwksProvider.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Jwks;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using D2.Shared.Auth.Abstractions.Jwks;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Telemetry;
using D2.Shared.Resilience.CircuitBreaker;
using D2.Shared.Resilience.Singleflight;
using D2.Shared.Result;
using D2.Shared.Utilities.Diagnostics;
using D2.Shared.Utilities.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

/// <summary>
/// Default <see cref="IJwksProvider"/> impl. Wraps
/// <see cref="IConfigurationManager{T}"/> for OIDC discovery + JWKS fetch +
/// internal caching + auto-refresh, and adds:
/// <list type="bullet">
///   <item><see cref="Singleflight{TKey, TValue}"/> on
///     <see cref="RefreshAsync"/> — N concurrent reactive-refresh callers
///     dedup to one upstream HTTP call.</item>
///   <item>Configurable cooldown between forced refreshes (default 30s)
///     — prevents reactive-refresh-on-unknown-kid stampedes during sustained
///     validation failures.</item>
///   <item><see cref="CircuitBreaker{T}"/> around upstream fetches
///     (default 5 consecutive failures → 30s open) — fail fast during
///     sustained Edge outage instead of waiting per-call HTTP timeout.</item>
///   <item>Projection from <see cref="OpenIdConnectConfiguration.SigningKeys"/>
///     to <see cref="JwksKeySetSnapshot"/> (kid-indexed dict + fetched-at +
///     source URL) — easier consumption than the raw OIDC config.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// <strong>Cache strategy: process-local only.</strong> JWKS is small,
/// low-frequency, Edge-internal traffic; ConfigurationManager's built-in
/// process-local cache is the storage layer. Cluster coherency is handled by
/// the <see cref="JwksBackplaneSubscriber"/> which forces a refresh on every
/// rotation event — no shared L2 needed.
/// </para>
/// <para>
/// <strong>Backplane integration</strong> happens via
/// <see cref="JwksBackplaneSubscriber"/> (registered as
/// <see cref="Microsoft.Extensions.Hosting.IHostedService"/>) which calls
/// <see cref="RefreshAsync"/> on every <c>key-rotated</c> event for the
/// JWKS domain.
/// </para>
/// </remarks>
internal sealed class HttpJwksProvider : IJwksProvider
{
    // Singleflight key: there's exactly one global "refresh JWKS" operation
    // per process — N concurrent reactive callers dedup to one upstream call.
    private const string _SINGLEFLIGHT_KEY = "force-refresh";

    private readonly IConfigurationManager<OpenIdConnectConfiguration> r_configManager;
    private readonly AuthOptions r_options;
    private readonly ILogger<HttpJwksProvider> r_logger;
    private readonly TimeProvider r_clock;
    private readonly Singleflight<string, D2Result> r_singleflight = new();
    private readonly CircuitBreaker<OpenIdConnectConfiguration> r_circuitBreaker;

    // Volatile so concurrent reads see the most-recent write without a lock.
    // Writes happen at most once per cooldown window via Singleflight, so
    // a Volatile.Write inside the singleflight body is sufficient.
    private long _lastRefreshTicks;

    /// <summary>Initializes a new instance of the <see cref="HttpJwksProvider"/> class.</summary>
    /// <param name="configManager">The shared OIDC configuration manager.</param>
    /// <param name="options">The auth options snapshot.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="clock">The time provider (overridable for tests).</param>
    public HttpJwksProvider(
        IConfigurationManager<OpenIdConnectConfiguration> configManager,
        IOptions<AuthOptions> options,
        ILogger<HttpJwksProvider> logger,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(configManager);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);

        r_configManager = configManager;
        r_options = options.Value;
        r_logger = logger;
        r_clock = clock;

        // Throwing operations count as failures by predicate-default; we don't
        // need value-based failure detection because GetConfigurationAsync
        // returns OpenIdConnectConfiguration directly (no D2Result wrapper).
        r_circuitBreaker = new CircuitBreaker<OpenIdConnectConfiguration>(
            isFailure: static _ => false,
            options: new CircuitBreakerOptions(
                failureThreshold: r_options.Jwks.CircuitBreakerFailureThreshold,
                cooldownDuration: r_options.Jwks.CircuitBreakerCooldown));
    }

    /// <inheritdoc/>
    public async ValueTask<D2Result<JwksKeySetSnapshot>> GetKeysAsync(
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var config = await r_circuitBreaker
                .ExecuteAsync(FetchConfigurationAsync, ct: ct)
                .ConfigureAwait(false);

            if (TryProjectSnapshot(config, out var snapshot, out var missingUriIssuer) is false)
            {
                r_logger.OidcDiscoveryMissingJwksUri(missingUriIssuer);
                RecordFetch(
                AuthTelemetryTags.JwksFetches.Trigger.IMPLICIT,
                AuthTelemetryTags.JwksFetches.Outcome.FAILURE,
                sw.Elapsed.TotalMilliseconds);
                return AuthFailures.JwksUnavailable<JwksKeySetSnapshot>();
            }

            RecordFetch(
                AuthTelemetryTags.JwksFetches.Trigger.IMPLICIT,
                AuthTelemetryTags.JwksFetches.Outcome.SUCCESS,
                sw.Elapsed.TotalMilliseconds);
            return D2Result<JwksKeySetSnapshot>.Ok(snapshot);
        }
        catch (CircuitOpenException)
        {
            RecordFetch(
                AuthTelemetryTags.JwksFetches.Trigger.IMPLICIT,
                AuthTelemetryTags.JwksFetches.Outcome.CIRCUIT_OPEN,
                sw.Elapsed.TotalMilliseconds);
            return AuthFailures.JwksUnavailable<JwksKeySetSnapshot>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var outcome = ClassifyOutcome(ex);
            RecordFetch(
                AuthTelemetryTags.JwksFetches.Trigger.IMPLICIT,
                outcome,
                sw.Elapsed.TotalMilliseconds);
            r_logger.JwksFetchFailed(
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex));
            return AuthFailures.JwksUnavailable<JwksKeySetSnapshot>();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The supplied <paramref name="ct"/> propagates only to the first caller
    /// that wins the singleflight slot — concurrent callers piggyback on that
    /// in-flight result and their own <paramref name="ct"/> values do not
    /// abort the shared upstream call. This is intentional: canceling the
    /// shared call would penalize every other caller. To opt out of the
    /// shared call, await <see cref="GetKeysAsync"/> with the per-call ct
    /// instead.
    /// </remarks>
    public async ValueTask<D2Result> RefreshAsync(CancellationToken ct = default)
    {
        return await r_singleflight.ExecuteAsync(_SINGLEFLIGHT_KEY, RunRefreshAsync, ct)
            .ConfigureAwait(false);
    }

    private static bool TryProjectSnapshot(
        OpenIdConnectConfiguration config,
        [NotNullWhen(true)] out JwksKeySetSnapshot? snapshot,
        [NotNullWhen(false)] out string? missingUriIssuer)
    {
        if (config.JwksUri.Falsey())
        {
            // Discovery doc resolved but jwks_uri is absent — Edge config bug.
            // Tag with the issuer so operators can correlate.
            missingUriIssuer = config.Issuer ?? "<unknown>";
            snapshot = null;
            return false;
        }

        // SigningKeys can include keys without a kid (rare; defensive: skip).
        var keys = new Dictionary<string, Microsoft.IdentityModel.Tokens.SecurityKey>(
            StringComparer.Ordinal);
        foreach (var key in config.SigningKeys)
        {
            if (key.KeyId.Truthy())
                keys[key.KeyId] = key;
        }

        snapshot = new JwksKeySetSnapshot
        {
            Keys = keys,
            FetchedAt = DateTimeOffset.UtcNow,
            SourceUri = new Uri(config.JwksUri),
        };
        missingUriIssuer = null;
        return true;
    }

    private static void RecordFetch(string trigger, string outcome, double elapsedMs)
    {
        var triggerTag = new KeyValuePair<string, object?>(
            AuthTelemetryTags.JwksFetches.TAG_TRIGGER, trigger);
        var outcomeTag = new KeyValuePair<string, object?>(
            AuthTelemetryTags.JwksFetches.TAG_OUTCOME, outcome);
        AuthTelemetry.JwksFetches.Add(1, triggerTag, outcomeTag);
        AuthTelemetry.JwksFetchDurationMs.Record(elapsedMs, triggerTag, outcomeTag);
    }

    // Splits malformed-JSON parse errors from generic network failures —
    // gives operators a distinct outcome tag to alert on. A parse error
    // typically indicates an Edge config bug or proxy interfering with the
    // OIDC discovery doc, which warrants different remediation than
    // transient network outage.
    private static string ClassifyOutcome(Exception ex) => ex switch
    {
        JsonException => AuthTelemetryTags.JwksFetches.Outcome.PARSE_ERROR,
        _ => AuthTelemetryTags.JwksFetches.Outcome.FAILURE,
    };

    // Adapter from ConfigurationManager's Task-returning API to the ValueTask
    // shape CircuitBreaker.ExecuteAsync expects.
    private ValueTask<OpenIdConnectConfiguration> FetchConfigurationAsync(CancellationToken ct)
        => new(r_configManager.GetConfigurationAsync(ct));

    private async ValueTask<D2Result> RunRefreshAsync(CancellationToken ct)
    {
        // Cooldown gate — refuse forced refresh within RefreshCooldown of the
        // last one. Prevents reactive-refresh-on-unknown-kid stampedes during
        // sustained validation failures.
        var nowTicks = r_clock.GetUtcNow().Ticks;
        var lastTicks = Interlocked.Read(ref _lastRefreshTicks);
        if (lastTicks > 0)
        {
            var elapsed = TimeSpan.FromTicks(nowTicks - lastTicks);
            if (elapsed < r_options.Jwks.RefreshCooldown)
            {
                r_logger.JwksRefreshCooldownSuppressed(
                    elapsedMs: (long)elapsed.TotalMilliseconds,
                    cooldownMs: (long)r_options.Jwks.RefreshCooldown.TotalMilliseconds);
                AuthTelemetry.JwksFetches.Add(
                    1,
                    new KeyValuePair<string, object?>(
                        AuthTelemetryTags.JwksFetches.TAG_TRIGGER,
                        AuthTelemetryTags.JwksFetches.Trigger.COOLDOWN_SKIPPED),
                    new KeyValuePair<string, object?>(
                        AuthTelemetryTags.JwksFetches.TAG_OUTCOME,
                        AuthTelemetryTags.JwksFetches.Outcome.SUCCESS));
                return D2Result.Ok();
            }
        }

        // Tell ConfigurationManager to refresh (non-blocking; schedules bg refresh).
        // Subsequent GetConfigurationAsync returns the refreshed snapshot.
        r_configManager.RequestRefresh();

        var sw = Stopwatch.StartNew();
        try
        {
            // Force the refresh to complete by awaiting GetConfigurationAsync.
            // ConfigurationManager throttles consecutive RequestRefresh calls
            // via its own RefreshInterval (default 30s); our cooldown is the
            // outer guard.
            var config = await r_circuitBreaker
                .ExecuteAsync(FetchConfigurationAsync, ct: ct)
                .ConfigureAwait(false);
            Interlocked.Exchange(ref _lastRefreshTicks, nowTicks);
            r_logger.JwksRefreshTriggered(
                trigger: AuthTelemetryTags.JwksFetches.Trigger.REACTIVE,
                kidCount: config.SigningKeys?.Count ?? 0,
                sourceUri: config.JwksUri ?? "<none>");
            RecordFetch(
                AuthTelemetryTags.JwksFetches.Trigger.REACTIVE,
                AuthTelemetryTags.JwksFetches.Outcome.SUCCESS,
                sw.Elapsed.TotalMilliseconds);
            return D2Result.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CircuitOpenException)
        {
            RecordFetch(
                AuthTelemetryTags.JwksFetches.Trigger.REACTIVE,
                AuthTelemetryTags.JwksFetches.Outcome.CIRCUIT_OPEN,
                sw.Elapsed.TotalMilliseconds);
            return AuthFailures.JwksUnavailable();
        }
        catch (Exception ex)
        {
            var outcome = ClassifyOutcome(ex);
            RecordFetch(
                AuthTelemetryTags.JwksFetches.Trigger.REACTIVE,
                outcome,
                sw.Elapsed.TotalMilliseconds);
            r_logger.JwksFetchFailed(
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex));
            return AuthFailures.JwksUnavailable();
        }
    }
}
