// -----------------------------------------------------------------------
// <copyright file="DlqFailureMetadata.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

using System.Text.Json.Serialization;

/// <summary>
/// JSON shape attached to a dead-lettered message via the
/// <c>x-d2-failure-reason</c> header. Lets ops triage from queue inspection
/// without decrypting the body.
/// </summary>
/// <remarks>
/// Kept small because RabbitMQ headers are not unbounded. Truncate
/// long messages at the consumer (256 chars is plenty for triage).
///
/// Property names are explicitly bound to <see cref="DlqFailureMetadataFields"/>
/// codegen-emitted constants via <see cref="JsonPropertyNameAttribute"/>.
/// This eliminates implicit reliance on <c>MessagingJsonOptions</c>'
/// <c>JsonNamingPolicy.CamelCase</c> and gives the spec a single source of
/// truth for the wire shape — drift between the .NET wire encoder and the
/// spec is structurally impossible.
/// </remarks>
public sealed record DlqFailureMetadata
{
    /// <summary>Gets the handler's failure cause — one of the
    /// <c>DlqFailureCauses</c> constants (closed-enum string catalog emitted
    /// by DcsvIo.D2.Messaging.DlqMetadata.SourceGen into DcsvIo.D2.Messaging.RabbitMq).
    /// The producer-side <c>DlqFailureHeaderBuilder</c> only emits values
    /// from that closed catalog.</summary>
    [JsonPropertyName(DlqFailureMetadataFields.CAUSE)]
    public required string Cause { get; init; }

    /// <summary>Gets the exception type's full name when <c>Cause</c> is
    /// HANDLER_EXCEPTION, or the D2Result error code otherwise.</summary>
    [JsonPropertyName(DlqFailureMetadataFields.ERROR_CODE)]
    public required string ErrorCode { get; init; }

    /// <summary>Gets the truncated diagnostic message (≤256 chars).</summary>
    [JsonPropertyName(DlqFailureMetadataFields.DETAIL)]
    public string? Detail { get; init; }

    /// <summary>Gets how many times this message had been redelivered + retried
    /// before final fail (read from the AMQP <c>x-death</c> header where
    /// available; 0 if not).</summary>
    [JsonPropertyName(DlqFailureMetadataFields.ATTEMPT_COUNT)]
    public int AttemptCount { get; init; }

    /// <summary>Gets the producer-side trace-id, copied from the message
    /// headers for cross-hop OTel correlation when the DLQ entry is
    /// investigated.</summary>
    [JsonPropertyName(DlqFailureMetadataFields.TRACE_ID)]
    public string? TraceId { get; init; }

    /// <summary>Gets the consumer service name (which replica nacked).</summary>
    [JsonPropertyName(DlqFailureMetadataFields.NACKED_BY)]
    public string? NackedBy { get; init; }
}
