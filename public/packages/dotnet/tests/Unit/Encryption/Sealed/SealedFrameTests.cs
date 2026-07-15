// -----------------------------------------------------------------------
// <copyright file="SealedFrameTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.Sealed;

using System.Buffers.Binary;
using System.Text;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using Xunit;

/// <summary>
/// Adversarial coverage of the sealed (version-2) frame codec: encode/decode
/// round-trips, the truncation-at-every-boundary matrix, length-prefix
/// overruns, kid bounds, strict UTF-8, and structural version dispatch
/// against the symmetric (version-1) codec.
/// </summary>
public sealed class SealedFrameTests
{
    private static readonly byte[] sr_ephPub = new byte[91];
    private static readonly byte[] sr_nonce = new byte[SealedFrameLayout.CONSTRAINT_NONCE_LENGTH];
    private static readonly byte[] sr_ctWithTag = new byte[24];

    static SealedFrameTests()
    {
        // Deterministic non-zero filler so slices are distinguishable.
        for (var i = 0; i < sr_ephPub.Length; i++) sr_ephPub[i] = (byte)(i + 1);
        for (var i = 0; i < sr_nonce.Length; i++) sr_nonce[i] = (byte)(0xA0 + i);
        for (var i = 0; i < sr_ctWithTag.Length; i++) sr_ctWithTag[i] = (byte)(0xC0 + i);
    }

    // ---------------------------------------------------------------
    // Encode → Decode round-trip.
    // ---------------------------------------------------------------

    [Fact]
    public void EncodeDecode_RoundTripsAllComponents()
    {
        var frame = SealedFrame.Encode("seal-kid-1", sr_ephPub, sr_nonce, sr_ctWithTag);

        var view = SealedFrame.Decode(frame);

        view.Version.Should().Be(SealedFrameLayout.CURRENT_VERSION);
        view.RecipientKid.Should().Be("seal-kid-1");
        view.EphemeralPublicSpki.ToArray().Should().Equal(sr_ephPub);
        view.Nonce.ToArray().Should().Equal(sr_nonce);
        view.CiphertextWithTag.ToArray().Should().Equal(sr_ctWithTag);
    }

    [Fact]
    public void Encode_VersionByteIsTwo()
    {
        var frame = SealedFrame.Encode("k", sr_ephPub, sr_nonce, sr_ctWithTag);

        frame[SealedFrameLayout.VERSION_OFFSET].Should().Be(SealedFrameLayout.CURRENT_VERSION);
    }

    [Fact]
    public void Encode_EphPubLengthIsBigEndian()
    {
        var frame = SealedFrame.Encode("k", sr_ephPub, sr_nonce, sr_ctWithTag);

        // kid "k" is 1 byte → eph_pub_len sits right after [ver][len][kid].
        var ephLenOffset = SealedFrameLayout.RECIPIENT_KID_OFFSET + 1;
        var declared = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(ephLenOffset));

        declared.Should().Be((ushort)sr_ephPub.Length);
    }

    [Fact]
    public void EncodeDecode_MultiByteUtf8Kid_RoundTrips()
    {
        var frame = SealedFrame.Encode("🔑-kid", sr_ephPub, sr_nonce, sr_ctWithTag);

        SealedFrame.Decode(frame).RecipientKid.Should().Be("🔑-kid");
    }

    // ---------------------------------------------------------------
    // Encode input validation.
    // ---------------------------------------------------------------

    [Fact]
    public void Encode_KidTooLong_Throws()
    {
        var kid = new string('k', SealedFrameLayout.CONSTRAINT_MAX_KID_LENGTH + 1);
        var act = () => SealedFrame.Encode(kid, sr_ephPub, sr_nonce, sr_ctWithTag);

        act.Should().Throw<ArgumentException>().WithParameterName("recipientKid");
    }

    [Fact]
    public void Encode_KidEmpty_Throws()
    {
        var act = () => SealedFrame.Encode(string.Empty, sr_ephPub, sr_nonce, sr_ctWithTag);

        act.Should().Throw<ArgumentException>().WithParameterName("recipientKid");
    }

    [Fact]
    public void Encode_EphPubEmpty_Throws()
    {
        var act = () => SealedFrame.Encode(
            "k", ReadOnlySpan<byte>.Empty, sr_nonce, sr_ctWithTag);

        act.Should().Throw<ArgumentException>().WithParameterName("ephemeralPublicSpki");
    }

    [Fact]
    public void Encode_EphPubOverCap_Throws()
    {
        var oversized = new byte[SealedFrameLayout.CONSTRAINT_MAX_EPH_PUB_LENGTH + 1];
        var act = () => SealedFrame.Encode("k", oversized, sr_nonce, sr_ctWithTag);

        act.Should().Throw<ArgumentException>().WithParameterName("ephemeralPublicSpki");
    }

    [Fact]
    public void Encode_NonceWrongLength_Throws()
    {
        var shortNonce = new byte[SealedFrameLayout.CONSTRAINT_NONCE_LENGTH - 1];
        var act = () => SealedFrame.Encode("k", sr_ephPub, shortNonce, sr_ctWithTag);

        act.Should().Throw<ArgumentException>().WithParameterName("nonce");
    }

    [Fact]
    public void Encode_CiphertextShorterThanTag_Throws()
    {
        var tooShort = new byte[SealedFrameLayout.CONSTRAINT_TAG_LENGTH - 1];
        var act = () => SealedFrame.Encode("k", sr_ephPub, sr_nonce, tooShort);

        act.Should().Throw<ArgumentException>().WithParameterName("ciphertextWithTag");
    }

    // ---------------------------------------------------------------
    // Decode — version dispatch (structural).
    // ---------------------------------------------------------------

    [Fact]
    public void Decode_V1Frame_ThrowsFrameVersionMismatch()
    {
        // A structurally-valid SYMMETRIC frame — the sealed decoder must
        // reject it on the version byte, never mis-parse it.
        using var ring = TestKeyrings.AuditSingleKey();
        var v1Frame = new PayloadCrypto(ring).Encrypt("payload"u8);

        var act = () => { _ = SealedFrame.Decode(v1Frame).Version; };

        act.Should().Throw<FrameVersionMismatchException>()
            .Which.Version.Should().Be(1);
    }

    [Fact]
    public void Decode_UnknownVersion3_Throws()
    {
        var frame = SealedFrame.Encode("k", sr_ephPub, sr_nonce, sr_ctWithTag);
        frame[SealedFrameLayout.VERSION_OFFSET] = 3;

        var act = () => { _ = SealedFrame.Decode(frame).Version; };

        act.Should().Throw<FrameVersionMismatchException>()
            .Which.Version.Should().Be(3);
    }

    // ---------------------------------------------------------------
    // Decode — malformation matrix.
    // ---------------------------------------------------------------

    [Fact]
    public void Decode_Empty_Throws()
    {
        var act = () => { _ = SealedFrame.Decode(ReadOnlySpan<byte>.Empty).Version; };

        act.Should().Throw<FrameMalformedException>().WithMessage("*too short*");
    }

    [Fact]
    public void Decode_BelowMinFrameSize_Throws()
    {
        var tooShort = new byte[SealedFrameLayout.CONSTRAINT_MIN_FRAME_SIZE - 1];
        tooShort[0] = SealedFrameLayout.CURRENT_VERSION;

        var act = () => { _ = SealedFrame.Decode(tooShort).Version; };

        act.Should().Throw<FrameMalformedException>().WithMessage("*too short*");
    }

    [Fact]
    public void Decode_KidLengthZero_Throws()
    {
        var frame = SealedFrame.Encode("k", sr_ephPub, sr_nonce, sr_ctWithTag);
        frame[SealedFrameLayout.RECIPIENT_KID_LENGTH_OFFSET] = 0;

        var act = () => { _ = SealedFrame.Decode(frame).Version; };

        act.Should().Throw<FrameMalformedException>().WithMessage("*recipient_kid_length*");
    }

    [Fact]
    public void Decode_KidLengthOverMax_Throws()
    {
        var frame = SealedFrame.Encode("k", sr_ephPub, sr_nonce, sr_ctWithTag);
        frame[SealedFrameLayout.RECIPIENT_KID_LENGTH_OFFSET] =
            SealedFrameLayout.CONSTRAINT_MAX_KID_LENGTH + 1;

        var act = () => { _ = SealedFrame.Decode(frame).Version; };

        act.Should().Throw<FrameMalformedException>().WithMessage("*recipient_kid_length*");
    }

    [Fact]
    public void Decode_TruncatedAtKid_Throws()
    {
        // Min-size buffer whose declared kid length pushes the eph_pub
        // length prefix past the end.
        var frame = new byte[SealedFrameLayout.CONSTRAINT_MIN_FRAME_SIZE];
        frame[SealedFrameLayout.VERSION_OFFSET] = SealedFrameLayout.CURRENT_VERSION;
        frame[SealedFrameLayout.RECIPIENT_KID_LENGTH_OFFSET] =
            SealedFrameLayout.CONSTRAINT_MAX_KID_LENGTH;

        var act = () => { _ = SealedFrame.Decode(frame).Version; };

        act.Should().Throw<FrameMalformedException>().WithMessage("*overruns*");
    }

    [Fact]
    public void Decode_EphPubLenZero_Throws()
    {
        var frame = SealedFrame.Encode("k", sr_ephPub, sr_nonce, sr_ctWithTag);
        var ephLenOffset = SealedFrameLayout.RECIPIENT_KID_OFFSET + 1;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(ephLenOffset), 0);

        var act = () => { _ = SealedFrame.Decode(frame).Version; };

        act.Should().Throw<FrameMalformedException>().WithMessage("*eph_pub_len is zero*");
    }

    [Fact]
    public void Decode_EphPubLenExceedsCap_Throws()
    {
        var frame = SealedFrame.Encode("k", sr_ephPub, sr_nonce, sr_ctWithTag);
        var ephLenOffset = SealedFrameLayout.RECIPIENT_KID_OFFSET + 1;
        BinaryPrimitives.WriteUInt16BigEndian(
            frame.AsSpan(ephLenOffset),
            SealedFrameLayout.CONSTRAINT_MAX_EPH_PUB_LENGTH + 1);

        var act = () => { _ = SealedFrame.Decode(frame).Version; };

        act.Should().Throw<FrameMalformedException>().WithMessage("*exceeds the cap*");
    }

    [Fact]
    public void Decode_EphPubLenOverrunsBuffer_Throws()
    {
        var frame = SealedFrame.Encode("k", sr_ephPub, sr_nonce, sr_ctWithTag);
        var ephLenOffset = SealedFrameLayout.RECIPIENT_KID_OFFSET + 1;

        // Within the cap, but larger than the actual remaining bytes.
        BinaryPrimitives.WriteUInt16BigEndian(
            frame.AsSpan(ephLenOffset),
            SealedFrameLayout.CONSTRAINT_MAX_EPH_PUB_LENGTH);

        var act = () => { _ = SealedFrame.Decode(frame).Version; };

        act.Should().Throw<FrameMalformedException>().WithMessage("*too short*");
    }

    [Fact]
    public void Decode_TruncatedAtNonce_Throws()
    {
        var frame = SealedFrame.Encode("k", sr_ephPub, sr_nonce, sr_ctWithTag);
        var headerThroughEph = SealedFrameLayout.RECIPIENT_KID_OFFSET + 1
            + SealedFrameLayout.EPH_PUB_LENGTH_LENGTH + sr_ephPub.Length;
        var truncated = frame.AsSpan(0, headerThroughEph + 5).ToArray();

        // Keep above the absolute minimum so the version gate is reached.
        truncated.Length.Should().BeGreaterThan(SealedFrameLayout.CONSTRAINT_MIN_FRAME_SIZE);

        var act = () => { _ = SealedFrame.Decode(truncated).Version; };

        act.Should().Throw<FrameMalformedException>().WithMessage("*too short*");
    }

    [Fact]
    public void Decode_TruncatedInsideTag_Throws()
    {
        var frame = SealedFrame.Encode("k", sr_ephPub, sr_nonce, sr_ctWithTag);
        var truncated = frame.AsSpan(0, frame.Length - sr_ctWithTag.Length).ToArray();

        var act = () => { _ = SealedFrame.Decode(truncated).Version; };

        act.Should().Throw<FrameMalformedException>().WithMessage("*too short*");
    }

    [Fact]
    public void Decode_KidNotUtf8_Throws()
    {
        var frame = SealedFrame.Encode("kk", sr_ephPub, sr_nonce, sr_ctWithTag);

        // 0xFF is never valid in UTF-8.
        frame[SealedFrameLayout.RECIPIENT_KID_OFFSET] = 0xFF;

        var act = () => { _ = SealedFrame.Decode(frame).Version; };

        act.Should().Throw<FrameMalformedException>().WithMessage("*not valid UTF-8*");
    }

    [Fact]
    public void Decode_MinimalValidFrame_Succeeds()
    {
        // 1-byte kid + 1-byte eph_pub = exactly the min frame size.
        var frame = SealedFrame.Encode(
            "k",
            new byte[] { 0x42 },
            sr_nonce,
            new byte[SealedFrameLayout.CONSTRAINT_TAG_LENGTH]);

        frame.Length.Should().Be(SealedFrameLayout.CONSTRAINT_MIN_FRAME_SIZE);

        var view = SealedFrame.Decode(frame);

        view.RecipientKid.Should().Be("k");
        view.EphemeralPublicSpki.Length.Should().Be(1);
        view.CiphertextWithTag.Length.Should().Be(SealedFrameLayout.CONSTRAINT_TAG_LENGTH);
    }

    [Fact]
    public void SymmetricDecode_V2Frame_ThrowsFrameVersionMismatch()
    {
        // The symmetric (version-1) decoder must hard-reject a sealed frame
        // on its version byte — the regression pin that the v1 path stayed
        // byte-for-byte unchanged in behavior.
        var sealedFrame = SealedFrame.Encode("k", sr_ephPub, sr_nonce, sr_ctWithTag);

        var act = () => { _ = EncryptionFrame.Decode(sealedFrame).Version; };

        act.Should().Throw<FrameVersionMismatchException>()
            .Which.Version.Should().Be(SealedFrameLayout.CURRENT_VERSION);
    }

    [Fact]
    public void Decode_KidBytesMatchEncodedUtf8()
    {
        const string kid = "seal-2026q3";
        var frame = SealedFrame.Encode(kid, sr_ephPub, sr_nonce, sr_ctWithTag);

        var kidBytes = frame.AsSpan(
            SealedFrameLayout.RECIPIENT_KID_OFFSET,
            Encoding.UTF8.GetByteCount(kid));

        kidBytes.ToArray().Should().Equal(Encoding.UTF8.GetBytes(kid));
    }
}
