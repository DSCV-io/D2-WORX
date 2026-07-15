// -----------------------------------------------------------------------
// <copyright file="IHandlerDispatcher.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

using DcsvIo.D2.Result;

/// <summary>
/// Closed-over-generics dispatcher for one subscriber registration. Decodes
/// the AMQP body into the registration's message type and invokes the
/// handler resolved from the per-message DI scope. One instance per
/// <see cref="ISubscriberRegistration"/>; reused across deliveries (no
/// per-message reflection cost).
/// </summary>
internal interface IHandlerDispatcher
{
    /// <summary>
    /// Decodes <paramref name="body"/>, opens / populates the per-message
    /// scope, and invokes the handler. Returns the handler's
    /// <see cref="D2Result"/> (with <see cref="D2Result.Failed"/> set on
    /// any failure path: decrypt, deserialize, handler-failure, exception).
    /// </summary>
    /// <param name="scope">Per-message DI scope (caller owns disposal).</param>
    /// <param name="body">AMQP body bytes (encrypted or plaintext).</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<D2Result> DispatchAsync(
        IServiceProvider scope,
        ReadOnlyMemory<byte> body,
        CancellationToken ct);
}
