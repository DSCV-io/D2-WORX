// -----------------------------------------------------------------------
// <copyright file="SealOpenRoundTripTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.Sealed;

using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Adversarial coverage of the full seal → open path over real P-256
/// material: round-trips, wrong-recipient rejection (the service-identity
/// AEAD binding), the tamper matrix, version dispatch across the symmetric
/// and sealed modes, forward-secrecy properties, concurrency, and the
/// opener's dispose lifecycle.
/// </summary>
public sealed class SealOpenRoundTripTests
{
    private static readonly byte[] sr_payload =
        Encoding.UTF8.GetBytes("{\"sample\":\"sealed-payload\"}");

    // ---------------------------------------------------------------
    // Round-trips (happy + boundary).
    // ---------------------------------------------------------------

    [Fact]
    public void SealOpen_RoundTrips_Plaintext()
    {
        var (sealer, opener) = SealedTestKeys.SealerOpenerPair("audit", "seal-kid-1");

        var framed = sealer.Seal(sr_payload);
        var opened = opener.Open(framed);

        opened.Should().Equal(sr_payload);
    }

    [Fact]
    public void SealOpen_EmptyPlaintext_RoundTrips()
    {
        var (sealer, opener) = SealedTestKeys.SealerOpenerPair("audit", "seal-kid-1");

        var framed = sealer.Seal(ReadOnlySpan<byte>.Empty);
        var opened = opener.Open(framed);

        opened.Should().BeEmpty();
    }

    [Fact]
    public void SealOpen_LargePayload_RoundTrips()
    {
        var (sealer, opener) = SealedTestKeys.SealerOpenerPair("audit", "seal-kid-1");
        var large = RandomNumberGenerator.GetBytes(1024 * 1024);

        var framed = sealer.Seal(large);
        var opened = opener.Open(framed);

        opened.Should().Equal(large);
    }

    [Fact]
    public void Seal_FrameCarriesVersion2AndActiveKid()
    {
        var (sealer, _) = SealedTestKeys.SealerOpenerPair("audit", "seal-kid-1");

        var framed = sealer.Seal(sr_payload);

        framed[0].Should().Be(SealedFrameLayout.CURRENT_VERSION);
        SealedFrame.Decode(framed).RecipientKid.Should().Be("seal-kid-1");
    }

    [Fact]
    public void Open_RetiringKidFrame_StillOpens()
    {
        // Overlap guarantee: the private keyring holds active + retiring;
        // a frame sealed under the retiring kid still opens.
        var retiring = SealedTestKeys.GenerateKeypair();
        var active = SealedTestKeys.GenerateKeypair();
        var sealerUnderRetiring = new PayloadSealer(
            SealedTestKeys.PublicKeyring("audit", "kid-old", retiring));
        using var privateRing = new RecipientPrivateKeyring(
            "audit",
            new Dictionary<string, byte[]>
            {
                ["kid-new"] = active.PrivatePkcs8,
                ["kid-old"] = retiring.PrivatePkcs8,
            });
        var opener = new PayloadOpener(privateRing);

        var framed = sealerUnderRetiring.Seal(sr_payload);

        opener.Open(framed).Should().Equal(sr_payload);
    }

    // ---------------------------------------------------------------
    // Wrong recipient (the service-identity binding).
    // ---------------------------------------------------------------

    [Fact]
    public void Open_FrameSealedToOtherService_ThrowsAuthenticationTagMismatch()
    {
        // Different keypairs AND different service ids — the ordinary
        // wrong-recipient case.
        var (sealerToAudit, _) = SealedTestKeys.SealerOpenerPair("audit", "kid-a");
        var courier = SealedTestKeys.GenerateKeypair();
        using var courierRing = SealedTestKeys.PrivateKeyring("courier", "kid-a", courier);
        var courierOpener = new PayloadOpener(courierRing);

        var framed = sealerToAudit.Seal(sr_payload);

        var act = () => courierOpener.Open(framed);

        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    [Fact]
    public void Open_SameKeypairDifferentServiceId_Rejects()
    {
        // The SAME keypair registered under two service ids: the binding is
        // the SERVICE identity (AAD + HKDF info/salt), not the key
        // coincidence — opening under a different id must fail.
        var keypair = SealedTestKeys.GenerateKeypair();
        var sealerToAudit = new PayloadSealer(
            SealedTestKeys.PublicKeyring("audit", "kid-a", keypair));
        using var sameKeyOtherService = SealedTestKeys.PrivateKeyring(
            "courier", "kid-a", keypair);
        var otherOpener = new PayloadOpener(sameKeyOtherService);

        var framed = sealerToAudit.Seal(sr_payload);

        var act = () => otherOpener.Open(framed);

        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    [Fact]
    public void Open_MismatchedKeypairSameKid_ThrowsAuthenticationTagMismatch()
    {
        // Same service id + same kid, but the private key does not match the
        // public key that sealed — the misconfigured-pair case the startup
        // self-check exists to catch.
        var sealingPair = SealedTestKeys.GenerateKeypair();
        var wrongPair = SealedTestKeys.GenerateKeypair();
        var sealer = new PayloadSealer(
            SealedTestKeys.PublicKeyring("audit", "kid-a", sealingPair));
        using var wrongRing = SealedTestKeys.PrivateKeyring("audit", "kid-a", wrongPair);
        var opener = new PayloadOpener(wrongRing);

        var framed = sealer.Seal(sr_payload);

        var act = () => opener.Open(framed);

        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    // ---------------------------------------------------------------
    // Tamper matrix.
    // ---------------------------------------------------------------

    [Fact]
    public void Open_TamperedCiphertextByte_Throws()
    {
        var (sealer, opener) = SealedTestKeys.SealerOpenerPair("audit", "kid-a");
        var framed = sealer.Seal(sr_payload);

        // Last byte is inside the tag; second-to-last-16 is ciphertext — flip
        // a byte firmly inside the ciphertext region.
        framed[^(SealedFrameLayout.CONSTRAINT_TAG_LENGTH + 1)] ^= 0xFF;

        var act = () => opener.Open(framed);

        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    [Fact]
    public void Open_TamperedTagByte_Throws()
    {
        var (sealer, opener) = SealedTestKeys.SealerOpenerPair("audit", "kid-a");
        var framed = sealer.Seal(sr_payload);

        framed[^1] ^= 0xFF;

        var act = () => opener.Open(framed);

        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    [Fact]
    public void Open_TamperedNonce_Throws()
    {
        var (sealer, opener) = SealedTestKeys.SealerOpenerPair("audit", "kid-a");
        var framed = sealer.Seal(sr_payload);

        // Nonce sits immediately before ct+tag; ct = payload + tag.
        var nonceOffset = framed.Length
            - sr_payload.Length
            - SealedFrameLayout.CONSTRAINT_TAG_LENGTH
            - SealedFrameLayout.CONSTRAINT_NONCE_LENGTH;
        framed[nonceOffset] ^= 0xFF;

        var act = () => opener.Open(framed);

        act.Should().Throw<AuthenticationTagMismatchException>();
    }

    [Fact]
    public void Open_TamperedEphemeralPublic_Throws()
    {
        var (sealer, opener) = SealedTestKeys.SealerOpenerPair("audit", "kid-a");
        var framed = sealer.Seal(sr_payload);

        // The eph_pub DER sits after [ver][kid_len]["kid-a"][len:2]. Flip a
        // byte in the middle of the encoded point. Depending on where the
        // flip lands the failure surfaces as a structural import failure, a
        // derivation failure, or an AEAD tag mismatch — every arm is a hard
        // throw; none may decrypt.
        var view = SealedFrame.Decode(framed);
        var ephOffset = SealedFrameLayout.RECIPIENT_KID_OFFSET
            + Encoding.UTF8.GetByteCount(view.RecipientKid)
            + SealedFrameLayout.EPH_PUB_LENGTH_LENGTH;
        framed[ephOffset + (view.EphemeralPublicSpki.Length / 2)] ^= 0xFF;

        var act = () => opener.Open(framed);

        act.Should().Throw<Exception>()
            .Which.Should().Match(ex =>
                ex is AuthenticationTagMismatchException
                || ex is FrameMalformedException
                || ex is CryptographicException);
    }

    [Fact]
    public void Open_TamperedRecipientKid_ThrowsKidNotInKeyringOrTagMismatch()
    {
        var (sealer, opener) = SealedTestKeys.SealerOpenerPair("audit", "kid-a");
        var framed = sealer.Seal(sr_payload);

        // Flip a kid byte to another valid ASCII char → unknown kid.
        framed[SealedFrameLayout.RECIPIENT_KID_OFFSET] ^= 0x01;

        var act = () => opener.Open(framed);

        act.Should().Throw<Exception>()
            .Which.Should().Match(ex =>
                ex is KidNotInKeyringException
                || ex is AuthenticationTagMismatchException);
    }

    [Fact]
    public void Open_UnknownKid_ThrowsKidNotInKeyring()
    {
        var keypair = SealedTestKeys.GenerateKeypair();
        var sealer = new PayloadSealer(
            SealedTestKeys.PublicKeyring("audit", "kid-unknown-to-opener", keypair));
        using var ring = SealedTestKeys.PrivateKeyring("audit", "kid-b", keypair);
        var opener = new PayloadOpener(ring);

        var framed = sealer.Seal(sr_payload);

        var act = () => opener.Open(framed);

        act.Should().Throw<KidNotInKeyringException>()
            .Which.Kid.Should().Be("kid-unknown-to-opener");
    }

    // ---------------------------------------------------------------
    // Version dispatch across the two modes.
    // ---------------------------------------------------------------

    [Fact]
    public void Open_V1SymmetricFrame_ThrowsFrameVersionMismatch()
    {
        var (_, opener) = SealedTestKeys.SealerOpenerPair("audit", "kid-a");
        using var symmetricRing = TestKeyrings.AuditSingleKey();
        var v1Frame = new PayloadCrypto(symmetricRing).Encrypt(sr_payload);

        var act = () => opener.Open(v1Frame);

        act.Should().Throw<FrameVersionMismatchException>()
            .Which.Version.Should().Be(1);
    }

    [Fact]
    public void SymmetricDecrypt_V2SealedFrame_ThrowsFrameVersionMismatch()
    {
        // Regression pin: the v1 symmetric path is UNCHANGED — it hard-rejects
        // a sealed frame on the version byte, never mis-parses it.
        var (sealer, _) = SealedTestKeys.SealerOpenerPair("audit", "kid-a");
        using var symmetricRing = TestKeyrings.AuditSingleKey();
        var crypto = new PayloadCrypto(symmetricRing);

        var sealedFrame = sealer.Seal(sr_payload);

        var act = () => crypto.Decrypt(sealedFrame);

        act.Should().Throw<FrameVersionMismatchException>()
            .Which.Version.Should().Be(SealedFrameLayout.CURRENT_VERSION);
    }

    [Fact]
    public void Open_UnknownVersion3_Throws()
    {
        var (sealer, opener) = SealedTestKeys.SealerOpenerPair("audit", "kid-a");
        var framed = sealer.Seal(sr_payload);
        framed[0] = 3;

        var act = () => opener.Open(framed);

        act.Should().Throw<FrameVersionMismatchException>()
            .Which.Version.Should().Be(3);
    }

    // ---------------------------------------------------------------
    // Forward-secrecy pins.
    // ---------------------------------------------------------------

    [Fact]
    public void Seal_TwoCallsSamePlaintext_ProduceDifferentEphemeralAndCiphertext()
    {
        var (sealer, _) = SealedTestKeys.SealerOpenerPair("audit", "kid-a");

        var first = sealer.Seal(sr_payload);
        var second = sealer.Seal(sr_payload);

        first.Should().NotEqual(second);
        SealedFrame.Decode(first).EphemeralPublicSpki.ToArray()
            .Should().NotEqual(SealedFrame.Decode(second).EphemeralPublicSpki.ToArray());
        SealedFrame.Decode(first).Nonce.ToArray()
            .Should().NotEqual(SealedFrame.Decode(second).Nonce.ToArray());
    }

    // ---------------------------------------------------------------
    // Sealer misuse.
    // ---------------------------------------------------------------

    [Fact]
    public void Sealer_NullKeyring_Throws()
    {
        var act = () => new PayloadSealer(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Opener_NullKeyring_Throws()
    {
        var act = () => new PayloadOpener(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SealerToString_NeverContainsKeyMaterial()
    {
        var (sealer, opener) = SealedTestKeys.SealerOpenerPair("audit", "kid-a");

        sealer.ToString().Should().Be(nameof(PayloadSealer));
        opener.ToString().Should().Be(nameof(PayloadOpener));
    }

    // ---------------------------------------------------------------
    // Concurrency.
    // ---------------------------------------------------------------

    [Fact]
    public void Seal_ConcurrentCallers_AllFramesOpen()
    {
        var (sealer, opener) = SealedTestKeys.SealerOpenerPair("audit", "kid-a");
        var frames = new byte[32][];

        Parallel.For(0, frames.Length, i =>
        {
            frames[i] = sealer.Seal(sr_payload);
        });

        foreach (var frame in frames)
            opener.Open(frame).Should().Equal(sr_payload);
    }

    [Fact]
    public void Open_ConcurrentCallers_AllSucceed()
    {
        var (sealer, opener) = SealedTestKeys.SealerOpenerPair("audit", "kid-a");
        var framed = sealer.Seal(sr_payload);

        Parallel.For(0, 32, _ =>
        {
            opener.Open(framed).Should().Equal(sr_payload);
        });
    }

    // ---------------------------------------------------------------
    // Dispose lifecycle (opener over a disposed keyring).
    // ---------------------------------------------------------------

    [Fact]
    public void Open_AfterKeyringDispose_ThrowsObjectDisposed()
    {
        var keypair = SealedTestKeys.GenerateKeypair();
        var sealer = new PayloadSealer(SealedTestKeys.PublicKeyring("audit", "kid-a", keypair));
        var ring = SealedTestKeys.PrivateKeyring("audit", "kid-a", keypair);
        var opener = new PayloadOpener(ring);
        var framed = sealer.Seal(sr_payload);

        ring.Dispose();

        var act = () => opener.Open(framed);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Open_ConcurrentWithDispose_ThrowsCleanly()
    {
        // Hammer Open on N threads while disposing the keyring mid-flight:
        // every call either succeeds (pre-dispose) or throws
        // ObjectDisposedException — never a torn/undefined outcome like a
        // partial decrypt under zeroed key bytes silently succeeding.
        var keypair = SealedTestKeys.GenerateKeypair();
        var sealer = new PayloadSealer(SealedTestKeys.PublicKeyring("audit", "kid-a", keypair));
        var ring = SealedTestKeys.PrivateKeyring("audit", "kid-a", keypair);
        var opener = new PayloadOpener(ring);
        var framed = sealer.Seal(sr_payload);

        var tasks = Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            try
            {
                var opened = opener.Open(framed);
                opened.Should().Equal(sr_payload);
            }
            catch (ObjectDisposedException)
            {
                // The clean post-dispose outcome.
            }
            catch (AuthenticationTagMismatchException)
            {
                // A decrypt racing the zeroize loses AEAD authentication —
                // a hard throw, never a silent wrong-plaintext success.
            }
        })).ToArray();

        ring.Dispose();
        await Task.WhenAll(tasks);
    }
}
