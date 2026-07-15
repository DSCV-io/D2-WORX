// -----------------------------------------------------------------------
// <copyright file="SubscriberChannel.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

using System.Diagnostics;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Headers.Amqp;
using DcsvIo.D2.Messaging.RabbitMq.Connection;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;
using DcsvIo.D2.Messaging.RabbitMq.Telemetry;
using DcsvIo.D2.Messaging.RabbitMq.Topology;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Diagnostics;
using DcsvIo.D2.Utilities.Extensions;
using global::RabbitMQ.Client;
using global::RabbitMQ.Client.Events;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Owns one dedicated <see cref="IChannel"/> + one
/// <see cref="AsyncEventingBasicConsumer"/> for a single subscriber. The
/// hosted service constructs one of these per registered subscriber. Each
/// channel has its own prefetch + ack window; channels are NOT shared
/// across subscribers (a slow handler must not stall another subscriber's
/// ack window).
/// </summary>
[MustDisposeResource(false)]
internal sealed class SubscriberChannel : IAsyncDisposable
{
    /// <summary>Hard cap on accepted message-id length. Our publisher emits
    /// a 36-char UUIDv7 string; an attacker controlling a queue we subscribe
    /// to could send a 64KB id and bloat the idempotency-store key. Reject
    /// anything longer than this — the consumer falls through to "no id"
    /// behavior (skips idempotency, still acks).</summary>
    private const int _MAX_MESSAGE_ID_LENGTH = 128;

    /// <summary>Bounded wait for in-flight callbacks to complete during
    /// disposal. After this timeout we close the channel anyway and
    /// accept that any handler still running may see a
    /// <see cref="ObjectDisposedException"/> on its next ack/nack.</summary>
    private const int _DISPOSE_DRAIN_TIMEOUT_MS = 30_000;

    private readonly ID2Connection r_connection;
    private readonly IServiceScopeFactory r_scopeFactory;
    private readonly HandlerDispatcherFactory r_dispatcherFactory;
    private readonly ISubscriberRegistration r_registration;
    private readonly ILogger<SubscriberChannel> r_logger;

    // Dedicated republish channel — must NOT share state with _channel,
    // because (a) consume + publish use different channel state machines
    // and (b) a republish failure shouldn't poison the consume channel's
    // delivery queue. Opened lazily on first DLQ republish; the
    // r_republishLock protects against concurrent failures racing to
    // create/replace the channel (without it, two simultaneous failures
    // would both observe the channel as null, both create one, and the
    // second assignment would silently leak the first).
    private readonly SemaphoreSlim r_republishLock = new(1, 1);
    private IChannel? _channel;
    private IChannel? _republishChannel;
    private AsyncEventingBasicConsumer? _consumer;
    private string? _consumerTag;
    private bool _disposed;

    // In-flight delivery callback counter — protects against the
    // disposal-races-with-delivery scenario where DisposeAsync would
    // close the channel mid-handler, surfacing as a confusing
    // ObjectDisposedException on the BasicAck/BasicNack call. Disposal
    // unsubscribes + cancels first, then bounded-spin-waits for this
    // to drain to zero.
    private int _inflightCallbacks;

    /// <summary>Initializes a subscriber channel.</summary>
    /// <param name="connection">Connection wrapper.</param>
    /// <param name="scopeFactory">Per-message DI scope factory.</param>
    /// <param name="dispatcherFactory">Closed-generic dispatchers per subscriber.</param>
    /// <param name="registration">The subscriber to host.</param>
    /// <param name="logger">Logger.</param>
    [MustDisposeResource(false)]
    public SubscriberChannel(
        ID2Connection connection,
        IServiceScopeFactory scopeFactory,
        HandlerDispatcherFactory dispatcherFactory,
        ISubscriberRegistration registration,
        ILogger<SubscriberChannel> logger)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(dispatcherFactory);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(logger);
        r_connection = connection;
        r_scopeFactory = scopeFactory;
        r_dispatcherFactory = dispatcherFactory;
        r_registration = registration;
        r_logger = logger;
    }

    /// <summary>Gets or sets the test-only seam that, when non-null,
    /// replaces the real <c>BasicAckAsync</c> call with the supplied
    /// delegate so the M2 narrow-catch test can deterministically inject
    /// an ack failure. Production code MUST NOT set this.</summary>
    internal static Func<IChannel, ulong, Task>? AckHookForTesting { get; set; }

    /// <summary>Gets the in-flight delivery callback count via volatile
    /// read of <c>_inflightCallbacks</c> — exposed for in-flight-drain count
    /// assertions in integration tests.</summary>
    internal int InflightCallbackCountForTesting =>
        Volatile.Read(ref _inflightCallbacks);

    /// <summary>Opens a channel and starts consuming.</summary>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask StartAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var descriptor = r_registration.Descriptor;
        var queueName = r_registration.ResolvedQueueName;
        var prefetch = (ushort)Math.Min(ushort.MaxValue, descriptor.Prefetch);

        _channel = await r_connection.CreateChannelAsync(options: null, ct);

        try
        {
            await _channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: prefetch,
                global: false,
                cancellationToken: ct);

            // FanoutExclusiveAutoDelete: ensure queue+bind on the long-lived consumer channel
            // before BasicConsume. Topology declare uses a short-lived channel; under load a
            // NOT_FOUND window can still appear between declare and consume. Args must match
            // DefaultTopologyDeclarer (via QueueFlagsFor + DLX args). Exclusive queues are
            // deleted when their declaring connection closes — not merely when a declare
            // channel is disposed — so this re-declare is about consume-channel readiness
            // and arg parity, not "channel dispose deleted the queue."
            if (descriptor.Pattern == QueuePattern.FanoutExclusiveAutoDelete)
                await EnsureExclusiveQueueOnConsumerChannelAsync(_channel, queueName, ct);

            _consumer = new AsyncEventingBasicConsumer(_channel);
            _consumer.ReceivedAsync += OnReceivedAsync;

            _consumerTag = await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumerTag: string.Empty,
                noLocal: false,
                exclusive: false,
                arguments: null,
                consumer: _consumer,
                cancellationToken: ct);
        }
        catch
        {
            // Hosted service only tracks successfully started channels. If QoS /
            // exclusive re-declare / BasicConsume throws after assignment, dispose the
            // orphan channel so we do not leak a broker channel with no owner.
            var orphan = _channel;
            _channel = null;
            _consumer = null;
            _consumerTag = null;

            if (orphan is not null)
            {
                try
                {
                    await orphan.DisposeAsync();
                }
                catch
                {
                    // Best-effort — we are already failing StartAsync.
                }
            }

            throw;
        }

        SubscriberLog.ConsumerStarted(
            r_logger,
            queueName,
            prefetch,
            descriptor.Pattern.ToString(),
            r_registration.HandlerType.Name);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _disposed = true;

        SubscriberLog.ConsumerStopping(r_logger, r_registration.ResolvedQueueName);

        if (_consumer is not null)
            _consumer.ReceivedAsync -= OnReceivedAsync;

        if (_channel is not null)
        {
            try
            {
                if (_consumerTag.Truthy())
                {
                    await _channel.BasicCancelAsync(
                        _consumerTag!,
                        noWait: false,
                        cancellationToken: default);
                }
            }
            catch
            {
                // Best-effort during shutdown — broker may already be gone.
            }

            // Drain in-flight callbacks before closing the channel. Bounded
            // by _DISPOSE_DRAIN_TIMEOUT_MS — slow handlers don't get to
            // hold the host shutdown indefinitely, but well-behaved ones
            // get a chance to ack their message cleanly.
            await DrainInflightCallbacksAsync();

            try
            {
                if (_channel.IsOpen) await _channel.CloseAsync();
            }
            catch
            {
                // Best-effort.
            }

            await _channel.DisposeAsync();
        }

        if (_republishChannel is not null)
        {
            try
            {
                if (_republishChannel.IsOpen) await _republishChannel.CloseAsync();
            }
            catch
            {
                // Best-effort.
            }

            await _republishChannel.DisposeAsync();
        }

        r_republishLock.Dispose();
    }

    /// <summary>Counts redelivery attempts via the broker's <c>x-death</c>
    /// header. RabbitMQ pushes one entry per (queue, reason) pair the message
    /// has cycled through; the <c>count</c> field on an entry increments each
    /// additional cycle. We sum <c>count</c> across entries whose
    /// <c>reason</c> reflects a retry-cycle event:
    /// <list type="bullet">
    ///   <item><c>expired</c> — retry-tier TTL expiry (our retry path).</item>
    ///   <item><c>rejected</c> — consumer NACK without requeue.</item>
    /// </list>
    /// Other reasons (<c>maxlen</c>, <c>delivery_limit</c>) are broker-side
    /// flow control, not consumer-side retries — counting them would
    /// trigger <c>RETRIES_EXHAUSTED</c> prematurely. Returns zero on first
    /// delivery / malformed header / missing field — fail-open so a broker
    /// quirk doesn't strand a message in retry forever.</summary>
    /// <param name="ea">The delivery event args carrying broker headers.</param>
    internal static int ReadAttemptCount(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties.Headers is null) return 0;
        if (!ea.BasicProperties.Headers.TryGetValue("x-death", out var raw)) return 0;
        if (raw is not IList<object?> entries) return 0;

        var total = 0;
        foreach (var entry in entries)
        {
            if (entry is not IDictionary<string, object?> dict) continue;

            // Filter on reason — only retry-cycle events count.
            if (!dict.TryGetValue("reason", out var reasonObj)) continue;
            var reason = reasonObj switch
            {
                string s => s,
                byte[] bytes => System.Text.Encoding.UTF8.GetString(bytes),
                _ => null,
            };
            if (reason is not ("expired" or "rejected")) continue;

            if (!dict.TryGetValue("count", out var countObj)) continue;

            // x-death "count" is broker-emitted as long.
            var count = countObj switch
            {
                long l => (int)l,
                int i => i,
                _ => 0,
            };
            total += count;
        }

        return total;
    }

    /// <summary>Internal access for republish-channel race-coverage tests.
    /// Production code MUST go through the failure path which calls this lazily.</summary>
    [MustDisposeResource(false)]
    internal async ValueTask<IChannel> EnsureRepublishChannelAsync()
    {
        // Fast path: open channel exists, no contention.
        if (_republishChannel is { IsOpen: true } existing)
            return existing;

        // Slow path: serialize the create-or-replace work so two concurrent
        // failure callers don't both create channels and silently leak one.
        await r_republishLock.WaitAsync();
        try
        {
            // Re-check inside the lock (another caller may have just created it).
            if (_republishChannel is { IsOpen: true } now)
                return now;

            // Discard any closed channel so we don't leak file descriptors on
            // a broker restart.
            if (_republishChannel is { IsOpen: false } stale)
            {
                try
                {
                    await stale.DisposeAsync();
                }
                catch
                {
                    // Best-effort — channel was already closed.
                }

                _republishChannel = null;
            }

            _republishChannel = await r_connection.CreateChannelAsync(options: null);
            return _republishChannel;
        }
        finally
        {
            r_republishLock.Release();
        }
    }

    private static string? ReadMessageId(BasicDeliverEventArgs ea)
    {
        var props = ea.BasicProperties;
        var raw = ReadFromPropertiesOrHeader(props);
        if (raw is null) return null;

        // Reject pathological lengths (key-injection / Redis-memory DoS
        // protection) and any control characters that could split the
        // idempotency store's namespace via ":" or whitespace.
        if (raw.Length > _MAX_MESSAGE_ID_LENGTH) return null;
        foreach (var ch in raw)
        {
            if (char.IsControl(ch) || ch == ':' || ch == ' ')
                return null;
        }

        return raw;
    }

    private static string? ReadFromPropertiesOrHeader(IReadOnlyBasicProperties props)
    {
        if (!props.MessageId.Falsey()) return props.MessageId;

        if (props.Headers is not null
            && props.Headers.TryGetValue(AmqpHeaders.MESSAGE_ID, out var raw)
            && raw is byte[] bytes)
        {
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        return null;
    }

    private static Dictionary<string, object?> BuildRepublishHeaders(
        IReadOnlyBasicProperties sourceProps, byte[] failureHeader)
    {
        var headers = new Dictionary<string, object?>(StringComparer.Ordinal);

        // Carry forward producer-set headers (traceparent, x-d2-context,
        // etc.) so DLQ inspection sees the same context as a normal delivery.
        if (sourceProps.Headers is not null)
        {
            foreach (var kvp in sourceProps.Headers)
                headers[kvp.Key] = kvp.Value;
        }

        // Overwrite (or set) the failure-reason header — this is the whole
        // point of the republish path.
        headers[AmqpHeaders.FAILURE_REASON] = failureHeader;
        return headers;
    }

    private static PropagatedContext? ReadPropagatedContext(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties.Headers is null) return null;
        if (!ea.BasicProperties.Headers.TryGetValue(AmqpHeaders.PROPAGATED_CONTEXT, out var raw)
            || raw is not byte[] bytes)
        {
            return null;
        }

        var encoded = System.Text.Encoding.UTF8.GetString(bytes);
        return PropagatedContextSerializer.TryDecode(encoded);
    }

    private static ActivityContext ReadTraceContext(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties.Headers is null) return default;

        string? traceparent = null;
        string? tracestate = null;
        if (ea.BasicProperties.Headers.TryGetValue(AmqpHeaders.TRACEPARENT, out var tpRaw)
            && tpRaw is byte[] tpBytes)
        {
            traceparent = System.Text.Encoding.UTF8.GetString(tpBytes);
        }

        if (ea.BasicProperties.Headers.TryGetValue(AmqpHeaders.TRACESTATE, out var tsRaw)
            && tsRaw is byte[] tsBytes)
        {
            tracestate = System.Text.Encoding.UTF8.GetString(tsBytes);
        }

        if (traceparent is null) return default;

        // ActivityContext.TryParse handles full traceparent string format
        // (00-{traceId}-{spanId}-{flags}). Failure (malformed header) →
        // we silently drop the parent context — the consume span starts a
        // new trace rather than crashing on a forged header.
        return ActivityContext.TryParse(traceparent, tracestate, out var ctx)
            ? ctx
            : default;
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs ea)
    {
        // Track in-flight callback count so DisposeAsync can drain in-flight
        // handlers before closing the channel. Increment BEFORE the work,
        // decrement in finally so any throw still releases the slot.
        Interlocked.Increment(ref _inflightCallbacks);
        try
        {
            await OnReceivedCoreAsync(ea);
        }
        finally
        {
            Interlocked.Decrement(ref _inflightCallbacks);
        }
    }

    private async Task OnReceivedCoreAsync(BasicDeliverEventArgs ea)
    {
        var queue = r_registration.ResolvedQueueName;
        var deliveryTag = ea.DeliveryTag;
        var stopwatch = Stopwatch.StartNew();
        var messageId = ReadMessageId(ea);

        // Start a Consumer-kind span linked to the producer's publish span
        // via traceparent. Activity.Current is set inside the using block,
        // so SubscriberLog + handler dispatch + DLQ failure-header all see
        // the consume span as their ambient activity.
        var parentContext = ReadTraceContext(ea);
        var activityName = $"receive {queue}";
        using var activity = MessagingTelemetry.SR_ActivitySource.StartActivity(
            activityName,
            kind: ActivityKind.Consumer,
            parentContext: parentContext);

        if (activity is not null)
        {
            activity.SetTag(MessagingActivityTags.MESSAGING_SYSTEM, "rabbitmq");
            activity.SetTag(MessagingActivityTags.MESSAGING_DESTINATION_NAME, queue);

            // OTel sem-conv canonical attribute is "messaging.operation.type"
            // (NOT "messaging.operation"). Spec-driving structurally prevents
            // drift from the standard.
            activity.SetTag(MessagingActivityTags.MESSAGING_OPERATION_TYPE, "receive");
            activity.SetTag(MessagingActivityTags.MESSAGING_MESSAGE_ID, messageId);
            activity.SetTag(MessagingActivityTags.MESSAGING_RABBITMQ_DELIVERY_TAG, deliveryTag);
            activity.SetTag(MessagingActivityTags.MESSAGING_RABBITMQ_REDELIVERED, ea.Redelivered);
        }

        SubscriberLog.DeliveryReceived(
            r_logger, queue, deliveryTag, ea.Redelivered, messageId);

        try
        {
            await using var scope = r_scopeFactory.CreateAsyncScope();

            // Apply the producer-side propagated context (RequestId,
            // RequestPath, fingerprints, WhoIs hash) onto the per-message
            // scope's MutableRequestContext BEFORE the handler resolves its
            // IRequestContext through HandlerContext. Identity (UserId /
            // OrgId / Scopes) is NOT in this header — it would rebuild from
            // a JWT in a sync hop; for async events the consumer-side handler
            // doesn't have one and shouldn't claim caller identity.
            var propagated = ReadPropagatedContext(ea);
            if (propagated is not null)
            {
                var mutableCtx = scope.ServiceProvider.GetService<MutableRequestContext>();
                mutableCtx?.ApplyPropagatedContext(propagated);
            }

            // Optional idempotency pre-check — opt-in per subscriber.
            if (r_registration.Descriptor.Idempotency && messageId.Truthy())
            {
                var store = scope.ServiceProvider
                    .GetService<IMessageIdempotencyStore>();
                if (store is not null)
                {
                    var seen = await store.HasSeenAsync(messageId!);
                    if (seen is { Failed: false, Data: true })
                    {
                        // Already processed — ack without re-doing the work.
                        await _channel!.BasicAckAsync(deliveryTag, multiple: false);
                        SubscriberLog.DeliverySkipped(
                            r_logger, queue, deliveryTag, messageId);
                        return;
                    }

                    // ServiceUnavailable on the store: fail-open (process the
                    // message; better a duplicate than reject during a Redis
                    // blip — handlers are required to be at-least-once-safe
                    // anyway).
                }
            }

            // Tiered-retry attempt-count enforcement (M1). When the
            // subscription declares a TieredRetry block and the broker's
            // x-death history says we've already burned the budget, route
            // straight to DLQ with cause=RETRIES_EXHAUSTED so we don't
            // pile up another tier-bounce cycle for a permanently broken
            // payload.
            if (r_registration.Descriptor.TieredRetry is { } retry)
            {
                var attemptCount = ReadAttemptCount(ea);
                if (attemptCount >= retry.MaxAttempts)
                {
                    await NackRetriesExhaustedAsync(ea, attemptCount, messageId);
                    SubscriberLog.RetriesExhausted(
                        r_logger, queue, messageId, attemptCount, retry.MaxAttempts);
                    return;
                }
            }

            var dispatcher = r_dispatcherFactory.GetForQueue(queue);
            D2Result result;
            try
            {
                result = await dispatcher.DispatchAsync(scope.ServiceProvider, ea.Body, default);
            }
            catch (MessageBodyDecodeException dex)
            {
                var cause = dex.InnerException is System.Text.Json.JsonException
                    ? DlqFailureCauses.DESERIALIZE_FAILURE
                    : DlqFailureCauses.DECRYPT_FAILURE;
                var rootEx = dex.InnerException ?? dex;
                await NackToDlqAsync(ea, cause, rootEx, messageId);
                SubscriberLog.BoundaryFailure(
                    r_logger,
                    queue,
                    cause,
                    messageId,
                    SanitizedExceptionRender.TypeName(rootEx),
                    SanitizedExceptionRender.FirstFrame(rootEx));
                return;
            }
            catch (Exception ex)
            {
                await NackHandlerExceptionAsync(ea, ex, messageId);
                SubscriberLog.HandlerThrew(
                    r_logger,
                    queue,
                    messageId,
                    SanitizedExceptionRender.TypeName(ex),
                    SanitizedExceptionRender.FirstFrame(ex));
                return;
            }

            if (result.Failed)
            {
                await NackHandlerResultAsync(ea, result, messageId);
                SubscriberLog.HandlerResultFailure(r_logger, queue, result.ErrorCode);
                return;
            }

            // Success — record idempotency mark first so a crash AFTER ack
            // before mark causes a (safe) redelivery rather than a duplicate
            // that the handler must defend against itself.
            if (r_registration.Descriptor.Idempotency && messageId.Truthy())
            {
                var store = scope.ServiceProvider
                    .GetService<IMessageIdempotencyStore>();
                if (store is not null)
                {
                    var markResult = await store.MarkSeenAsync(messageId!);
                    if (markResult.Failed)
                    {
                        // Mark failed (Redis blip / store outage). Acking now
                        // would silently leave the dedup window unguarded for
                        // this message — a redelivery would re-run the
                        // handler. NACK-no-requeue routes to DLQ via
                        // x-dead-letter-exchange so an operator sees the
                        // store-degradation impact instead of duplicates
                        // being processed silently.
                        MessagingTelemetry.SR_AckFailuresCounter.Add(1);
                        SubscriberLog.IdempotencyMarkFailed(
                            r_logger,
                            queue,
                            deliveryTag,
                            messageId,
                            markResult.ErrorCode);
                        await _channel!.BasicNackAsync(
                            deliveryTag, multiple: false, requeue: false);
                        return;
                    }
                }
            }

            // M2: ack failure is NOT a handler failure — narrow the catch
            // to the BasicAckAsync call so a broker-side ack rejection
            // (channel closed mid-flight, etc.) doesn't get reported as
            // HANDLER_EXCEPTION and falsely route the (already-processed)
            // message to DLQ. Without requeue: broker's at-least-once
            // semantics will redeliver on reconnect; the idempotency mark
            // (already written above) keeps the duplicate from re-running
            // the handler.
            try
            {
                if (AckHookForTesting is { } hook)
                    await hook(_channel!, deliveryTag);
                else
                    await _channel!.BasicAckAsync(deliveryTag, multiple: false);
            }
            catch (Exception ackEx)
            {
                MessagingTelemetry.SR_AckFailuresCounter.Add(1);
                SubscriberLog.AckFailed(
                    r_logger,
                    queue,
                    deliveryTag,
                    messageId,
                    SanitizedExceptionRender.TypeName(ackEx),
                    SanitizedExceptionRender.FirstFrame(ackEx));
                return;
            }

            stopwatch.Stop();
            SubscriberLog.DeliveryAcked(
                r_logger, queue, deliveryTag, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            // Defensive — anything else surfaces as a NACK; the message
            // either redelivers (broker's job) or hits its DLQ if requeue
            // limit reached.
            await NackHandlerExceptionAsync(ea, ex, messageId);
            SubscriberLog.BoundaryFailure(
                r_logger,
                queue,
                "UNEXPECTED",
                messageId,
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex));
        }
    }

    private async Task NackToDlqAsync(
        BasicDeliverEventArgs ea, string cause, Exception ex, string? messageId)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var headerBytes = DlqFailureHeaderBuilder.FromBoundary(cause, ex, traceId);
        await PublishFailureHeaderAsync(ea, headerBytes, messageId);
    }

    private async Task NackHandlerExceptionAsync(
        BasicDeliverEventArgs ea, Exception ex, string? messageId)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var headerBytes = DlqFailureHeaderBuilder.FromException(ex, traceId: traceId);
        await PublishFailureHeaderAsync(ea, headerBytes, messageId);
    }

    private async Task NackHandlerResultAsync(
        BasicDeliverEventArgs ea, D2Result result, string? messageId)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var headerBytes = DlqFailureHeaderBuilder.FromResult(result, traceId: traceId);
        await PublishFailureHeaderAsync(ea, headerBytes, messageId);
    }

    private async Task NackRetriesExhaustedAsync(
        BasicDeliverEventArgs ea, int attemptCount, string? messageId)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var headerBytes = DlqFailureHeaderBuilder.FromRetriesExhausted(
            attemptCount, traceId: traceId);
        await PublishFailureHeaderAsync(ea, headerBytes, messageId);
    }

    private async Task PublishFailureHeaderAsync(
        BasicDeliverEventArgs ea, byte[] failureHeader, string? messageId)
    {
        // RabbitMQ's BasicNack doesn't let you attach headers to the
        // dead-lettered message directly. We republish the same body to the
        // queue's DLX with the failure header attached, then BasicAck the
        // original delivery — that way the broker's
        // x-dead-letter-exchange routing isn't ALSO triggered (which
        // would land a header-less duplicate alongside).
        var queue = r_registration.ResolvedQueueName;
        var dlxName = DlqNaming.DlxFor(queue);
        var deliveryTag = ea.DeliveryTag;

        try
        {
            var republishChannel = await EnsureRepublishChannelAsync();
            var headers = BuildRepublishHeaders(ea.BasicProperties, failureHeader);
            var props = new BasicProperties
            {
                ContentType = ea.BasicProperties.ContentType,
                ContentEncoding = ea.BasicProperties.ContentEncoding,
                MessageId = ea.BasicProperties.MessageId,
                CorrelationId = ea.BasicProperties.CorrelationId,
                DeliveryMode = DeliveryModes.Persistent,
                Headers = headers,
            };

            await republishChannel.BasicPublishAsync(
                exchange: dlxName,
                routingKey: string.Empty,
                mandatory: false,
                basicProperties: props,
                body: ea.Body);

            await _channel!.BasicAckAsync(deliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            // Republish path failed — fall back to BasicNack-no-requeue so
            // the broker's x-dead-letter-exchange routes the message
            // (header-less, but still in the DLQ — better than losing it).
            MessagingTelemetry.SR_DlqRepublishFailuresCounter.Add(1);
            SubscriberLog.DlqRepublishFailed(
                r_logger,
                queue,
                dlxName,
                deliveryTag,
                messageId,
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex));
            await _channel!.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
        }
    }

    private async Task DrainInflightCallbacksAsync()
    {
        // Spin-wait with bounded timeout. Channel + consumer pumps are
        // cooperative — once BasicCancel returns, the broker stops
        // pushing new deliveries; we just need to wait for already-pushed
        // ones to finish their handler dispatch.
        var deadline = Environment.TickCount64 + _DISPOSE_DRAIN_TIMEOUT_MS;
        while (Volatile.Read(ref _inflightCallbacks) > 0)
        {
            if (Environment.TickCount64 >= deadline)
            {
                SubscriberLog.DisposeDrainTimeout(
                    r_logger,
                    r_registration.ResolvedQueueName,
                    Volatile.Read(ref _inflightCallbacks),
                    _DISPOSE_DRAIN_TIMEOUT_MS);
                return;
            }

            await Task.Delay(50);
        }
    }

    /// <summary>
    /// Idempotent exclusive-queue declare+bind on the consumer channel. Declare order and
    /// flags match <see cref="DefaultTopologyDeclarer"/> for
    /// <see cref="QueuePattern.FanoutExclusiveAutoDelete"/> (DLX before main queue; flags via
    /// <see cref="DefaultTopologyDeclarer.QueueFlagsFor"/>).
    /// </summary>
    private async ValueTask EnsureExclusiveQueueOnConsumerChannelAsync(
        IChannel channel, string queueName, CancellationToken ct)
    {
        var descriptor = r_registration.Descriptor;
        var wire = MessageWireResolver.Resolve(r_registration.MessageType);
        var dlxName = DlqNaming.DlxFor(queueName);
        var dlqName = DlqNaming.DlqFor(queueName);

        await channel.ExchangeDeclareAsync(
            exchange: wire.Exchange,
            type: wire.ExchangeType,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: ct);

        // DLX before main queue so x-dead-letter-exchange arg always references a declared
        // exchange (same order as DefaultTopologyDeclarer).
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

        var queueArgs = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["x-dead-letter-exchange"] = dlxName,
            ["x-dead-letter-routing-key"] = string.Empty,
        };

        var (durable, exclusive, autoDelete) =
            DefaultTopologyDeclarer.QueueFlagsFor(QueuePattern.FanoutExclusiveAutoDelete);

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: durable,
            exclusive: exclusive,
            autoDelete: autoDelete,
            arguments: queueArgs,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: wire.Exchange,
            routingKey: descriptor.RoutingKeyBinding,
            arguments: null,
            cancellationToken: ct);
    }
}
