// -----------------------------------------------------------------------
// <copyright file="TransientPublishClassifier.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Publishing;

using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Resilience.Retry;
using global::RabbitMQ.Client.Exceptions;

/// <summary>
/// Decides whether an exception thrown during a publish attempt is
/// transient (worth retrying) or terminal (caller-side bug, surface
/// immediately). Used by the publisher's built-in retry loop.
/// </summary>
internal static class TransientPublishClassifier
{
    /// <summary>True when <paramref name="ex"/> is worth retrying.</summary>
    /// <param name="ex">The exception.</param>
    public static bool IsTransient(Exception ex) => ex switch
    {
        // Connection wasn't open at acquire time — almost certainly comes
        // back within a retry cycle (or we surface ServiceUnavailable after
        // attempts exhaust).
        BrokerUnavailableException => true,

        // Channel-level: the channel went down mid-call. Pool will discard
        // it; next attempt creates a fresh one.
        AlreadyClosedException => true,

        // RabbitMQ.Client wraps various AMQP / network failures here.
        OperationInterruptedException => true,

        // Couldn't reach the broker at all.
        BrokerUnreachableException => true,

        // Pool acquire timed out — usually because every channel is stuck
        // waiting on a confirm during broker degradation. Worth retrying.
        TimeoutException => true,

        // M3: broker-NACK / confirm-failure during a publish attempt. We
        // EXCLUDE return-publish failures (mandatory:true → unroutable):
        // those are routing-config bugs, not transient broker conditions,
        // and retrying won't help. Everything else (queue full, mirror
        // sync in progress, broker restart in progress) IS worth a
        // backoff retry.
        PublishException pex when !pex.IsReturn => true,

        // Defer the rest to the standard classifier (HTTP / sockets / etc.).
        _ => RetryHelper.IsTransientException(ex),
    };
}
