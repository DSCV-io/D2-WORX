// -----------------------------------------------------------------------
// <copyright file="MqMessageDescriptor.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Messaging;

/// <summary>
/// Fully-resolved publisher contract for one message type. Codegen-emitted
/// from <c>contracts/mq-messages/mq-messages.spec.json</c> by
/// <c>D2.Shared.Messaging.SourceGen</c>; one per <c>MqMessages.X</c> constant.
/// </summary>
/// <param name="Constant">The string constant identifying this descriptor
/// (matches the value of the corresponding <c>MqMessages.X</c> field).</param>
/// <param name="MessageTypeName">Fully-qualified .NET type name of the
/// message class that carries <c>[MqPub(MqMessages.X)]</c>.</param>
/// <param name="Exchange">AMQP exchange name to publish to.</param>
/// <param name="ExchangeType">AMQP exchange type — <c>fanout</c>,
/// <c>topic</c>, or <c>direct</c>.</param>
/// <param name="Encryption">Either an <c>EncryptionDomains</c> constant
/// value (e.g. <c>"audit"</c>) — or the literal <c>"plaintext"</c>.</param>
/// <param name="EncryptionReason">When <see cref="Encryption"/> is
/// <c>"plaintext"</c>, the rationale (free-form string) explaining why
/// payload confidentiality is intentionally bypassed for this type. Null
/// when encrypted.</param>
/// <param name="DefaultRoutingKey">Routing key used by publishers when no
/// per-publish override is supplied. Null = empty.</param>
public sealed record MqMessageDescriptor(
    string Constant,
    string MessageTypeName,
    string Exchange,
    string ExchangeType,
    string Encryption,
    string? EncryptionReason,
    string? DefaultRoutingKey)
{
    /// <summary>Sentinel value representing "this message type publishes
    /// without payload encryption." Compare with <see cref="Encryption"/>
    /// using ordinal equality.</summary>
    public const string PLAINTEXT = "plaintext";

    /// <summary>Gets a value indicating whether this descriptor declares
    /// plaintext (no payload encryption) — i.e. <see cref="Encryption"/>
    /// equals <see cref="PLAINTEXT"/>.</summary>
    public bool IsPlaintext =>
        string.Equals(Encryption, PLAINTEXT, System.StringComparison.Ordinal);
}
