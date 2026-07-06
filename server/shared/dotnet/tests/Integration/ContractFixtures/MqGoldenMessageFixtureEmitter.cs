// -----------------------------------------------------------------------
// <copyright file="MqGoldenMessageFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using D2.Shared.Auth.Events;
using D2.Shared.Context.Abstractions;
using D2.Shared.Headers.Amqp;
using D2.Shared.Messaging;
using Xunit;

/// <summary>
/// Emits golden wire messages (body bytes + AMQP headers) that the TS
/// <c>@d2/messaging-rabbitmq</c> Testcontainer integration suite replays through
/// a real broker (D11). The body is serialized with the SAME options the
/// runtime <c>EncryptedBodyComposer</c> uses (camelCase + omit-null); the
/// <c>x-d2-context</c> header is produced by the real
/// <see cref="PropagatedContextSerializer.Encode"/> (base64url-of-JSON), so the
/// TS consumer proves it decodes genuine .NET wire output. Binary bodies are
/// base64-encoded in the fixture JSON.
/// </summary>
public sealed class MqGoldenMessageFixtureEmitter
{
    private const string CATALOG = "mq-messages-golden";
    private const string PRODUCER_TRACE_ID = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string TRACEPARENT =
        "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    // Mirrors D2.Shared.Messaging.RabbitMq MessagingJsonOptions (internal to the
    // rabbitmq lib) — camelCase property names + omit-null, the exact shape the
    // publisher writes on the wire.
    private static readonly JsonSerializerOptions sr_bodyOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_PlaintextKeyRotated()
    {
        var evt = new KeyRotatedEvent
        {
            Domain = "audit",
            Kid = "audit-2026-07",
            NewStatus = "Active",
            Urgent = true,
        };

        var descriptor = MqMessagesRegistry.ByConstant[MqMessages.AuthKeyRotated];
        var body = JsonSerializer.SerializeToUtf8Bytes(evt, sr_bodyOptions);

        var ctx = new PropagatedContext
        {
            RequestId = "req-golden-1",
            RequestPath = "/v2/keys/rotate",
            WhoIsHashId = "whois-golden-0001",
        };

        var propagated = PropagatedContextSerializer.Encode(ctx);

        var headers = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            [AmqpHeaders.CONTENT_TYPE] = "application/octet-stream",
            [AmqpHeaders.PROTO_TYPE] = typeof(KeyRotatedEvent).FullName,
            [AmqpHeaders.MESSAGE_ID] = "0192f8c1-1111-7000-8000-0000000000aa",
            [AmqpHeaders.TRACEPARENT] = TRACEPARENT,
            [AmqpHeaders.PROPAGATED_CONTEXT] = propagated,
        };

        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["exchange"] = descriptor.Exchange,
            ["exchangeType"] = descriptor.ExchangeType,
            ["routingKey"] = descriptor.DefaultRoutingKey ?? string.Empty,
            ["bodyBase64"] = Convert.ToBase64String(body),
            ["headers"] = headers,
            ["expectedDecoded"] = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["domain"] = evt.Domain,
                ["kid"] = evt.Kid,
                ["newStatus"] = evt.NewStatus,
                ["urgent"] = evt.Urgent,
            },
            ["producerTraceId"] = PRODUCER_TRACE_ID,
            ["expectedRequestId"] = ctx.RequestId,
        };

        FixturePathHelpers.WriteFixture(CATALOG, "auth-key-rotated-plaintext", data);
    }

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_EncryptedFrame()
    {
        // A representative encrypted-domain frame — the first byte is the
        // symmetric encryption-frame version (1). The TS consumer keys its
        // fail-loud DECRYPT_FAILURE path on this version byte, so no live
        // keyring is needed to prove the guard routes ciphertext to the DLQ.
        var frame = new byte[40];
        frame[0] = 1; // EncryptionFrame.CURRENT_VERSION
        frame[1] = 4; // kid length
        frame[2] = (byte)'k';
        frame[3] = (byte)'1';
        frame[4] = (byte)'-';
        frame[5] = (byte)'a';

        for (var i = 6; i < frame.Length; i++)
            frame[i] = (byte)((i * 7) % 251);

        var headers = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            [AmqpHeaders.CONTENT_TYPE] = "application/octet-stream",
            [AmqpHeaders.PROTO_TYPE] = "D2.Shared.Sample.EncryptedFixtureEvent",
            [AmqpHeaders.MESSAGE_ID] = "0192f8c1-2222-7000-8000-0000000000bb",
            [AmqpHeaders.ENCRYPTION_KID] = "k1-a",
        };

        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["bodyBase64"] = Convert.ToBase64String(frame),
            ["headers"] = headers,
        };

        FixturePathHelpers.WriteFixture(CATALOG, "encrypted-frame", data);
    }
}
