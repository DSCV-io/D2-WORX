// -----------------------------------------------------------------------
// <copyright file="MqMessageEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

/// <summary>
/// One entry parsed from <c>mq-messages.spec.json</c>. Pairs a message type
/// with its publisher-side contract (target exchange, exchange type,
/// encryption domain, default routing key).
/// </summary>
/// <param name="Constant">PascalCase identifier emitted as a const string under
/// <c>MqMessages</c>.</param>
/// <param name="MessageType">Fully-qualified .NET type name of the message
/// class.</param>
/// <param name="Exchange">AMQP exchange name (lowercase, dot- and hyphen-
/// separated; convention <c>d2.{producer}.{purpose}</c>).</param>
/// <param name="ExchangeType">AMQP exchange type — <c>fanout</c>, <c>topic</c>,
/// or <c>direct</c>.</param>
/// <param name="Encryption">Either an <c>EncryptionDomains</c> constant value
/// (e.g. <c>"audit"</c>) OR the literal string <c>"plaintext"</c>.</param>
/// <param name="EncryptionReason">Required when
/// <paramref name="Encryption"/> is <c>"plaintext"</c>; null otherwise.</param>
/// <param name="DefaultRoutingKey">Routing key used by publishers when no
/// per-publish override is supplied. Empty for fanout. Null = empty.</param>
internal sealed record MqMessageEntry(
    string Constant,
    string MessageType,
    string Exchange,
    string ExchangeType,
    string Encryption,
    string? EncryptionReason,
    string? DefaultRoutingKey);
