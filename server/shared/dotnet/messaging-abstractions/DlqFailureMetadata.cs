// -----------------------------------------------------------------------
// <copyright file="DlqFailureMetadata.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Messaging;

/// <summary>
/// JSON shape attached to a dead-lettered message via the
/// <c>x-d2-failure-reason</c> header. Lets ops triage from queue inspection
/// without decrypting the body.
/// </summary>
/// <remarks>
/// Kept small because RabbitMQ headers are not unbounded. Truncate
/// long messages at the consumer (256 chars is plenty for triage).
/// </remarks>
public sealed record DlqFailureMetadata
{
    /// <summary>Gets the handler's failure cause — one of:
    /// <c>HANDLER_RESULT_FAILURE</c> (D2Result.IsOk == false),
    /// <c>HANDLER_EXCEPTION</c> (handler threw),
    /// <c>DECRYPT_FAILURE</c>,
    /// <c>DESERIALIZE_FAILURE</c>,
    /// <c>RETRIES_EXHAUSTED</c>.</summary>
    public required string Cause { get; init; }

    /// <summary>Gets the exception type's full name when <c>Cause</c> is
    /// HANDLER_EXCEPTION, or the D2Result error code otherwise.</summary>
    public required string ErrorCode { get; init; }

    /// <summary>Gets the truncated diagnostic message (≤256 chars).</summary>
    public string? Detail { get; init; }

    /// <summary>Gets how many times this message had been redelivered + retried
    /// before final fail (read from the AMQP <c>x-death</c> header where
    /// available; 0 if not).</summary>
    public int AttemptCount { get; init; }

    /// <summary>Gets the producer-side trace-id, copied from the message
    /// headers for cross-hop OTel correlation when the DLQ entry is
    /// investigated.</summary>
    public string? TraceId { get; init; }

    /// <summary>Gets the consumer service name (which replica nacked).</summary>
    public string? NackedBy { get; init; }
}
