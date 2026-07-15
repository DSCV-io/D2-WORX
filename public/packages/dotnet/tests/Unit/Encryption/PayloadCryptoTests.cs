// -----------------------------------------------------------------------
// <copyright file="PayloadCryptoTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption;

using System.Security.Cryptography;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Round-trip + AEAD-property coverage for <see cref="PayloadCrypto"/>.
/// Every test exercises the real <see cref="AesGcm"/> path with a real
/// crypto-random key.
/// </summary>
public sealed class PayloadCryptoTests
{
    [Fact]
    public void Encrypt_Decrypt_RoundTrips_NonEmpty()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        var plaintext = "hello world"u8.ToArray();

        var framed = crypto.Encrypt(plaintext);
        var roundTripped = crypto.Decrypt(framed);

        roundTripped.Should().BeEquivalentTo(plaintext);
    }

    [Fact]
    public void Encrypt_Decrypt_RoundTrips_Empty()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);

        var framed = crypto.Encrypt(ReadOnlySpan<byte>.Empty);
        var roundTripped = crypto.Decrypt(framed);

        roundTripped.Should().BeEmpty();
    }

    [Fact]
    public void Encrypt_Twice_ProducesDifferentCiphertexts()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        var plaintext = "same input"u8.ToArray();

        var first = crypto.Encrypt(plaintext);
        var second = crypto.Encrypt(plaintext);

        // Both must decrypt to the same thing.
        crypto.Decrypt(first).Should().BeEquivalentTo(plaintext);
        crypto.Decrypt(second).Should().BeEquivalentTo(plaintext);

        // But the frames must differ — fresh nonce per encrypt.
        first.Should().NotBeEquivalentTo(second);
    }

    [Fact]
    public void Encrypt_FrameContainsCorrectVersionAndKid()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        var framed = crypto.Encrypt("hi"u8);

        framed[0].Should().Be(1, "version byte must be 1");
        framed[1].Should().Be((byte)"audit-2026q2".Length, "kid_length byte must equal kid length");
        System.Text.Encoding.UTF8.GetString(framed.AsSpan(2, "audit-2026q2".Length))
            .Should().Be("audit-2026q2");
    }

    [Fact]
    public void Decrypt_TamperedCiphertextByte_FailsTagCheck()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        var framed = crypto.Encrypt("payload"u8);

        // Flip one bit of the last ciphertext+tag byte.
        framed[^1] ^= 0x01;

        var act = () => crypto.Decrypt(framed);
        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    [Fact]
    public void Decrypt_TamperedNonceByte_FailsTagCheck()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        var framed = crypto.Encrypt("payload"u8);

        // Nonce starts after [version=1][kid_len=1][kid:N].
        var nonceStart = 2 + "audit-2026q2".Length;
        framed[nonceStart] ^= 0x01;

        var act = () => crypto.Decrypt(framed);
        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    [Fact]
    public void Decrypt_FrameWithKidNotInKeyring_ThrowsKidNotInKeyring()
    {
        using var encryptingRing = TestKeyrings.SingleKey("audit-2026q1", "audit");
        var encryptingCrypto = new PayloadCrypto(encryptingRing);
        var framed = encryptingCrypto.Encrypt("payload"u8);

        // Decrypting keyring has a different kid set.
        using var decryptingRing = TestKeyrings.AuditSingleKey();
        var decryptingCrypto = new PayloadCrypto(decryptingRing);

        var act = () => decryptingCrypto.Decrypt(framed);
        act.Should().Throw<KidNotInKeyringException>().Which.Kid.Should().Be("audit-2026q1");
    }

    [Fact]
    public void Decrypt_DifferentAad_FailsTagCheck()
    {
        // Same active kid + same key bytes, different AAD context — tag must fail.
        var sharedKey = TestKeyrings.RandomKey();
        using var encryptingRing = new PayloadCryptoKeyring(
            "audit-2026q2",
            new Dictionary<string, byte[]> { ["audit-2026q2"] = sharedKey },
            TestKeyrings.AadFor("audit"));
        var encryptingCrypto = new PayloadCrypto(encryptingRing);
        var framed = encryptingCrypto.Encrypt("cross-domain attempt"u8);

        using var decryptingRing = new PayloadCryptoKeyring(
            "audit-2026q2",
            new Dictionary<string, byte[]> { ["audit-2026q2"] = sharedKey },
            TestKeyrings.AadFor("courier"));
        var decryptingCrypto = new PayloadCrypto(decryptingRing);

        var act = () => decryptingCrypto.Decrypt(framed);
        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    [Fact]
    public void Decrypt_FrameVersionNonOne_ThrowsFrameVersionMismatch()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        var framed = crypto.Encrypt("payload"u8);
        framed[0] = 99;

        var act = () => crypto.Decrypt(framed);
        act.Should().Throw<FrameVersionMismatchException>().Which.Version.Should().Be(99);
    }

    [Fact]
    public void Decrypt_FrameTooShort_ThrowsFrameMalformed()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        var act = () => crypto.Decrypt(new byte[5]);
        act.Should().Throw<FrameMalformedException>();
    }

    [Fact]
    public void Decrypt_KidLengthLies_ThrowsFrameMalformed()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        var framed = crypto.Encrypt("payload"u8);

        // Inflate kid_length so it overruns the buffer.
        framed[1] = 200;

        var act = () => crypto.Decrypt(framed);
        act.Should().Throw<FrameMalformedException>();
    }

    [Fact]
    public void Decrypt_ZeroKidLength_ThrowsFrameMalformed()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        var framed = crypto.Encrypt("payload"u8);

        framed[1] = 0;

        var act = () => crypto.Decrypt(framed);
        act.Should().Throw<FrameMalformedException>();
    }

    [Fact]
    public void Decrypt_RetiringKid_StillWorks()
    {
        // Audit-2026q1 was active when the frame was made; now it's retiring.
        var oldKey = TestKeyrings.RandomKey();
        using var encryptingRing = new PayloadCryptoKeyring(
            "audit-2026q1",
            new Dictionary<string, byte[]> { ["audit-2026q1"] = oldKey },
            TestKeyrings.AadFor("audit"));
        var framed = new PayloadCrypto(encryptingRing).Encrypt("in flight when rotated"u8);

        // New keyring has the new active kid + the old kid still as retiring.
        using var decryptingRing = new PayloadCryptoKeyring(
            "audit-2026q2",
            new Dictionary<string, byte[]>
            {
                ["audit-2026q2"] = TestKeyrings.RandomKey(),
                ["audit-2026q1"] = oldKey,
            },
            TestKeyrings.AadFor("audit"));

        var roundTripped = new PayloadCrypto(decryptingRing).Decrypt(framed);
        roundTripped.Should().BeEquivalentTo("in flight when rotated"u8.ToArray());
    }

    [Fact]
    public void Encrypt_AfterKeyringDisposed_Throws()
    {
        var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        ring.Dispose();

        var act = () => crypto.Encrypt("late"u8);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Decrypt_AfterKeyringDisposed_Throws()
    {
        var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        var framed = crypto.Encrypt("payload"u8);
        ring.Dispose();

        var act = () => crypto.Decrypt(framed);
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Ctor_NullKeyring_Throws()
    {
        var act = () => new PayloadCrypto(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Encrypt_LargePayload_RoundTrips()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        var plaintext = RandomNumberGenerator.GetBytes(1024 * 1024); // 1 MiB

        var framed = crypto.Encrypt(plaintext);
        var roundTripped = crypto.Decrypt(framed);

        roundTripped.Should().BeEquivalentTo(plaintext);
    }

    [Fact]
    public void Encrypt_Concurrent_AllRoundTrip()
    {
        // Per-call AesGcm instantiation means parallel calls on a single
        // PayloadCrypto are safe. Verify by stress-encrypting from many
        // threads and ensuring every round-trip succeeds.
        using var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        var plaintexts = Enumerable.Range(0, 200)
            .Select(i => System.Text.Encoding.UTF8.GetBytes($"msg-{i}"))
            .ToArray();

        var roundTripped = plaintexts
            .AsParallel()
            .WithDegreeOfParallelism(8)
            .Select(p => crypto.Decrypt(crypto.Encrypt(p)))
            .ToArray();

        roundTripped.Should().BeEquivalentTo(plaintexts);
    }

    [Fact]
    public void ToString_DoesNotLeakState()
    {
        using var ring = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(ring);
        crypto.ToString().Should().Be("PayloadCrypto");
    }
}
