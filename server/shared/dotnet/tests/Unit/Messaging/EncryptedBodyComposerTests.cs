// -----------------------------------------------------------------------
// <copyright file="EncryptedBodyComposerTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Messaging;

using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using D2.Shared.Encryption;
using D2.Shared.Messaging;
using D2.Shared.Messaging.RabbitMq.Encryption;
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
        // C1/C2 invariant: a plaintext wire shape cannot carry any
        // identity / context fields by construction.
        asJson.Should().NotContain("\"envelope\":");
        asJson.Should().NotContain("\"userId\":");
    }

    [Fact]
    public void Compose_EncryptedDescriptor_ReturnsFrameWithKid()
    {
        var sp = BuildProviderForAudit("kid-a");
        var descriptor = EncryptedDescriptor(EncryptionDomains.Audit);
        var msg = new SampleAuditEvent();

        var (body, kid) = EncryptedBodyComposer.Compose(msg, descriptor, sp);

        kid.Should().Be("kid-a");
        body.Length.Should().BeGreaterThan(0);
        body[0].Should().Be(1, "frame version 1");
    }

    [Fact]
    public void Compose_EncryptedDescriptor_FrameDoesNotIncludeEnvelopeWrapper()
    {
        var sp = BuildProviderForAudit("kid-a");
        var descriptor = EncryptedDescriptor(EncryptionDomains.Audit);

        var (frame, _) = EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, sp);

        var crypto = sp.GetRequiredKeyedService<IPayloadCrypto>(EncryptionDomains.Audit);
        var json = Encoding.UTF8.GetString(crypto.Decrypt(frame));
        json.Should().NotContain("\"envelope\":");
        json.Should().NotContain("\"message\":");
    }

    [Fact]
    public void Compose_NullDescriptor_Throws()
    {
        var sp = BuildProviderForAudit("kid-a");
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
        var sp = BuildProviderForAudit("kid-a");
        var descriptor = EncryptedDescriptor(EncryptionDomains.Audit);

        var (body, _) = EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, sp);
        var message = EncryptedBodyComposer.Decompose<SampleAuditEvent>(body, descriptor, sp);

        message.Should().NotBeNull();
    }

    [Fact]
    public void Decompose_TamperedFrame_ThrowsOnTagMismatch()
    {
        var sp = BuildProviderForAudit("kid-a");
        var descriptor = EncryptedDescriptor(EncryptionDomains.Audit);

        var (body, _) = EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, sp);
        body[^1] ^= 0xFF;

        var act = () => EncryptedBodyComposer.Decompose<SampleAuditEvent>(body, descriptor, sp);
        act.Should().Throw<Exception>(
            "AEAD tag verification must reject any tamper");
    }

    [Fact]
    public void Decompose_KidNotInKeyring_Throws()
    {
        var composeSp = BuildProviderForAudit("kid-a");
        var descriptor = EncryptedDescriptor(EncryptionDomains.Audit);
        var (body, _) = EncryptedBodyComposer.Compose(
            new SampleAuditEvent(), descriptor, composeSp);

        var decomposeSp = BuildProviderForAudit("kid-b");
        var act = () => EncryptedBodyComposer.Decompose<SampleAuditEvent>(
            body, descriptor, decomposeSp);
        act.Should().Throw<Exception>("missing kid is fatal — caller maps to DLQ");
    }

    [Fact]
    public void Decompose_TruncatedBody_ThrowsCleanly()
    {
        var sp = BuildProviderForAudit("kid-a");
        var descriptor = EncryptedDescriptor(EncryptionDomains.Audit);
        var (body, _) = EncryptedBodyComposer.Compose(new SampleAuditEvent(), descriptor, sp);

        var truncated = body.AsSpan(0, 10).ToArray();
        var act = () => EncryptedBodyComposer.Decompose<SampleAuditEvent>(
            truncated, descriptor, sp);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Decompose_EmptyBody_ThrowsCleanly()
    {
        var sp = BuildProviderForAudit("kid-a");
        var descriptor = EncryptedDescriptor(EncryptionDomains.Audit);
        var act = () => EncryptedBodyComposer.Decompose<SampleAuditEvent>(
            ReadOnlySpan<byte>.Empty, descriptor, sp);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void ReadKidFromFrame_ValidFrame_ReturnsKid()
    {
        var sp = BuildProviderForAudit("kid-a");
        var descriptor = EncryptedDescriptor(EncryptionDomains.Audit);
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
        // Version byte 2 (unknown) — the version gate must reject before
        // attempting to read the rest of the frame as a v1 kid.
        var frame = new byte[] { 2, 5, (byte)'k', (byte)'i', (byte)'d', (byte)'-', (byte)'a' };
        var act = () => EncryptedBodyComposer.ReadKidFromFrame(frame);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown encryption frame version*");
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

    private static IServiceProvider BuildProviderForAudit(string kid)
    {
        var key = RandomNumberGenerator.GetBytes(PayloadCryptoKeyring.KEY_SIZE_BYTES);
        var keyring = new PayloadCryptoKeyring(
            activeKid: kid,
            keys: new Dictionary<string, byte[]>(StringComparer.Ordinal) { [kid] = key },
            aadContext: Encoding.UTF8.GetBytes("d2/" + EncryptionDomains.Audit));
        var crypto = new PayloadCrypto(keyring);

        var services = new ServiceCollection();
        services.AddKeyedSingleton<IPayloadCrypto>(EncryptionDomains.Audit, crypto);
        return services.BuildServiceProvider();
    }
}
