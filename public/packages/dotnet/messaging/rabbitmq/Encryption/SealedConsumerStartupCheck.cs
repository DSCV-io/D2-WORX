// -----------------------------------------------------------------------
// <copyright file="SealedConsumerStartupCheck.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Encryption;

using DcsvIo.D2.Encryption;
using DcsvIo.D2.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Fails host startup when a subscriber consumes a message on a SEALED
/// encryption domain but no keyed <see cref="IPayloadOpener"/> is registered for
/// that domain's consumer service — the consumer could never open a delivery, so
/// every message would DLQ. Registered UNCONDITIONALLY by
/// <c>AddD2MessagingRabbitMq</c> (never by the KeyCustodian sealing call it
/// guards): a net registered by the very call it protects could not catch the
/// FORGOTTEN call, which is the whole point.
/// </summary>
/// <remarks>
/// <para>
/// Per registration the resolution chain is
/// <c>SubscriberRegistry.All → ISubscriberRegistration.MessageType →
/// MessageWireResolver.Resolve → MqMessageDescriptor → IsSealed</c>; a sealed
/// descriptor demands a keyed <see cref="IPayloadOpener"/> for its
/// <see cref="MqMessageDescriptor.ConsumerService"/>. Presence is probed via
/// <see cref="IServiceProviderIsKeyedService"/> (registration-presence only — the
/// opener's own fail-loud boot fetch happens when the sealed self-check resolves
/// it, not here). The throw fires at host start, BEFORE any consumer channel
/// opens, naming the queue, handler, domain, consumer service, and the fix.
/// </para>
/// <para>
/// Production-inert today: no <c>mq-messages.spec.json</c> entry targets a sealed
/// domain, so no production subscriber can trip it. It is exercised only via
/// fixture message types pre-seeded through
/// <c>MessageWireResolver.RegisterForTesting</c> with descriptors on real sealed
/// domain values.
/// </para>
/// </remarks>
internal sealed class SealedConsumerStartupCheck : IHostedService
{
    private readonly SubscriberRegistry r_registry;
    private readonly IServiceProvider r_serviceProvider;

    /// <summary>Initializes the check.</summary>
    /// <param name="registry">Subscriber registry.</param>
    /// <param name="serviceProvider">
    /// Used to probe for a keyed <see cref="IPayloadOpener"/> registration.
    /// </param>
    public SealedConsumerStartupCheck(
        SubscriberRegistry registry, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        r_registry = registry;
        r_serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var isKeyed = r_serviceProvider.GetService<IServiceProviderIsKeyedService>();

        foreach (var registration in r_registry.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var descriptor = MessageWireResolver.Resolve(registration.MessageType);

            if (!descriptor.IsSealed)
                continue;

            // A sealed descriptor always carries a consumer service (the catalog
            // binds the two together); guard defensively regardless.
            var consumerService = descriptor.ConsumerService
                ?? throw new InvalidOperationException(
                    $"Subscriber '{registration.HandlerType.FullName}' (queue "
                    + $"'{registration.ResolvedQueueName}') consumes sealed domain "
                    + $"'{descriptor.Encryption}' but the generated catalog names no "
                    + "consumer service for it — the encryption-domain catalog is "
                    + "internally inconsistent.");

            var hasOpener = isKeyed?.IsKeyedService(typeof(IPayloadOpener), consumerService)
                ?? false;

            if (!hasOpener)
            {
                throw new InvalidOperationException(
                    $"Subscriber '{registration.HandlerType.FullName}' (queue "
                    + $"'{registration.ResolvedQueueName}') consumes messages on the sealed "
                    + $"encryption domain '{descriptor.Encryption}' (consumer service "
                    + $"'{consumerService}'), but no keyed IPayloadOpener is registered for "
                    + "that consumer service — every delivery would fail to open and DLQ. "
                    + $"Call services.AddD2SealedEncryptionViaKeyCustodian(\"{consumerService}\") "
                    + "on the consumer host so it can open its own sealed frames.");
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
