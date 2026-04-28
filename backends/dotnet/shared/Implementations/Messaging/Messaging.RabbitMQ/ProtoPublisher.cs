// -----------------------------------------------------------------------
// <copyright file="ProtoPublisher.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Messaging.RabbitMQ;

using System.Collections.Concurrent;
using System.Text;
using global::RabbitMQ.Client;
using Google.Protobuf;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

/// <summary>
/// Publishes protobuf messages as JSON to RabbitMQ exchanges.
/// Lazily creates and caches a single channel, and tracks declared exchanges
/// to avoid per-publish overhead.
/// </summary>
[MustDisposeResource(false)]
public partial class ProtoPublisher : IAsyncDisposable
{
    private readonly IConnection r_connection;
    private readonly ILogger<ProtoPublisher> r_logger;
    private readonly ConcurrentDictionary<string, bool> r_declaredExchanges = new();
    private readonly SemaphoreSlim r_channelLock = new(1, 1);
    private IChannel? _channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtoPublisher"/> class.
    /// </summary>
    ///
    /// <param name="connection">
    /// The RabbitMQ connection.
    /// </param>
    /// <param name="logger">
    /// The logger.
    /// </param>
    [MustDisposeResource(false)]
    public ProtoPublisher(
        IConnection connection,
        ILogger<ProtoPublisher> logger)
    {
        r_connection = connection;
        r_logger = logger;
    }

    /// <summary>
    /// Publishes a protobuf message as JSON to the specified exchange.
    /// </summary>
    ///
    /// <typeparam name="T">
    /// The protobuf message type.
    /// </typeparam>
    /// <param name="exchange">
    /// The target exchange name.
    /// </param>
    /// <param name="message">
    /// The protobuf message to publish.
    /// </param>
    /// <param name="routingKey">
    /// The routing key (empty string for fanout exchanges).
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    ///
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    public virtual async Task PublishAsync<T>(
        string exchange,
        T message,
        string routingKey = "",
        CancellationToken ct = default)
        where T : IMessage<T>
    {
        var channel = await GetOrCreateChannelAsync(ct);
        await EnsureExchangeDeclaredAsync(channel, exchange, ct);

        var json = JsonFormatter.Default.Format(message);
        var body = Encoding.UTF8.GetBytes(json);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString("N"),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                ["x-proto-type"] = message.Descriptor.FullName,
            },
        };

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: ct);

        LogMessagePublished(r_logger, message.Descriptor.FullName, exchange);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            _channel.Dispose();
        }

        r_channelLock.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Logs that a protobuf message was published to an exchange.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Published {ProtoType} to exchange {Exchange}")]
    private static partial void LogMessagePublished(ILogger logger, string protoType, string exchange);

    [MustDisposeResource(false)]
    private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken ct)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await r_channelLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock.
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            _channel?.Dispose();
            _channel = await r_connection.CreateChannelAsync(cancellationToken: ct);
            r_declaredExchanges.Clear();
            return _channel;
        }
        finally
        {
            r_channelLock.Release();
        }
    }

    private async Task EnsureExchangeDeclaredAsync(
        IChannel channel,
        string exchange,
        CancellationToken ct)
    {
        if (r_declaredExchanges.ContainsKey(exchange))
        {
            return;
        }

        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false,
            cancellationToken: ct);

        r_declaredExchanges.TryAdd(exchange, true);
    }
}
