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
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Service-identity token request to Edge returned HTTP {StatusCode}.")]
    public static partial void ServiceIdentityHttpFailure(this ILogger logger, int statusCode);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Service-identity token fetch failed: {ExceptionType} ({FirstFrame}).")]
    public static partial void ServiceIdentityFetchFailed(
        this ILogger logger,
        string exceptionType,
        string firstFrame);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Warning,
        Message = "Initial service-identity token acquisition failed at startup; "
                + "will retry on the polling cadence ({PollInterval}).")]
    public static partial void ServiceIdentityStartupAcquireFailed(
        this ILogger logger,
        TimeSpan pollInterval);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Warning,
        Message = "Service-identity proactive refresh failed; will retry on next tick.")]
    public static partial void ServiceIdentityRefreshTickFailed(this ILogger logger);

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
}
