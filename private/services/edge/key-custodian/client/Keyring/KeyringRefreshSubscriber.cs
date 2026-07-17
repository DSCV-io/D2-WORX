// -----------------------------------------------------------------------
// <copyright file="KeyringRefreshSubscriber.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;

using DcsvIo.D2.Handler;
using DcsvIo.D2.Messaging;
using I = DcsvIo.D2.Auth.Events.KeyRotatedEvent;
using O = DcsvIo.D2.Result.Unit;

/// <summary>
/// Messaging entry point for keyring rotation. Consumes the <c>KeyRotatedEvent</c> fanout
/// (queue <c>keyring-refresher</c>) and fans each event out to the
/// <see cref="RabbitMqRotationEventChannel"/> callbacks whose domain matches the event's
/// domain. Non-matching domains are a no-op.
/// </summary>
/// <remarks>
/// The refetch + hot-swap (and its bounded, keep-serving-current failure handling) lives
/// in each holder's callback (<see cref="KeyringBackedPayloadCrypto"/>), so this
/// subscriber always acks after dispatching: the KeyringRefresh queue dead-letters (never
/// requeues-to-source) on a handler-result failure, so a fail-and-redeliver strategy would
/// dead-letter rotation events rather than re-drive the refetch. Consumer isolation (a
/// throwing callback does not stop siblings) is provided by the channel.
/// </remarks>
[MqSub(MqSubscriptions.KeyringRefresh)]
public sealed class KeyringRefreshSubscriber(
    HandlerContext<KeyringRefreshSubscriber> ctx,
    RabbitMqRotationEventChannel channel)
    : BaseHandler<KeyringRefreshSubscriber, I, O>(ctx)
{
    /// <inheritdoc />
    // O is Unit (a value type), so the base's D2Result<TOutput?> resolves to
    // D2Result<Unit> — no nullable annotation on the alias.
    protected override async ValueTask<D2Result<O>> ExecuteAsync(I input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        await channel.DispatchAsync(input.Domain, ct).ConfigureAwait(false);

        return D2Result<O>.Ok();
    }
}
