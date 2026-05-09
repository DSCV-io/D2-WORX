// -----------------------------------------------------------------------
// <copyright file="AmqpHeaders.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Messaging;

/// <summary>
/// Plaintext AMQP header names propagated on every D² message. These stay
/// outside the encrypted body so the broker can route + observe without
/// decryption, and so DLQ inspection works without keyring access.
/// </summary>
/// <remarks>
/// Headers MUST NOT carry user identity, scopes, fingerprints, or any other
/// sensitive context — the broker stores headers as plaintext at rest. Only
/// the fields below are emitted: routing / observability / DLQ-triage data.
/// Sensitive identity that a consumer needs goes in the typed message body
/// (encrypted via the descriptor's <c>encryption</c> domain when the type
/// carries PII — see <c>contracts/mq-messages/mq-messages.spec.json</c>).
/// </remarks>
public static class AmqpHeaders
{
    /// <summary>Always <c>application/octet-stream</c> for D² messages — the
    /// body is encrypted bytes (or proto-canonical JSON bytes, never a
    /// directly-readable structured type).</summary>
    public const string CONTENT_TYPE = "content-type";

    /// <summary>Fully-qualified proto type name (e.g.
    /// <c>d2.events.files.v1.FileUploadedEvent</c>). Lets consumers
    /// fail-fast on a type mismatch before deserialization.</summary>
    public const string PROTO_TYPE = "x-proto-type";

    /// <summary>UUIDv7 message identifier — sortable, includes timestamp,
    /// suitable as an idempotency key.</summary>
    public const string MESSAGE_ID = "message-id";

    /// <summary>Producer-set ISO 8601 UTC timestamp.</summary>
    public const string TIMESTAMP = "timestamp";

    /// <summary>
    /// W3C <c>traceparent</c> header — full
    /// <c>{version}-{traceId}-{spanId}-{flags}</c> string per
    /// https://www.w3.org/TR/trace-context. Consumers parse it to start a
    /// child span whose parent is the producer's publish span, so cross-hop
    /// trace assembly works in any OTel backend without bespoke linking.
    /// </summary>
    public const string TRACEPARENT = "traceparent";

    /// <summary>
    /// W3C <c>tracestate</c> header (vendor-specific tracing context that
    /// rides alongside <c>traceparent</c>). Forwarded as-is so non-D²
    /// tracing systems chained through us don't lose state.
    /// </summary>
    public const string TRACESTATE = "tracestate";

    /// <summary>Base64url-of-JSON encoded <c>PropagatedContext</c> — the
    /// small set of cross-hop fields a downstream consumer can't recompute
    /// (request id, original request path, fingerprints, WhoIs hash). Same
    /// header name + encoding on AMQP / gRPC / HTTP. Identity fields are
    /// NEVER in here — they rebuild from the JWT at every hop.</summary>
    public const string CONTEXT = "x-d2-context";

    /// <summary>Encryption key id (kid). Duplicated from the encrypted frame
    /// so ops can decide whether archive keys are needed for a stuck DLQ
    /// message without decrypting first.</summary>
    public const string ENCRYPTION_KID = "x-d2-encryption-kid";

    /// <summary>Failure reason metadata attached by the consumer when
    /// nacking a message to its DLQ. JSON-encoded
    /// <see cref="DlqFailureMetadata"/>.</summary>
    public const string FAILURE_REASON = "x-d2-failure-reason";
}
