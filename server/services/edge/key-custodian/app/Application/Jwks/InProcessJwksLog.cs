// -----------------------------------------------------------------------
// <copyright file="InProcessJwksLog.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Jwks;

using Microsoft.Extensions.Logging;

/// <summary>
/// PII-safe <see cref="LoggerMessage"/> delegates for
/// <see cref="InProcessJwksProvider"/>. No delegate accepts
/// <see cref="Exception"/> — use sanitized type + first-frame strings.
/// </summary>
// §5.6 carve-out: [LoggerMessage] partial methods cannot live in a C# 14
// extension(...) block; instance-extension style is correct here.
internal static partial class InProcessJwksLog
{
    [LoggerMessage(
        EventId = 5101,
        Level = LogLevel.Warning,
        Message = "In-process JWKS refresh found zero Active/Retiring jwks-signing keys "
            + "(fail-secure ServiceUnavailable).")]
    public static partial void EmptySigningKeyStore(ILogger logger);

    [LoggerMessage(
        EventId = 5102,
        Level = LogLevel.Information,
        Message = "In-process JWKS refreshed; {KidCount} keys from {SourceUri} in {ElapsedMs}ms.")]
    public static partial void RefreshSucceeded(
        ILogger logger,
        int kidCount,
        string sourceUri,
        long elapsedMs);

    [LoggerMessage(
        EventId = 5103,
        Level = LogLevel.Warning,
        Message = "In-process JWKS refresh failed: {ExceptionType} ({FirstFrame}).")]
    public static partial void RefreshFailed(
        ILogger logger,
        string exceptionType,
        string firstFrame);
}
