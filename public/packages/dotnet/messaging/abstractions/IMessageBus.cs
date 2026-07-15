// -----------------------------------------------------------------------
// <copyright file="IMessageBus.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

using DcsvIo.D2.Result;

/// <summary>
/// Publishes typed messages to D² conventional exchanges. Resolves the
/// destination exchange + routing key from the message type's
/// <see cref="MqPubAttribute"/> + the codegen'd <c>MqMessagesRegistry</c>,
/// encrypts the body when the descriptor's <c>encryption</c> field is a
/// non-<c>plaintext</c> domain, attaches the canonical AMQP headers, and
/// waits for the broker's publisher-confirm (when enabled).
/// </summary>
/// <remarks>
/// <para>
/// Implementations MUST be thread-safe — multiple handlers / threads
/// concurrently call <see cref="PublishAsync"/>.
/// </para>
/// <para>
/// The wire body is the serialized message — no envelope wrapper.
/// Cross-hop trace correlation rides in the W3C <c>traceparent</c> +
/// <c>tracestate</c> AMQP headers; small operational propagation context
/// (request id, fingerprints, WhoIs hash) rides in <c>x-d2-context</c>.
/// Any caller-identity / org / scope a consumer needs goes in the typed
/// message body — never in a generic context blob.
/// </para>
/// </remarks>
public interface IMessageBus
{
    /// <summary>
    /// Publishes <paramref name="message"/>. The message type's
    /// <see cref="MqPubAttribute"/> drives the descriptor lookup; the
    /// descriptor's <c>encryption</c> field decides plaintext vs
    /// per-domain AES-256-GCM.
    /// </summary>
    /// <typeparam name="TMessage">
    /// Proto-generated message type. Reflection on this type is cached per
    /// process — calls are amortized to near-zero overhead.
    /// </typeparam>
    /// <param name="message">The message to publish. Must not be null.</param>
    /// <param name="options">Per-call overrides; null for defaults.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>D2Result.Ok</c> on broker confirm (or fire-and-forget success when
    /// confirms disabled). <c>ServiceUnavailable</c> on broker timeout /
    /// connection failure (graceful — caller decides retry).
    /// <c>ValidationFailed</c> on null message.
    /// <c>Canceled</c> if <paramref name="ct"/> fires.
    /// </returns>
    /// <remarks>
    /// Configuration errors — message type missing <see cref="MqPubAttribute"/>,
    /// <see cref="MqPubAttribute.Constant"/> not in the codegen'd
    /// <c>MqMessagesRegistry</c>, or descriptor's encryption domain not
    /// registered via <c>AddD2EncryptionFor</c> — are programmer errors, not
    /// runtime conditions: they surface as
    /// <see cref="InvalidOperationException"/> from the resolver or
    /// encryption layer rather than a <c>D2Result</c> failure. Fix the
    /// registration / attribute and redeploy.
    /// </remarks>
    ValueTask<D2Result> PublishAsync<TMessage>(
        TMessage message,
        PublisherOptions? options = null,
        CancellationToken ct = default)
        where TMessage : class;

    /// <summary>
    /// Awaits the underlying transport being ready for publishing — for
    /// RabbitMQ, this is "first connection has landed and at least one
    /// channel is acquirable from the pool". Use from background hosted
    /// services that fire off a publish at startup (e.g. KeyCustodian
    /// rotation announcement) so a startup-race-with-broker doesn't
    /// surface as a confusing <c>ServiceUnavailable</c> on the first call.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the bus is ready, faults if
    /// the transport reports an unrecoverable connect failure, or
    /// cancels if <paramref name="ct"/> fires before readiness.</returns>
    Task WaitForReadyAsync(CancellationToken ct = default);
}
