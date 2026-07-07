// -----------------------------------------------------------------------
// <copyright file="MqGoldenMessageFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using D2.Shared.Auth.Events;
using D2.Shared.Context.Abstractions;
using D2.Shared.Encryption;
using D2.Shared.Headers.Amqp;
using D2.Shared.Messaging;
using D2.Shared.Messaging.RabbitMq.Encryption;
using D2.Shared.Utilities.Extensions;
using Microsoft.Extensions.DependencyInjection;
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

    // The sealed golden-message recipient — the SAME synthetic "audit" recipient
    // (serviceId + kid + private PKCS#8) as SealedCryptoKatFixtureEmitter, so the TS
    // consume test opens this .NET-composed message with a self-contained, pinned
    // (fixture-only, no PII) private keyring. The ECDH ephemeral is still non-deterministic
    // (a fresh keypair + nonce per emit), but Emit_SealedAuditMessage writes the file ONLY
    // when it is absent or D2_REGEN_GOLDENS is set, so committed bytes do not churn on
    // normal runs.
    private const string _SEAL_RECIPIENT_SERVICE_ID = "audit";
    private const string _SEAL_RECIPIENT_KID = "seal-kat-kid";

    private const string _SEAL_RECIPIENT_PRIVATE_PKCS8_B64 =
        "MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQgAGdQitJuiEOZLHEa1ooL5nxm" +
        "9k9UDMauc/9PTbrtmbWhRANCAAQcu3gDUuYgdaan/4uF2SnWekAoJSx3nDj2merWTH0mEcok" +
        "rO0jSFyMpMLRNpOdsFH2i9X8AjOs5+Bk+J6A3U7+";

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

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_SealedAuditMessage()
    {
        // A REAL sealed (version-2) golden MESSAGE — the message JSON is composed by the
        // production EncryptedBodyComposer.Compose sealed branch, resolving the keyed
        // IPayloadSealer by the descriptor's consumer service exactly as a live producer
        // host does. It proves the TS @d2/messaging-rabbitmq consumer (CryptoBodyOpener
        // over @d2/encryption's PayloadOpener) opens a genuinely .NET-composed sealed body.
        // Unlike the deterministic sealed-crypto-kat vector, the frame is NON-deterministic
        // (the sealer mints a fresh per-message ephemeral keypair + nonce — no injection
        // point by design), so re-emitting yields a different bodyBase64. The write is
        // therefore gated: the file is written ONLY when it does not already exist, OR when
        // D2_REGEN_GOLDENS is set (e.g. D2_REGEN_GOLDENS=1). On a normal run the existing
        // committed file is left unchanged; the production-code assertions below still run.
        // The opener material + expected content stay pinned; the byte-exact gate remains
        // sealed-crypto-kat.
        var descriptor = new MqMessageDescriptor(
            Constant: "AuditSealedGoldenFixture",
            MessageTypeName: typeof(KeyRotatedEvent).FullName!,
            Exchange: "d2.audit.events",
            ExchangeType: "fanout",
            Encryption: EncryptionDomains.AUDIT,
            EncryptionReason: null,
            DefaultRoutingKey: null);

        descriptor.IsSealed.Should().BeTrue("audit is a sealed domain");
        var consumerService = descriptor.ConsumerService!;
        consumerService.Should().Be(_SEAL_RECIPIENT_SERVICE_ID);

        var evt = new KeyRotatedEvent
        {
            Domain = "audit",
            Kid = "audit-seal-2026-07",
            NewStatus = "Active",
            Urgent = false,
        };

        var recipientPkcs8 = Convert.FromBase64String(_SEAL_RECIPIENT_PRIVATE_PKCS8_B64);

        using var recipientKey = ECDiffieHellman.Create();
        recipientKey.ImportPkcs8PrivateKey(recipientPkcs8, out _);
        var recipientSpki = recipientKey.ExportSubjectPublicKeyInfo();

        var publicKeyring = new RecipientPublicKeyring(
            _SEAL_RECIPIENT_SERVICE_ID,
            _SEAL_RECIPIENT_KID,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [_SEAL_RECIPIENT_KID] = recipientSpki,
            });

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IPayloadSealer>(
            consumerService, new PayloadSealer(publicKeyring));

        using var provider = services.BuildServiceProvider();

        var (body, kid) = EncryptedBodyComposer.Compose(evt, descriptor, provider);

        body[0].Should().Be(2, "sealed golden bodies are version-2 frames");
        kid.Should().Be(_SEAL_RECIPIENT_KID, "the recipient kid rides x-d2-encryption-kid");

        var headers = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            [AmqpHeaders.CONTENT_TYPE] = "application/octet-stream",
            [AmqpHeaders.PROTO_TYPE] = typeof(KeyRotatedEvent).FullName,
            [AmqpHeaders.MESSAGE_ID] = "0192f8c1-3333-7000-8000-0000000000cc",
            [AmqpHeaders.ENCRYPTION_KID] = kid,
        };

        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["domain"] = descriptor.Encryption,
            ["consumerService"] = consumerService,
            ["recipientServiceId"] = _SEAL_RECIPIENT_SERVICE_ID,
            ["recipientKid"] = _SEAL_RECIPIENT_KID,
            ["recipientPrivatePkcs8Base64"] = _SEAL_RECIPIENT_PRIVATE_PKCS8_B64,
            ["bodyBase64"] = Convert.ToBase64String(body),
            ["headers"] = headers,
            ["expectedDecoded"] = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["domain"] = evt.Domain,
                ["kid"] = evt.Kid,
                ["newStatus"] = evt.NewStatus,
                ["urgent"] = evt.Urgent,
            },
        };

        var goldenPath = FixturePathHelpers.FixturePath(CATALOG, "sealed-audit-message");

        var regenRequested = Environment.GetEnvironmentVariable("D2_REGEN_GOLDENS").Truthy();

        if (!File.Exists(goldenPath) || regenRequested)
        {
            FixturePathHelpers.WriteFixture(CATALOG, "sealed-audit-message", data);
        }
    }
}
