// -----------------------------------------------------------------------
// <copyright file="DefaultTopologyDeclarer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Topology;

using System.Diagnostics;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;
using global::RabbitMQ.Client;
using Microsoft.Extensions.Logging;

/// <summary>
/// Idempotent topology declarer driven by the registered
/// <see cref="SubscriberRegistry"/>. Acquires a single dedicated channel,
/// declares everything, and closes it — declaration runs once per startup
/// and the channel is not reused by the publish / consume paths.
/// </summary>
internal sealed class DefaultTopologyDeclarer : ITopologyDeclarer
{
    private readonly ID2Connection r_connection;
    private readonly SubscriberRegistry r_registry;
    private readonly ILogger<DefaultTopologyDeclarer> r_logger;

    /// <summary>Initializes the declarer.</summary>
    /// <param name="connection">Connection wrapper.</param>
    /// <param name="registry">Subscriber registry to drive declarations.</param>
    /// <param name="logger">Logger.</param>
    public DefaultTopologyDeclarer(
        ID2Connection connection,
        SubscriberRegistry registry,
        ILogger<DefaultTopologyDeclarer> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(logger);
        r_connection = connection;
        r_registry = registry;
        r_logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask DeclareAsync(CancellationToken ct)
    {
        if (r_registry.All.Count == 0)
        {
            TopologyLog.NoSubscribersToDeclare(r_logger);
            return;
        }

        TopologyLog.DeclarationStarted(r_logger, r_registry.All.Count);
        var stopwatch = Stopwatch.StartNew();

        // Declaration is idempotent — safe to retry across reconnects, but the
        // hosted service runs us only once after first connection ready, so a
        // single channel per declaration cycle is enough.
        await using var channel = await r_connection.CreateChannelAsync(
            options: null, ct);

        foreach (var registration in r_registry.All)
        {
            try
            {
                await DeclareForSubscriberAsync(channel, registration, ct);
            }
            catch (Exception ex)
            {
                // Per §3.1: pass exception type name only, never the
                // exception itself — `OperationInterruptedException.Message`
                // from RabbitMQ.Client can include broker-side text such as
                // PRECONDITION_FAILED arg dumps that operators may have
                // configured (declaration arguments, queue settings, etc.).
                TopologyLog.DeclarationFailed(
                    r_logger, ex.GetType().Name, registration.ResolvedQueueName);
                throw;
            }
        }

        stopwatch.Stop();
        TopologyLog.DeclarationCompleted(r_logger, stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Queue durability / exclusivity / auto-delete flags per <see cref="QueuePattern"/>.
    /// Shared with consumer-channel re-declare for
    /// <see cref="QueuePattern.FanoutExclusiveAutoDelete"/> so flags cannot drift.
    /// </summary>
    /// <param name="pattern">The subscription queue pattern.</param>
    /// <returns>The declare flags for the primary queue.</returns>
    internal static (bool Durable, bool Exclusive, bool AutoDelete) QueueFlagsFor(
        QueuePattern pattern) => pattern switch
    {
        QueuePattern.CompetingConsumer => (Durable: true, Exclusive: false, AutoDelete: false),
        QueuePattern.DurableShared => (Durable: true, Exclusive: false, AutoDelete: false),
        QueuePattern.FanoutExclusiveAutoDelete =>
            (Durable: false, Exclusive: true, AutoDelete: true),
        _ => throw new ArgumentOutOfRangeException(nameof(pattern), pattern, null),
    };

    private static string ResolveExchange(Type messageType) =>
        MessageWireResolver.Resolve(messageType).Exchange;

    private static string ResolveExchangeType(Type messageType) =>
        MessageWireResolver.Resolve(messageType).ExchangeType;

    private async ValueTask DeclareForSubscriberAsync(
        IChannel channel,
        ISubscriberRegistration registration,
        CancellationToken ct)
    {
        var descriptor = registration.Descriptor;
        var queueName = registration.ResolvedQueueName;
        var exchange = ResolveExchange(registration.MessageType);
        var exchangeType = ResolveExchangeType(registration.MessageType);

        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: exchangeType,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        var dlxName = DlqNaming.DlxFor(queueName);
        var dlqName = DlqNaming.DlqFor(queueName);

        // DLX (fanout — routing key irrelevant) → DLQ.
        await channel.ExchangeDeclareAsync(
            exchange: dlxName,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await channel.QueueDeclareAsync(
            queue: dlqName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: dlqName,
            exchange: dlxName,
            routingKey: string.Empty,
            arguments: null,
            cancellationToken: ct);

        // Main queue with DLX argument.
        var queueArgs = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["x-dead-letter-exchange"] = dlxName,
            ["x-dead-letter-routing-key"] = string.Empty,
        };

        var (durable, exclusive, autoDelete) = QueueFlagsFor(descriptor.Pattern);
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: durable,
            exclusive: exclusive,
            autoDelete: autoDelete,
            arguments: queueArgs,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: exchange,
            routingKey: descriptor.RoutingKeyBinding,
            arguments: null,
            cancellationToken: ct);

        TopologyLog.SubscriberTopologyDeclared(
            r_logger,
            exchange,
            exchangeType,
            queueName,
            descriptor.Pattern.ToString(),
            dlxName,
            dlqName);

        if (descriptor.TieredRetry is { } retry)
            await DeclareTieredRetryAsync(channel, queueName, retry, ct);
    }

    private async ValueTask DeclareTieredRetryAsync(
        IChannel channel,
        string queueName,
        TieredRetryDescriptor retry,
        CancellationToken ct)
    {
        // Retry queues TTL-expire onto a "return" fanout exchange that's bound
        // back to the primary queue — putting the message back in front of the
        // handler for another attempt.
        var returnExchange = DlqNaming.RetryReturnExchangeFor(queueName);
        await channel.ExchangeDeclareAsync(
            exchange: returnExchange,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: returnExchange,
            routingKey: string.Empty,
            arguments: null,
            cancellationToken: ct);

        for (var i = 0; i < retry.Tiers.Length; i++)
        {
            var tierExchange = DlqNaming.RetryTierExchangeFor(queueName, i);
            var tierQueue = DlqNaming.RetryTierQueueFor(queueName, i);
            var ttlMs = (long)retry.Tiers[i].TotalMilliseconds;

            await channel.ExchangeDeclareAsync(
                exchange: tierExchange,
                type: ExchangeType.Fanout,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: ct);

            var tierArgs = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["x-message-ttl"] = ttlMs,
                ["x-dead-letter-exchange"] = returnExchange,
                ["x-dead-letter-routing-key"] = string.Empty,
            };

            await channel.QueueDeclareAsync(
                queue: tierQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: tierArgs,
                cancellationToken: ct);

            await channel.QueueBindAsync(
                queue: tierQueue,
                exchange: tierExchange,
                routingKey: string.Empty,
                arguments: null,
                cancellationToken: ct);
        }

        TopologyLog.RetryTopologyDeclared(
            r_logger,
            retry.Tiers.Length,
            queueName,
            string.Join(",", retry.Tiers));
    }
}
