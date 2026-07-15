// -----------------------------------------------------------------------
// <copyright file="MqMessageDescriptor.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

/// <summary>
/// Fully-resolved publisher contract for one message type. Codegen-emitted
/// from <c>contracts/mq-messages/mq-messages.spec.json</c> by
/// <c>DcsvIo.D2.Messaging.SourceGen</c>; one per <c>MqMessages.X</c> constant.
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
    /// without payload encryption." Same wire value as
    /// <see cref="DcsvIo.D2.Encryption.EncryptionDomains.PLAINTEXT"/> — kept
    /// as a per-descriptor alias for backward compat with code that imports
    /// the descriptor type without pulling the encryption assembly into scope.
    /// </summary>
    public const string PLAINTEXT = DcsvIo.D2.Encryption.EncryptionDomains.PLAINTEXT;

    /// <summary>Gets a value indicating whether this descriptor declares
    /// plaintext (no payload encryption) — i.e. <see cref="Encryption"/>
    /// equals <see cref="PLAINTEXT"/>.</summary>
    public bool IsPlaintext =>
        string.Equals(Encryption, PLAINTEXT, System.StringComparison.Ordinal);

    /// <summary>Gets a value indicating whether this descriptor's
    /// <see cref="Encryption"/> domain is in the per-consumer-service SEALED
    /// (asymmetric) mode rather than shared-keyring symmetric mode. Computed
    /// from <see cref="DcsvIo.D2.Encryption.EncryptionDomainModeCatalog"/>
    /// (public generated baseline + product overlay registrations) — the domain
    /// mode is a single-source domain fact, never a second generated field on
    /// this descriptor. A plaintext or unknown domain is <c>false</c>.</summary>
    public bool IsSealed =>
        DcsvIo.D2.Encryption.EncryptionDomainModeCatalog.ModeFor(Encryption)
            == DcsvIo.D2.Encryption.EncryptionDomainMode.Sealed;

    /// <summary>Gets the single consumer ServiceId that opens sealed frames on
    /// this descriptor's domain, or <see langword="null"/> for a symmetric or
    /// plaintext domain. The keyed <see cref="DcsvIo.D2.Encryption.IPayloadSealer"/>
    /// (publish) and <see cref="DcsvIo.D2.Encryption.IPayloadOpener"/> (consume)
    /// are resolved by this value — sealed material is keyed by the recipient
    /// SERVICE, so two sealed domains sharing a consumer share one sealer/opener.
    /// Computed from
    /// <see cref="DcsvIo.D2.Encryption.EncryptionDomainModeCatalog.TryGetConsumerService"/>
    /// — never a constructor/positional record parameter.</summary>
    public string? ConsumerService =>
        DcsvIo.D2.Encryption.EncryptionDomainModeCatalog.TryGetConsumerService(
            Encryption,
            out var svc)
            ? svc
            : null;
}
