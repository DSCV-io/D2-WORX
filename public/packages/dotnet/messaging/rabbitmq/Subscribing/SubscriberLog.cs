// -----------------------------------------------------------------------
// <copyright file="SubscriberLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

using Microsoft.Extensions.Logging;

/// <summary><c>LoggerMessage</c> delegates for the consumer path.</summary>
internal static partial class SubscriberLog
{
    [LoggerMessage(
        EventId = 300,
        Level = LogLevel.Information,
        Message = "Consumer started for queue={Queue} (prefetch={Prefetch}, "
            + "pattern={Pattern}, handler={Handler}).")]
    public static partial void ConsumerStarted(
        ILogger logger,
        string queue,
        ushort prefetch,
        string pattern,
        string handler);

    [LoggerMessage(
        EventId = 301,
        Level = LogLevel.Information,
        Message = "Consumer stopping for queue={Queue}.")]
    public static partial void ConsumerStopping(ILogger logger, string queue);

    [LoggerMessage(
        EventId = 302,
        Level = LogLevel.Debug,
        Message = "Delivery received on queue={Queue} (deliveryTag={DeliveryTag}, "
            + "redelivered={Redelivered}, msgId={MessageId}).")]
    public static partial void DeliveryReceived(
        ILogger logger,
        string queue,
        ulong deliveryTag,
        bool redelivered,
        string? messageId);

    [LoggerMessage(
        EventId = 303,
        Level = LogLevel.Debug,
        Message = "Delivery acked on queue={Queue} (deliveryTag={DeliveryTag}, "
            + "durationMs={DurationMs:F0}).")]
    public static partial void DeliveryAcked(
        ILogger logger, string queue, ulong deliveryTag, double durationMs);

    [LoggerMessage(
        EventId = 304,
        Level = LogLevel.Debug,
        Message = "Delivery skipped (idempotency hit) on queue={Queue} "
            + "(deliveryTag={DeliveryTag}, msgId={MessageId}).")]
    public static partial void DeliverySkipped(
        ILogger logger, string queue, ulong deliveryTag, string? messageId);

    [LoggerMessage(
        EventId = 305,
        Level = LogLevel.Warning,
        Message = "Handler returned a non-Ok result on queue={Queue} "
            + "(errorCode={ErrorCode}); routing to DLQ.")]
    public static partial void HandlerResultFailure(
        ILogger logger, string queue, string? errorCode);

    [LoggerMessage(
        EventId = 306,
        Level = LogLevel.Error,
        Message = "Handler threw on queue={Queue} (msgId={MessageId}, "
            + "exType={ExType}, where={Where}); routing to DLQ.")]
    public static partial void HandlerThrew(
        ILogger logger,
        string queue,
        string? messageId,
        string exType,
        string? where);

    [LoggerMessage(
        EventId = 307,
        Level = LogLevel.Error,
        Message = "Boundary failure on queue={Queue} (cause={Cause}, msgId={MessageId}, "
            + "exType={ExType}, where={Where}); routing to DLQ.")]
    public static partial void BoundaryFailure(
        ILogger logger,
        string queue,
        string cause,
        string? messageId,
        string exType,
        string? where);

    [LoggerMessage(
        EventId = 308,
        Level = LogLevel.Information,
        Message = "ConsumerHostedService: started {Count} subscriber channel(s).")]
    public static partial void HostStarted(ILogger logger, int count);

    [LoggerMessage(
        EventId = 309,
        Level = LogLevel.Error,
        Message = "DLQ republish failed on queue={Queue} (dlx={Dlx}, "
            + "deliveryTag={DeliveryTag}, msgId={MessageId}, exType={ExType}, "
            + "where={Where}); falling back to BasicNack so x-dead-letter-exchange "
            + "routes the message — x-d2-failure-reason header will NOT survive.")]
    public static partial void DlqRepublishFailed(
        ILogger logger,
        string queue,
        string dlx,
        ulong deliveryTag,
        string? messageId,
        string exType,
        string? where);

    [LoggerMessage(
        EventId = 310,
        Level = LogLevel.Warning,
        Message = "Disposal drain timed out on queue={Queue} after {TimeoutMs}ms — "
            + "{InflightCount} in-flight callback(s) still running. Closing channel "
            + "anyway; affected handlers may surface ObjectDisposedException on "
            + "their next ack/nack and the broker will redeliver the message.")]
    public static partial void DisposeDrainTimeout(
        ILogger logger,
        string queue,
        int inflightCount,
        int timeoutMs);

    [LoggerMessage(
        EventId = 311,
        Level = LogLevel.Warning,
        Message = "Retries exhausted on queue={Queue} (msgId={MessageId}, "
            + "attemptCount={AttemptCount}, max={MaxAttempts}); routing direct "
            + "to DLQ without invoking the handler.")]
    public static partial void RetriesExhausted(
        ILogger logger,
        string queue,
        string? messageId,
        int attemptCount,
        int maxAttempts);

    [LoggerMessage(
        EventId = 312,
        Level = LogLevel.Error,
        Message = "BasicAck failed on queue={Queue} (deliveryTag={DeliveryTag}, "
            + "msgId={MessageId}, exType={ExType}, where={Where}); broker will "
            + "redeliver — idempotency mark already written, so handler will "
            + "skip on retry.")]
    public static partial void AckFailed(
        ILogger logger,
        string queue,
        ulong deliveryTag,
        string? messageId,
        string exType,
        string? where);

    [LoggerMessage(
        EventId = 313,
        Level = LogLevel.Error,
        Message = "Idempotency mark write failed on queue={Queue} "
            + "(deliveryTag={DeliveryTag}, msgId={MessageId}, "
            + "errorCode={ErrorCode}); NACKing to DLQ rather than acking — "
            + "acking would leave the dedup window unguarded and a "
            + "redelivery would re-run the handler.")]
    public static partial void IdempotencyMarkFailed(
        ILogger logger,
        string queue,
        ulong deliveryTag,
        string? messageId,
        string? errorCode);

    [LoggerMessage(
        EventId = 314,
        Level = LogLevel.Error,
        Message = "ConsumerHostedService background startup faulted; subscribers "
            + "did not start. Investigate the underlying exception before "
            + "expecting deliveries.")]
    public static partial void HostStartupFaulted(ILogger logger, Exception ex);
}
