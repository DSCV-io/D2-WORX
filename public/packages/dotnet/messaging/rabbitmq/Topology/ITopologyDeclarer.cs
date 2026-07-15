// -----------------------------------------------------------------------
// <copyright file="ITopologyDeclarer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Topology;

/// <summary>
/// Idempotently declares every exchange / queue / DLX binding required by
/// the registered subscribers. Invoked once at startup by the topology
/// hosted service after the connection is ready; safe to invoke repeatedly
/// (RabbitMQ's <c>queueDeclare</c> / <c>exchangeDeclare</c> are no-ops if
/// the topology already exists).
/// </summary>
internal interface ITopologyDeclarer
{
    /// <summary>Declares all topology required by the subscriber registry.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when declaration finishes.</returns>
    ValueTask DeclareAsync(CancellationToken ct);
}
