// -----------------------------------------------------------------------
// <copyright file="RabbitMqMessageBus.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Publishing;

using System.Diagnostics;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Headers.Amqp;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Messaging.RabbitMq.Channels;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;
using DcsvIo.D2.Messaging.RabbitMq.Telemetry;
using DcsvIo.D2.Resilience.Retry;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Extensions;
using global::RabbitMQ.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Default <see cref="IMessageBus"/> implementation backed by
/// RabbitMQ.Client 7.x. Handles route resolution, body composition
/// (optional payload encryption), header construction, channel acquisition,
/// publisher confirms, and built-in transient-failure retry.
/// </summary>
/// <remarks>
/// <para>
/// Thread-safe: relies on <see cref="IChannelPool"/> (semaphore-bounded) for
/// concurrent channel access — multiple threads may call
/// <see cref="PublishAsync"/> simultaneously without external synchronization.
/// </para>
/// <para>
/// The wire body is just the serialized message — there is no envelope wrapper.
/// Encryption is about confidentiality of the message payload itself; cross-hop
/// trace correlation rides in the W3C <c>traceparent</c> + <c>tracestate</c>
/// AMQP headers. Caller-identity / org / scope fields a consumer needs go in
/// the typed message body.
/// </para>
/// </remarks>
internal sealed class RabbitMqMessageBus : IMessageBus
{
    private readonly IChannelPool r_channelPool;
    private readonly IServiceScopeFactory r_scopeFactory;
    private readonly ID2Connection r_connection;
    private readonly RabbitMqPublisherOptions r_options;
    private readonly ILogger<RabbitMqMessageBus> r_logger;

    /// <summary>Initializes the bus.</summary>
    /// <param name="channelPool">Bounded publisher channel pool.</param>
    /// <param name="scopeFactory">Per-publish DI scope factory — resolves
    /// per-domain <see cref="DcsvIo.D2.Encryption.IPayloadCrypto"/> via keyed
    /// services for encrypted message types AND
    /// <see cref="IRequestContext"/> for the propagated-context header. The
    /// bus is a Singleton (every publish builds a transient ~µs-cost scope)
    /// so background hosted services can publish without ceremony.</param>
    /// <param name="connection">Underlying broker connection — exposes
    /// <c>ReadyTask</c> for the <see cref="IMessageBus.WaitForReadyAsync"/>
    /// startup-sync helper.</param>
    /// <param name="options">Transport-level publisher defaults.</param>
    /// <param name="logger">Logger.</param>
    public RabbitMqMessageBus(
        IChannelPool channelPool,
        IServiceScopeFactory scopeFactory,
        ID2Connection connection,
        IOptions<RabbitMqPublisherOptions> options,
        ILogger<RabbitMqMessageBus> logger)
    {
        ArgumentNullException.ThrowIfNull(channelPool);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        r_channelPool = channelPool;
        r_scopeFactory = scopeFactory;
        r_connection = connection;
        r_options = options.Value;
        r_logger = logger;

        if (r_options.MaxAttempts < 1)
        {
            throw new InvalidOperationException(
                $"RabbitMqPublisherOptions.MaxAttempts must be >= 1; "
                + $"was {r_options.MaxAttempts}.");
        }
    }

    /// <inheritdoc />
    public async ValueTask<D2Result> PublishAsync<TMessage>(
        TMessage message,
        PublisherOptions? options = null,
        CancellationToken ct = default)
        where TMessage : class
    {
        // Defensive null check: callers ignoring nullable annotations should
        // get a structured ValidationFailed, not an NRE deep inside compose.
        if ((object?)message is null)
            return MessagingFailures.Required(nameof(message));

        // Resolve the message type's publisher contract from the codegen'd
        // registry via [MqPub]. Throws InvalidOperationException for missing
        // attribute / unknown constant / type-name mismatch — programmer
        // errors, not runtime conditions, so let them surface without retry.
        var descriptor = MessageWireResolver.Resolve(typeof(TMessage));
        var route = ResolveRoute(descriptor, options);

        // Build a transient scope per publish — bus is Singleton, so we need
        // a scope to (a) resolve the keyed IPayloadCrypto and (b) snapshot
        // any IRequestContext registered by the caller's scope. Cost is in
        // the microseconds; lets background hosted services publish without
        // creating their own scope each time.
        await using var scope = r_scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Compose body ONCE — body bytes are reused across retry attempts.
        var (body, kid) = EncryptedBodyComposer.Compose(message, descriptor, sp);

        // Stable per-publish message-id — generated ONCE so retry attempts
        // share it. Otherwise a retry of an already-broker-received-but-
        // unconfirmed publish would land in the consumer with a fresh id and
        // bypass the IMessageIdempotencyStore dedup window.
        var messageId = Guid.CreateVersion7().ToString();

        // Snapshot the propagated subset of IRequestContext (RequestId /
        // RequestPath / fingerprints / WhoIs hash) ONCE. Encoded as a single
        // base64url-of-JSON header value so the same shape works on every
        // transport. Identity (UserId / OrgId / Scopes) is NOT included —
        // it rebuilds at every hop from the JWT.
        var propagatedHeader = BuildPropagatedHeader(sp);

        var maxAttempts = options?.MaxAttempts ?? r_options.MaxAttempts;
        var waitForConfirm = options?.WaitForConfirm ?? r_options.WaitForConfirm;
        var confirmTimeout = options?.ConfirmTimeout ?? r_options.ConfirmTimeout;

        if (maxAttempts < 1)
        {
            return D2Result.ValidationFailed(messages: [TK.Common.Errors.UNKNOWN]);
        }

        using var activity = MessagingTelemetry.SR_ActivitySource.StartActivity(
            $"publish {route.Exchange}/{route.RoutingKey}",
            ActivityKind.Producer);
        activity?.SetTag(MessagingActivityTags.MESSAGING_SYSTEM, "rabbitmq");
        activity?.SetTag(MessagingActivityTags.MESSAGING_DESTINATION_NAME, route.Exchange);
        activity?.SetTag(MessagingActivityTags.MESSAGING_RABBITMQ_ROUTING_KEY, route.RoutingKey);
        activity?.SetTag(MessagingActivityTags.D2_MESSAGE_TYPE, typeof(TMessage).FullName);
        activity?.SetTag(MessagingActivityTags.D2_ENCRYPTION_KID, kid);
        activity?.SetTag(MessagingActivityTags.MESSAGING_MESSAGE_ID, messageId);
        activity?.SetTag(MessagingActivityTags.MESSAGING_OPERATION_TYPE, "publish");

        var stopwatch = Stopwatch.StartNew();
        var attemptsObserved = 0;

        try
        {
            await RetryHelper.RetryAsync(
                async (attempt, t) =>
                {
                    attemptsObserved = attempt;
                    MessagingTelemetry.SR_PublishesCounter.Add(1);

                    if (attempt > 1) MessagingTelemetry.SR_PublishRetriesCounter.Add(1);

                    MessagingLog.PublishAttempt(
                        r_logger,
                        route.Exchange,
                        route.RoutingKey,
                        typeof(TMessage).FullName ?? "<unknown>",
                        attempt,
                        maxAttempts,
                        kid);

                    await PublishOnceAsync(
                        route,
                        body,
                        kid,
                        messageId,
                        propagatedHeader,
                        typeof(TMessage),
                        waitForConfirm,
                        confirmTimeout,
                        t);
                    return true;
                },
                new RetryOptions<bool>
                {
                    MaxAttempts = maxAttempts,
                    BaseDelayMs = (int)r_options.BaseRetryDelay.TotalMilliseconds,
                    BackoffMultiplier = 2.0,
                    MaxDelayMs = (int)r_options.MaxRetryDelay.TotalMilliseconds,
                    IsTransient = ex =>
                    {
                        var transient = TransientPublishClassifier.IsTransient(ex);
                        if (transient && attemptsObserved < maxAttempts)
                        {
                            MessagingLog.PublishTransientFailure(
                                r_logger,
                                route.Exchange,
                                route.RoutingKey,
                                attemptsObserved,
                                maxAttempts,
                                ex.GetType().FullName ?? ex.GetType().Name);
                        }

                        return transient;
                    },
                },
                ct);

            stopwatch.Stop();
            MessagingTelemetry.SR_PublishDurationHistogram.Record(
                stopwatch.Elapsed.TotalMilliseconds);
            MessagingLog.PublishSucceeded(
                r_logger,
                route.Exchange,
                route.RoutingKey,
                typeof(TMessage).FullName ?? "<unknown>",
                attemptsObserved,
                stopwatch.Elapsed.TotalMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Ok);
            return D2Result.Ok();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, "canceled");
            return D2Result.Canceled();
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            MessagingTelemetry.SR_PublishFailuresCounter.Add(1);
            MessagingTelemetry.SR_PublishDurationHistogram.Record(
                stopwatch.Elapsed.TotalMilliseconds);
            MessagingLog.PublishTerminalFailure(
                r_logger,
                route.Exchange,
                route.RoutingKey,
                typeof(TMessage).FullName ?? "<unknown>",
                attemptsObserved,
                ex.GetType().FullName ?? ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            return D2Result.ServiceUnavailable();
        }
    }

    /// <inheritdoc />
    public Task WaitForReadyAsync(CancellationToken ct = default) =>
        r_connection.ReadyTask.WaitAsync(ct);

    private static ExchangeRoute ResolveRoute(
        MqMessageDescriptor descriptor, PublisherOptions? perCall)
    {
        var exchange = perCall?.Exchange.ToNullIfEmpty() ?? descriptor.Exchange;
        var routingKey = perCall?.RoutingKey.ToNullIfEmpty()
            ?? descriptor.DefaultRoutingKey
            ?? string.Empty;
        return new ExchangeRoute(exchange, routingKey);
    }

    private static string? BuildPropagatedHeader(IServiceProvider sp)
    {
        // Resolve from the per-publish scope. If no IRequestContext is
        // registered (e.g. a process with no request-pipeline at all),
        // nothing to send.
        var ctx = sp.GetService<IRequestContext>();
        if (ctx is null) return null;

        var propagated = ctx.ToPropagatedContext();
        return propagated.HasAnyField ? PropagatedContextSerializer.Encode(propagated) : null;
    }

    private async ValueTask PublishOnceAsync(
        ExchangeRoute route,
        byte[] body,
        string? kid,
        string messageId,
        string? propagatedHeader,
        Type messageType,
        bool waitForConfirm,
        TimeSpan confirmTimeout,
        CancellationToken ct)
    {
        await using var lease = await r_channelPool.AcquireAsync(ct);

        var props = new BasicProperties
        {
            ContentType = "application/octet-stream",
            MessageId = messageId,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [AmqpHeaders.PROTO_TYPE] = messageType.FullName,
            },
        };

        if (kid.Truthy()) props.Headers[AmqpHeaders.ENCRYPTION_KID] = kid;

        // Full W3C traceparent — not just trace-id — so the consumer can
        // start a child span whose parent is THIS publish span. Format:
        // {version}-{traceId}-{spanId}-{flags} per
        // https://www.w3.org/TR/trace-context.
        var current = Activity.Current;
        if (current is not null)
        {
            var flagsByte = (int)current.ActivityTraceFlags;
            props.Headers[AmqpHeaders.TRACEPARENT] =
                $"00-{current.TraceId}-{current.SpanId}-{flagsByte:x2}";
            var traceState = current.TraceStateString;
            if (traceState.Truthy())
                props.Headers[AmqpHeaders.TRACESTATE] = traceState;
        }

        if (propagatedHeader.Truthy())
            props.Headers[AmqpHeaders.PROPAGATED_CONTEXT] = propagatedHeader;

        if (!waitForConfirm)
        {
            await lease.Channel.BasicPublishAsync(
                exchange: route.Exchange,
                routingKey: route.RoutingKey,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct);
            return;
        }

        // BasicPublishAsync on a publisher-confirms-enabled channel returns
        // when the broker acks. Wrap with a linked CTS for the confirm timeout
        // so a slow / hung broker surfaces as a TimeoutException (transient).
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(confirmTimeout);
        try
        {
            await lease.Channel.BasicPublishAsync(
                exchange: route.Exchange,
                routingKey: route.RoutingKey,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: linked.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            MessagingLog.PublishConfirmTimeout(
                r_logger,
                confirmTimeout.TotalMilliseconds,
                route.Exchange,
                route.RoutingKey);
            throw new TimeoutException(
                $"Publish confirm timed out after "
                + $"{confirmTimeout.TotalSeconds:F1}s.");
        }
    }
}
