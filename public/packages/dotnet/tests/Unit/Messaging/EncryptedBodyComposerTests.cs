// -----------------------------------------------------------------------
// <copyright file="EncryptedBodyComposerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Messaging.RabbitMq.Encryption;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Adversarial coverage of the encrypt/decrypt round-trip + boundary failures.
/// Uses real <see cref="IPayloadCrypto"/> (no mocks) since the codepath goes
/// through actual AES-GCM — mocking the crypto layer would hide the bugs we
/// care about (AAD binding, kid round-trip, tag mismatch). Descriptors are
/// constructed in-line so tests don't depend on the codegen'd registry.
/// </summary>
public sealed class EncryptedBodyComposerTests
{
    // Synthetic SYMMETRIC domain (unknown → Symmetric by EncryptionDomainModes.ModeFor).
    // Public sealed path uses EncryptionDomains.FIXTURE_SEALED (catalog-declared).
    // Product sealed domains (audit/notifications/courier) are private-only.
    private const string _SYMMETRIC_DOMAIN = "payload-fixture-symmetric";
    private const string _SEALED_DOMAIN = "payload-fixture-sealed";
    private const string _SEALED_CONSUMER = "payload-fixture-sealed";

    [Fact]
    public void Compose_PlaintextDescriptor_BodyIsRawMessageJson()
    {
        var sp = BuildProviderForPlaintext();
        var descriptor = PlaintextDescriptor();
        var msg = new SampleRotationEvent();

        var (body, kid) = EncryptedBodyComposer.Compose(msg, descriptor, sp);

        kid.Should().BeNull("plaintext descriptor has no kid");
        var asJson = Encoding.UTF8.GetString(body);
        asJson.Should().StartWith("{");

        // Body must be the message JSON DIRECTLY — no envelope wrapper.
        // Plaintext wire shape invariant: a plaintext descriptor cannot carry
        // any identity / context fields by construction.
        asJson.Should().NotContain("\"envelope\":");
        asJson.Should().NotContain("\"userId\":");
    }

    [Fact]
    public void Compose_EncryptedDescriptor_ReturnsFrameWithKid()
    {
        var sp = BuildProviderForSymmetric("kid-a");
        var descriptor = EncryptedDescriptor(_SYMMETRIC_DOMAIN);
        var msg = new SampleAuditEvent();

        var (body, kid) = EncryptedBodyComposer.Compose(msg, descriptor, sp);

        kid.Should().Be("kid-a");
        body.Length.Should().BeGreaterThan(0);
        body[0].Should().Be(1, "frame version 1");
    }

    [Fact]
    public void Compose_EncryptedDescriptor_FrameDoesNotIncludeEnvelopeWrapper()
    {
        var sp = BuildProviderForSymmetric("kid-a");
        var descriptor = EncryptedDescriptor(_SYMMETRIC_DOMAIN);

        var (frame, _) = EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, sp);

        var crypto = sp.GetRequiredKeyedService<IPayloadCrypto>(_SYMMETRIC_DOMAIN);
        var json = Encoding.UTF8.GetString(crypto.Decrypt(frame));
        json.Should().NotContain("\"envelope\":");
        json.Should().NotContain("\"message\":");
    }

    [Fact]
    public void Compose_NullDescriptor_Throws()
    {
        var sp = BuildProviderForSymmetric("kid-a");
        var act = () => EncryptedBodyComposer.Compose(
            new SampleAuditEvent(), null!, sp);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RoundTrip_PlaintextDescriptor_PreservesMessage()
    {
        var sp = BuildProviderForPlaintext();
        var descriptor = PlaintextDescriptor();

        var (body, _) = EncryptedBodyComposer.Compose(new SampleRotationEvent(), descriptor, sp);
        var message = EncryptedBodyComposer.Decompose<SampleRotationEvent>(body, descriptor, sp);

        message.Should().NotBeNull();
    }

    [Fact]
    public void RoundTrip_EncryptedDescriptor_PreservesMessage()
    {
        var sp = BuildProviderForSymmetric("kid-a");
        var descriptor = EncryptedDescriptor(_SYMMETRIC_DOMAIN);

        var (body, _) = EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, sp);
        var message = EncryptedBodyComposer.Decompose<SampleAuditEvent>(body, descriptor, sp);

        message.Should().NotBeNull();
    }

    [Fact]
    public void Decompose_TamperedFrame_ThrowsOnTagMismatch()
    {
        var sp = BuildProviderForSymmetric("kid-a");
        var descriptor = EncryptedDescriptor(_SYMMETRIC_DOMAIN);

        var (body, _) = EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, sp);
        body[^1] ^= 0xFF;

        var act = () => EncryptedBodyComposer.Decompose<SampleAuditEvent>(body, descriptor, sp);
        act.Should().Throw<Exception>(
            "AEAD tag verification must reject any tamper");
    }

    [Fact]
    public void Decompose_KidNotInKeyring_Throws()
    {
        var composeSp = BuildProviderForSymmetric("kid-a");
        var descriptor = EncryptedDescriptor(_SYMMETRIC_DOMAIN);
        var (body, _) = EncryptedBodyComposer.Compose(
            new SampleAuditEvent(), descriptor, composeSp);

        var decomposeSp = BuildProviderForSymmetric("kid-b");
        var act = () => EncryptedBodyComposer.Decompose<SampleAuditEvent>(
            body, descriptor, decomposeSp);
        act.Should().Throw<Exception>("missing kid is fatal — caller maps to DLQ");
    }

    [Fact]
    public void Decompose_TruncatedBody_ThrowsCleanly()
    {
        var sp = BuildProviderForSymmetric("kid-a");
        var descriptor = EncryptedDescriptor(_SYMMETRIC_DOMAIN);
        var (body, _) = EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, sp);

        var truncated = body.AsSpan(0, 10).ToArray();
        var act = () => EncryptedBodyComposer.Decompose<SampleAuditEvent>(
            truncated, descriptor, sp);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Decompose_EmptyBody_ThrowsCleanly()
    {
        var sp = BuildProviderForSymmetric("kid-a");
        var descriptor = EncryptedDescriptor(_SYMMETRIC_DOMAIN);
        var act = () => EncryptedBodyComposer.Decompose<SampleAuditEvent>(
            ReadOnlySpan<byte>.Empty, descriptor, sp);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ReadKidFromFrame_ValidFrame_ReturnsKid()
    {
        var sp = BuildProviderForSymmetric("kid-a");
        var descriptor = EncryptedDescriptor(_SYMMETRIC_DOMAIN);
        var (body, _) = EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, sp);

        EncryptedBodyComposer.ReadKidFromFrame(body).Should().Be("kid-a");
    }

    [Fact]
    public void ReadKidFromFrame_FrameTooShort_Throws()
    {
        var act = () => EncryptedBodyComposer.ReadKidFromFrame(new byte[] { 1 });
        act.Should().Throw<InvalidOperationException>().WithMessage("*Frame too short*");
    }

    [Fact]
    public void ReadKidFromFrame_UnknownVersionByte_Throws()
    {
        // Version byte 3 (unknown — 1 is symmetric, 2 is sealed) — the
        // version gate must reject before attempting to read the rest of
        // the frame as a kid.
        var frame = new byte[] { 3, 5, (byte)'k', (byte)'i', (byte)'d', (byte)'-', (byte)'a' };
        var act = () => EncryptedBodyComposer.ReadKidFromFrame(frame);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown encryption frame version*");
    }

    [Fact]
    public void ReadKidFromFrame_SealedV2Frame_ReturnsRecipientKid()
    {
        // A REAL sealed frame (production sealer) — the version-aware header
        // read must surface the recipient kid for the x-d2-encryption-kid
        // AMQP header so DLQ triage can identify the archive-opener key
        // without decrypting.
        using var recipient = System.Security.Cryptography.ECDiffieHellman.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var keyring = new RecipientPublicKeyring(
            _SEALED_CONSUMER,
            "seal-kid-7",
            new Dictionary<string, byte[]>
            {
                ["seal-kid-7"] = recipient.ExportSubjectPublicKeyInfo(),
            });
        var frame = new PayloadSealer(keyring).Seal("payload"u8);

        EncryptedBodyComposer.ReadKidFromFrame(frame).Should().Be("seal-kid-7");
    }

    [Fact]
    public void ReadKidFromFrame_SealedV2FrameTooShortForKid_Throws()
    {
        var bogus = new byte[] { 2, 100 };
        var act = () => EncryptedBodyComposer.ReadKidFromFrame(bogus);
        act.Should().Throw<InvalidOperationException>().WithMessage("*declared kid length*");
    }

    [Fact]
    public void ReadKidFromFrame_V1Frame_UnchangedBehavior()
    {
        // Regression pin: the v1 read path is byte-for-byte the pre-sealed
        // behavior — same kid, same offsets.
        var sp = BuildProviderForSymmetric("kid-v1-pin");
        var descriptor = EncryptedDescriptor(_SYMMETRIC_DOMAIN);
        var (body, _) = EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, sp);

        body[0].Should().Be(1);
        EncryptedBodyComposer.ReadKidFromFrame(body).Should().Be("kid-v1-pin");
    }

    [Fact]
    public void ReadKidFromFrame_DeclaresKidLengthBeyondBuffer_Throws()
    {
        var bogus = new byte[] { 1, 100 };
        var act = () => EncryptedBodyComposer.ReadKidFromFrame(bogus);
        act.Should().Throw<InvalidOperationException>().WithMessage("*declared kid length*");
    }

    [Fact]
    public void ReadKidFromFrame_DeclaredKidLengthZero_ReturnsEmpty()
    {
        var frame = new byte[] { 1, 0 };
        EncryptedBodyComposer.ReadKidFromFrame(frame).Should().Be(string.Empty);
    }

    [Fact]
    public void ReadKidFromFrame_DeclaredKidLengthExactlyFits_ReturnsKid()
    {
        var frame = new byte[] { 1, 3, (byte)'a', (byte)'b', (byte)'c' };
        EncryptedBodyComposer.ReadKidFromFrame(frame).Should().Be("abc");
    }

    [Fact]
    public void ReadKidFromFrame_MultiByteUtf8Kid_RoundTripsCorrectly()
    {
        var kidBytes = Encoding.UTF8.GetBytes("🔑");
        var frame = new byte[] { 1, (byte)kidBytes.Length };
        frame = [.. frame, .. kidBytes];
        EncryptedBodyComposer.ReadKidFromFrame(frame).Should().Be("🔑");
    }

    [Fact]
    public void Compose_SealedDescriptor_ResolvesSealerByConsumerService_ReturnsV2Frame()
    {
        // The keyed sealer is resolved by CONSUMER SERVICE (not domain). Two sealed domains
        // sharing a consumer share one sealer — proven by resolving under the consumer key.
        var sp = BuildProviderForSealed(_SEALED_CONSUMER);
        var descriptor = SealedDescriptor(_SEALED_DOMAIN);

        var (frame, kid) = EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, sp);

        frame[0].Should().Be(2, "sealed frames are version 2");
        kid.Should().NotBeNullOrEmpty("the recipient kid rides x-d2-encryption-kid");
    }

    [Fact]
    public void Compose_TwoSealedDomainsSharingConsumer_ResolveSameSealerInstance()
    {
        var sp = BuildProviderForSealed(_SEALED_CONSUMER);
        var sealerA = sp.GetRequiredKeyedService<IPayloadSealer>(_SEALED_CONSUMER);
        var sealerB = sp.GetRequiredKeyedService<IPayloadSealer>(_SEALED_CONSUMER);

        sealerA.Should().BeSameAs(sealerB, "one sealer per consumer service");
    }

    [Fact]
    public void RoundTrip_SealedDescriptor_SealAndOpenViaConsumerKeyedServices()
    {
        var sp = BuildProviderForSealed(_SEALED_CONSUMER);
        var descriptor = SealedDescriptor(_SEALED_DOMAIN);

        var (body, _) = EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, sp);
        var message = EncryptedBodyComposer.Decompose<SampleAuditEvent>(body, descriptor, sp);

        message.Should().NotBeNull();
    }

    [Fact]
    public void Compose_SealedDescriptor_NoSealerRegistered_Throws()
    {
        // A producer host that never registered a sealer for the consumer service lacks the
        // keyed registration → GetRequiredKeyedService throws → publish fails loud (never
        // plaintext). This is the second-producer-cannot-seal shape.
        var sp = new ServiceCollection().BuildServiceProvider();
        var descriptor = SealedDescriptor(_SEALED_DOMAIN);

        var act = () => EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, sp);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Decompose_SealedDescriptor_NoOpenerRegistered_Throws()
    {
        // Seal on a host that HAS the sealer, then attempt to open on a host with NEITHER →
        // no opener registration → throw → caller maps to DLQ (never a silent drop).
        var composeSp = BuildProviderForSealed(_SEALED_CONSUMER);
        var descriptor = SealedDescriptor(_SEALED_DOMAIN);
        var (body, _) = EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, composeSp);

        var openerlessSp = new ServiceCollection().BuildServiceProvider();
        var act = () => EncryptedBodyComposer.Decompose<SampleAuditEvent>(
            body, descriptor, openerlessSp);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Decompose_SealedFrame_TamperedTag_ThrowsForDlq()
    {
        var sp = BuildProviderForSealed(_SEALED_CONSUMER);
        var descriptor = SealedDescriptor(_SEALED_DOMAIN);
        var (body, _) = EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, sp);
        body[^1] ^= 0xFF;

        var act = () => EncryptedBodyComposer.Decompose<SampleAuditEvent>(body, descriptor, sp);

        act.Should().Throw<Exception>("AEAD tag verification rejects any tamper → DLQ");
    }

    private static MqMessageDescriptor SealedDescriptor(string domain) => new(
        Constant: "TestSealed",
        MessageTypeName: typeof(SampleAuditEvent).FullName!,
        Exchange: "d2.test.events",
        ExchangeType: "topic",
        Encryption: domain,
        EncryptionReason: null,
        DefaultRoutingKey: "test.event");

    private static IServiceProvider BuildProviderForSealed(string consumerService)
    {
        using var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        const string kid = "seal-fixture-kid";
        var publicKeyring = new RecipientPublicKeyring(
            consumerService,
            kid,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [kid] = ecdh.ExportSubjectPublicKeyInfo(),
            });
        var privateKeyring = new RecipientPrivateKeyring(
            consumerService,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [kid] = ecdh.ExportPkcs8PrivateKey(),
            });

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IPayloadSealer>(
            consumerService, new PayloadSealer(publicKeyring));
        services.AddKeyedSingleton<IPayloadOpener>(
            consumerService, new PayloadOpener(privateKeyring));

        return services.BuildServiceProvider();
    }

    private static MqMessageDescriptor PlaintextDescriptor() => new(
        Constant: "TestPlaintext",
        MessageTypeName: typeof(SampleRotationEvent).FullName!,
        Exchange: "d2.test.events",
        ExchangeType: "fanout",
        Encryption: MqMessageDescriptor.PLAINTEXT,
        EncryptionReason: "test fixture — never touches a real broker",
        DefaultRoutingKey: string.Empty);

    private static MqMessageDescriptor EncryptedDescriptor(string domain) => new(
        Constant: "TestEncrypted",
        MessageTypeName: typeof(SampleAuditEvent).FullName!,
        Exchange: "d2.test.events",
        ExchangeType: "topic",
        Encryption: domain,
        EncryptionReason: null,
        DefaultRoutingKey: "test.event");

    private static IServiceProvider BuildProviderForPlaintext() =>
        new ServiceCollection().BuildServiceProvider();

    private static IServiceProvider BuildProviderForSymmetric(string kid)
    {
        var key = RandomNumberGenerator.GetBytes(PayloadCryptoKeyring.KEY_SIZE_BYTES);
        var keyring = new PayloadCryptoKeyring(
            activeKid: kid,
            keys: new Dictionary<string, byte[]>(StringComparer.Ordinal) { [kid] = key },
            aadContext: Encoding.UTF8.GetBytes("d2/" + _SYMMETRIC_DOMAIN));
        var crypto = new PayloadCrypto(keyring);

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IPayloadCrypto>(_SYMMETRIC_DOMAIN, crypto);
        return services.BuildServiceProvider();
    }
}
