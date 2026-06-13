// -----------------------------------------------------------------------
// <copyright file="RabbitMqKeyRotationAnnouncer.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Infra.Messaging.RabbitMq;

using D2.Edge.KeyCustodian.Infra.Observability;
using D2.Shared.Messaging;
using Microsoft.Extensions.Logging;

/// <summary>
/// Publishes key-lifecycle changes over the message bus as
/// <c>KeyRotatedEvent</c>s so consumers refresh their keyrings. Implements the
/// App-layer <see cref="IKeyRotationAnnouncer"/> port.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fire-and-log.</b> The announce runs AFTER the durable transition commits.
/// A failed publish is NON-FATAL — consumers self-heal via keyring TTL refresh —
/// so this adapter NEVER throws or bubbles: a broker fault is logged and returned
/// as a failure <see cref="D2Result"/> the handler records but does not propagate.
/// The transition is already durable; rolling it back would be wrong.
/// </para>
/// <para>
/// The published payload carries public identifiers only (domain, kid, status,
/// urgent) — no key material, no compromise reason — so the message is plaintext
/// by design (the rotating key cannot encrypt the notification that triggers its
/// own rotation).
/// </para>
/// </remarks>
public sealed class RabbitMqKeyRotationAnnouncer(
    IMessageBus messageBus,
    ILogger<RabbitMqKeyRotationAnnouncer> logger)
    : IKeyRotationAnnouncer
{
    /// <inheritdoc/>
    public async ValueTask<D2Result> AnnounceAsync(
        KeyDomain domain,
        Kid kid,
        KeyStatus newStatus,
        bool urgent,
        CancellationToken cancellationToken = default)
    {
        var wireEvent = domain.ToKeyRotatedEvent(kid, newStatus, urgent);

        try
        {
            var result = await messageBus
                .PublishAsync(wireEvent, options: null, ct: cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
                KeyCustodianInfraLog.AnnouncePublishFailed(logger, domain.Value, kid.Value, urgent);

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fire-and-log: a broker/transport fault must not bubble out of the
            // post-commit announce. Surface it as a failure result the handler logs.
            KeyCustodianInfraLog.AnnounceThrew(
                logger,
                domain.Value,
                kid.Value,
                urgent,
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex));

            return D2Result.ServiceUnavailable();
        }
    }
}
