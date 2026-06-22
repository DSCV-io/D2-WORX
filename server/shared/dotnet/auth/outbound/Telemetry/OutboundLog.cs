// -----------------------------------------------------------------------
// <copyright file="OutboundLog.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.Telemetry;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="LoggerMessage"/>-compiled log delegates for the outbound auth
/// runtime. Compiled once at type-load (no allocations / format-string parsing
/// at the call site) per CA1848.
/// </summary>
// §5.6 carve-out: [LoggerMessage] partial methods cannot be declared inside a
// C# 14 extension(...) block (compiler-syntactic restriction), so the block form
// is categorically inapplicable here; instance-extension style is correct.
internal static partial class OutboundLog
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "OIDC discovery doc missing token_endpoint for issuer {Issuer}.")]
    public static partial void OidcDiscoveryMissingTokenEndpoint(
        this ILogger logger,
        string issuer);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "TokenExchangeCache: ICacheInvalidationBackplane is not registered. "
                + "Session-revoke events will not propagate cross-instance; falling back "
                + "to TTL-only invalidation. Register a backplane for cluster coherency.")]
    public static partial void TokenExchangeBackplaneAbsent(this ILogger logger);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "TokenExchangeCache: malformed session-revoked backplane message {Key}.")]
    public static partial void TokenExchangeBackplaneMalformedSessionRevoke(
        this ILogger logger,
        string key);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "TokenExchangeCache: purged {KeyCount} entries for revoked session {SessionId}.")]
    public static partial void TokenExchangeSessionRevokedPurged(
        this ILogger logger,
        Guid sessionId,
        int keyCount);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Warning,
        Message = "Token-exchange request to Edge returned HTTP {StatusCode}.")]
    public static partial void TokenExchangeHttpFailure(this ILogger logger, int statusCode);

    [LoggerMessage(
        EventId = 2005,
        Level = LogLevel.Warning,
        Message = "Token-exchange request failed: {ExceptionType} ({FirstFrame}).")]
    public static partial void TokenExchangeFetchFailed(
        this ILogger logger,
        string exceptionType,
        string firstFrame);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "Workload leaf reissue failed: {ExceptionType} ({FirstFrame}).")]
    public static partial void WorkloadLeafReissueFailed(
        this ILogger logger,
        string exceptionType,
        string firstFrame);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "Initial workload leaf acquisition failed at startup; "
                + "will retry on the polling cadence ({PollInterval}).")]
    public static partial void WorkloadLeafStartupAcquireFailed(
        this ILogger logger,
        TimeSpan pollInterval);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Warning,
        Message = "Workload leaf proactive reissue failed; will retry on next tick.")]
    public static partial void WorkloadLeafRefreshTickFailed(this ILogger logger);
}
