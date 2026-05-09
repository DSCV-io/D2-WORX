// -----------------------------------------------------------------------
// <copyright file="DlqFailureHeaderBuilder.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Messaging.RabbitMq.Subscribing;

using System.Text;
using System.Text.Json;
using D2.Shared.Result;

/// <summary>
/// Builds the <c>x-d2-failure-reason</c> header value (JSON-encoded
/// <see cref="DlqFailureMetadata"/>) attached when a consumer NACKs a
/// message to its DLQ. Truncates the diagnostic detail at 256 chars so the
/// header doesn't blow past AMQP frame limits.
/// </summary>
/// <remarks>
/// PII discipline: <see cref="DlqFailureMetadata.Detail"/> is built from
/// the exception's TYPE NAME or the result's MESSAGE-KEYS (stable
/// translation tokens), NEVER from <c>exception.Message</c> or any
/// free-form result text. Handler code is responsible for not leaking
/// user input into exception messages, but this builder defends against
/// it anyway by NOT propagating those strings into the broker-readable
/// DLQ header.
/// </remarks>
internal static class DlqFailureHeaderBuilder
{
    private const int _DETAIL_MAX_CHARS = 256;

    /// <summary>Builds the header bytes for a handler exception.</summary>
    /// <param name="exception">The exception thrown by the handler.</param>
    /// <param name="attemptCount">Total redelivery attempts so far.</param>
    /// <param name="traceId">Optional W3C trace id for correlation.</param>
    /// <param name="nackedBy">Optional service name.</param>
    public static byte[] FromException(
        Exception exception,
        int attemptCount = 0,
        string? traceId = null,
        string? nackedBy = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var meta = new DlqFailureMetadata
        {
            Cause = Causes.HANDLER_EXCEPTION,
            ErrorCode = exception.GetType().FullName ?? exception.GetType().Name,

            // PII guard: do NOT propagate exception.Message (handler-built
            // strings can interpolate user input). The exception type name
            // already lives in ErrorCode; Detail stays null.
            Detail = null,
            AttemptCount = attemptCount,
            TraceId = traceId,
            NackedBy = nackedBy,
        };
        return Encode(meta);
    }

    /// <summary>Builds the header bytes for a handler result failure.</summary>
    /// <param name="result">The non-ok handler result.</param>
    /// <param name="attemptCount">Total redelivery attempts so far.</param>
    /// <param name="traceId">Optional W3C trace id for correlation.</param>
    /// <param name="nackedBy">Optional service name.</param>
    public static byte[] FromResult(
        D2Result result,
        int attemptCount = 0,
        string? traceId = null,
        string? nackedBy = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        var meta = new DlqFailureMetadata
        {
            Cause = Causes.HANDLER_RESULT_FAILURE,
            ErrorCode = result.ErrorCode ?? "UNKNOWN",
            Detail = Truncate(string.Join("; ", result.Messages.Select(m => m.Key))),
            AttemptCount = attemptCount,
            TraceId = traceId,
            NackedBy = nackedBy,
        };
        return Encode(meta);
    }

    /// <summary>Builds the header bytes for a retries-exhausted DLQ
    /// route — used when the broker's <c>x-death</c> header reports we've
    /// already burned the subscriber's
    /// <see cref="TieredRetryDescriptor.MaxAttempts"/>. No exception is
    /// involved (we never even invoked the handler this time).</summary>
    /// <param name="attemptCount">Total redelivery attempts observed.</param>
    /// <param name="traceId">Optional W3C trace id for correlation.</param>
    /// <param name="nackedBy">Optional service name.</param>
    public static byte[] FromRetriesExhausted(
        int attemptCount,
        string? traceId = null,
        string? nackedBy = null)
    {
        var meta = new DlqFailureMetadata
        {
            Cause = Causes.RETRIES_EXHAUSTED,
            ErrorCode = Causes.RETRIES_EXHAUSTED,
            Detail = null,
            AttemptCount = attemptCount,
            TraceId = traceId,
            NackedBy = nackedBy,
        };
        return Encode(meta);
    }

    /// <summary>Builds the header bytes for a body decrypt / parse failure.</summary>
    /// <param name="cause">One of <see cref="Causes"/>.</param>
    /// <param name="exception">The exception caught.</param>
    /// <param name="traceId">Optional W3C trace id for correlation.</param>
    /// <param name="nackedBy">Optional service name.</param>
    public static byte[] FromBoundary(
        string cause,
        Exception exception,
        string? traceId = null,
        string? nackedBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cause);
        ArgumentNullException.ThrowIfNull(exception);
        var meta = new DlqFailureMetadata
        {
            Cause = cause,
            ErrorCode = exception.GetType().FullName ?? exception.GetType().Name,

            // PII guard: same as FromException — exception.Message is not safe
            // to attach to the broker-readable header.
            Detail = null,
            AttemptCount = 0,
            TraceId = traceId,
            NackedBy = nackedBy,
        };
        return Encode(meta);
    }

    private static byte[] Encode(DlqFailureMetadata meta)
    {
        var json = JsonSerializer.Serialize(meta, MessagingJsonOptions.Options);
        return Encoding.UTF8.GetBytes(json);
    }

    private static string? Truncate(string? input)
    {
        if (input is null) return null;

        return input.Length <= _DETAIL_MAX_CHARS ? input : input[.._DETAIL_MAX_CHARS];
    }

    /// <summary>Causes — see <see cref="DlqFailureMetadata.Cause"/>.</summary>
    public static class Causes
    {
        /// <summary>Handler returned a non-Ok <see cref="D2Result"/>.</summary>
        public const string HANDLER_RESULT_FAILURE = "HANDLER_RESULT_FAILURE";

        /// <summary>Handler threw an unhandled exception.</summary>
        public const string HANDLER_EXCEPTION = "HANDLER_EXCEPTION";

        /// <summary>AEAD decrypt failed (kid missing, tag mismatch, etc.).</summary>
        public const string DECRYPT_FAILURE = "DECRYPT_FAILURE";

        /// <summary>JSON deserialization of the message body failed.</summary>
        public const string DESERIALIZE_FAILURE = "DESERIALIZE_FAILURE";

        /// <summary>Retry tier limit hit; message routed direct to DLQ.</summary>
        public const string RETRIES_EXHAUSTED = "RETRIES_EXHAUSTED";
    }
}
