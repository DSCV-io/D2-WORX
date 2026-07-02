// -----------------------------------------------------------------------
// <copyright file="AuthLog.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Telemetry;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="LoggerMessage"/>-compiled log delegates for the inbound auth
/// runtime. Compiled once at type-load (no allocations / format-string parsing
/// at the call site) per CA1848.
/// </summary>
/// <remarks>
/// <para>
/// <strong>PII discipline</strong>: NO delegate accepts an
/// <see cref="Exception"/> parameter directly — exception messages can
/// interpolate JWT bytes, request URIs, response bodies, or other runtime
/// data that must not reach the log pipeline. Callers pass
/// <see cref="D2.Shared.Utilities.Diagnostics.SanitizedExceptionRender.TypeName(Exception)"/>
/// and
/// <see cref="D2.Shared.Utilities.Diagnostics.SanitizedExceptionRender.FirstFrame(Exception)"/>
/// as separate strings instead. Enforced across the class by reflection-based contract
/// tests in the test project.
/// </para>
/// </remarks>
// §5.6 carve-out: [LoggerMessage] partial methods cannot be declared inside a
// C# 14 extension(...) block (compiler-syntactic restriction), so the block form
// is categorically inapplicable here; instance-extension style is correct.
internal static partial class AuthLog
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "OIDC discovery doc missing jwks_uri for issuer {Issuer}.")]
    public static partial void OidcDiscoveryMissingJwksUri(this ILogger logger, string issuer);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "JWKS fetch failed: {ExceptionType} ({FirstFrame}).")]
    public static partial void JwksFetchFailed(
        this ILogger logger,
        string exceptionType,
        string firstFrame);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "JWKS refresh triggered ({Trigger}); current snapshot has {KidCount} keys "
                + "from {SourceUri}.")]
    public static partial void JwksRefreshTriggered(
        this ILogger logger,
        string trigger,
        int kidCount,
        string sourceUri);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Debug,
        Message = "JWKS refresh suppressed by cooldown gate; {ElapsedMs}ms elapsed since last "
                + "refresh, cooldown is {CooldownMs}ms.")]
    public static partial void JwksRefreshCooldownSuppressed(
        this ILogger logger,
        long elapsedMs,
        long cooldownMs);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Session liveness lookup failed: errorCode={ErrorCode}, "
                + "statusCode={StatusCode}. Failing closed.")]
    public static partial void SessionLivenessLookupFailed(
        this ILogger logger,
        string errorCode,
        string statusCode);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "JWT validation failed; outcome={Outcome}, errorCode={ErrorCode}.")]
    public static partial void JwtValidationFailed(
        this ILogger logger,
        string outcome,
        string errorCode);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "JWT validator forced reactive JWKS refresh on unknown kid; "
                + "retrying validation once after refresh.")]
    public static partial void JwtValidationReactiveRefreshTriggered(this ILogger logger);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Warning,
        Message = "JwksBackplaneSubscriber: ICacheInvalidationBackplane is not registered. "
                + "Cluster-wide key-rotated events will not propagate; falling back to "
                + "ConfigurationManager's AutomaticRefreshInterval (default 24h). Register a "
                + "backplane (e.g. via D2.Shared.Caching.Distributed.Redis) for prompt rotation.")]
    public static partial void JwksBackplaneAbsent(this ILogger logger);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "SessionRevokedBackplaneSubscriber: ICacheInvalidationBackplane is not "
                + "registered. Session-revoke event observability metric will not increment; "
                + "the underlying liveness check still works correctly via TTL-based eventual "
                + "consistency. Register a backplane for cluster-wide event observability.")]
    public static partial void SessionRevokedBackplaneAbsent(this ILogger logger);

    // ---- Transport-layer middleware (consumed by D2.Shared.Auth.Http +
    // D2.Shared.Auth.Grpc) ----
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Authorization header missing or non-Bearer; emitting 401 BearerMissing.")]
    public static partial void BearerHeaderMissing(this ILogger logger);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Warning,
        Message = "Endpoint scope requirement not satisfied; {RequiredScopesSummary}. "
                + "Emitting 401 ScopeInsufficient.")]
    public static partial void ScopeRequirementUnmet(
        this ILogger logger,
        string requiredScopesSummary);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Warning,
        Message = "Session liveness lookup returned revoked; emitting 401 SessionRevoked.")]
    public static partial void LivenessRevoked(this ILogger logger);

    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Warning,
        Message = "Endpoint/method scope metadata is present and non-harmless but declares "
                + "an EMPTY scope set (configuration anomaly — the public factories reject "
                + "empty sets); failing closed with 401 ScopeInsufficient.")]
    public static partial void ScopeMetadataEmptyAnomaly(this ILogger logger);
}
