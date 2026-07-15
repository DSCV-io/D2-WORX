// -----------------------------------------------------------------------
// <copyright file="RequestOrigin.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// What KIND of trust boundary produced the current request hop's context.
/// Recomputed locally by the receiving boundary from its own unforgeable transport
/// facts; never propagated. <see cref="Unestablished"/> is the fail-closed default — a
/// capability authority denies when the origin has not been positively established.
/// </summary>
public enum RequestOrigin
{
    /// <summary>No boundary positively established the origin (the scoped default).
    /// Fail-closed: capability authorities DENY rather than assume any plane.</summary>
    Unestablished = 0,

    /// <summary>The Edge HTTP inbound boundary produced this context from a validated
    /// cookie / edge-facing token — the external trust boundary, the start of the
    /// call-path.</summary>
    EdgeInbound = 1,

    /// <summary>A cross-process gRPC hop produced this context; the caller workload is
    /// authenticated by the validated mutual-TLS client certificate.</summary>
    CrossProcessHop = 2,

    /// <summary>An in-process module call (the generated I&lt;Module&gt;Api leaf) produced
    /// this context inside one host; the validated request-context was passed directly,
    /// no serialization.</summary>
    InProcessModule = 3,

    /// <summary>An in-host system worker (a background service with no inbound user
    /// request) produced this context under the host's own service identity.</summary>
    System = 4,
}
