// -----------------------------------------------------------------------
// <copyright file="RequestOriginGrpcLog.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Grpc.Telemetry;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="LoggerMessage"/>-compiled log delegates for the cross-process
/// establishment interceptor. Compiled once at type-load (no allocations / format-
/// string parsing at the call site) per CA1848.
/// </summary>
/// <remarks>
/// <strong>PII discipline</strong>: NO delegate accepts an <see cref="Exception"/>
/// and the only string parameter is a non-PII workload service label (e.g. <c>edge</c>);
/// the inbound call-path is summarized by its entry COUNT, never its contents.
/// </remarks>
// §5.6 carve-out: [LoggerMessage] partial methods cannot be declared inside a
// C# 14 extension(...) block (compiler-syntactic restriction), so the block form
// is categorically inapplicable here; instance-extension style is correct.
internal static partial class RequestOriginGrpcLog
{
    [LoggerMessage(
        EventId = 4101,
        Level = LogLevel.Debug,
        Message = "Cross-process hop established at {SelfServiceId}; call-path now has "
                + "{HopCount} entries.")]
    public static partial void CallPathReceived(
        this ILogger logger,
        int hopCount,
        string selfServiceId);

    [LoggerMessage(
        EventId = 4104,
        Level = LogLevel.Warning,
        Message = "Cross-process hop at {SelfServiceId} carried no validated mTLS peer "
                + "identity; request origin left unestablished (fail-closed) — a "
                + "misconfigured non-mTLS hop, not silently accepted.")]
    public static partial void CrossProcessPeerIdentityAbsent(
        this ILogger logger,
        string selfServiceId);
}
