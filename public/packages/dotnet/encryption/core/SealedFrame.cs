// -----------------------------------------------------------------------
// <copyright file="SealedFrame.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Buffers.Binary;
using System.Text;

/// <summary>
/// Internal codec for the on-wire SEALED (version-2, asymmetric) encryption
/// frame. Layout:
/// <c>[version=2:1][recipient_kid_len:1][recipient_kid:UTF-8 N][eph_pub_len:2 BE][eph_pub:SPKI M][nonce:12][ciphertext+tag:K]</c>.
/// Sibling of <see cref="EncryptionFrame"/> (the symmetric version-1 codec) —
/// version dispatch is structural: each codec hard-rejects the other family's
/// version byte, so the two formats can never cross-parse.
/// </summary>
internal static class SealedFrame
{
    // Strict decoder: an invalid UTF-8 recipient kid must THROW (surface as
    // FrameMalformedException), never silently decode to U+FFFD replacement
    // characters that would then fail keyring lookup with a misleading kid.
    private static readonly UTF8Encoding sr_strictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Encodes a sealed frame around an already-encrypted ciphertext+tag span.
    /// </summary>
    /// <param name="recipientKid">The recipient kid to embed in the frame header.</param>
    /// <param name="ephemeralPublicSpki">
    /// The per-message ephemeral public key (SubjectPublicKeyInfo DER; at most
    /// <see cref="SealedFrameLayout.CONSTRAINT_MAX_EPH_PUB_LENGTH"/> bytes).
    /// </param>
    /// <param name="nonce">
    /// The nonce used for encryption (must be exactly
    /// <see cref="SealedFrameLayout.CONSTRAINT_NONCE_LENGTH"/> bytes).
    /// </param>
    /// <param name="ciphertextWithTag">The encrypted bytes followed by the auth tag.</param>
    /// <returns>The complete sealed frame.</returns>
    internal static byte[] Encode(
        string recipientKid,
        ReadOnlySpan<byte> ephemeralPublicSpki,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> ciphertextWithTag)
    {
        var kidByteCount = Encoding.UTF8.GetByteCount(recipientKid);

        if (kidByteCount is < SealedFrameLayout.CONSTRAINT_MIN_KID_LENGTH
            or > SealedFrameLayout.CONSTRAINT_MAX_KID_LENGTH)
        {
            throw new ArgumentException(
                "recipientKid UTF-8 byte length must be in " +
                $"[{SealedFrameLayout.CONSTRAINT_MIN_KID_LENGTH}, " +
                $"{SealedFrameLayout.CONSTRAINT_MAX_KID_LENGTH}].",
                nameof(recipientKid));
        }

        if (ephemeralPublicSpki.Length is < 1
            or > SealedFrameLayout.CONSTRAINT_MAX_EPH_PUB_LENGTH)
        {
            throw new ArgumentException(
                "ephemeralPublicSpki byte length must be in " +
                $"[1, {SealedFrameLayout.CONSTRAINT_MAX_EPH_PUB_LENGTH}] " +
                $"(got {ephemeralPublicSpki.Length}).",
                nameof(ephemeralPublicSpki));
        }

        if (nonce.Length != SealedFrameLayout.CONSTRAINT_NONCE_LENGTH)
        {
            throw new ArgumentException(
                $"nonce must be exactly {SealedFrameLayout.CONSTRAINT_NONCE_LENGTH} bytes " +
                $"(got {nonce.Length}).",
                nameof(nonce));
        }

        if (ciphertextWithTag.Length < SealedFrameLayout.CONSTRAINT_TAG_LENGTH)
        {
            throw new ArgumentException(
                $"ciphertextWithTag must be at least " +
                $"{SealedFrameLayout.CONSTRAINT_TAG_LENGTH} bytes (the tag).",
                nameof(ciphertextWithTag));
        }

        var totalSize = SealedFrameLayout.VERSION_LENGTH
            + SealedFrameLayout.RECIPIENT_KID_LENGTH_LENGTH
            + kidByteCount
            + SealedFrameLayout.EPH_PUB_LENGTH_LENGTH
            + ephemeralPublicSpki.Length
            + SealedFrameLayout.CONSTRAINT_NONCE_LENGTH
            + ciphertextWithTag.Length;
        var frame = new byte[totalSize];
        var span = frame.AsSpan();

        span[SealedFrameLayout.VERSION_OFFSET] = SealedFrameLayout.CURRENT_VERSION;
        span[SealedFrameLayout.RECIPIENT_KID_LENGTH_OFFSET] = (byte)kidByteCount;
        Encoding.UTF8.GetBytes(
            recipientKid,
            span[SealedFrameLayout.RECIPIENT_KID_OFFSET
                ..(SealedFrameLayout.RECIPIENT_KID_OFFSET + kidByteCount)]);

        var ephLenOffset = SealedFrameLayout.RECIPIENT_KID_OFFSET + kidByteCount;
        BinaryPrimitives.WriteUInt16BigEndian(
            span[ephLenOffset..], (ushort)ephemeralPublicSpki.Length);

        var ephOffset = ephLenOffset + SealedFrameLayout.EPH_PUB_LENGTH_LENGTH;
        ephemeralPublicSpki.CopyTo(span[ephOffset..]);

        var nonceOffset = ephOffset + ephemeralPublicSpki.Length;
        nonce.CopyTo(span[nonceOffset..]);
        ciphertextWithTag.CopyTo(
            span[(nonceOffset + SealedFrameLayout.CONSTRAINT_NONCE_LENGTH)..]);

        return frame;
    }

    /// <summary>
    /// Decodes a sealed frame, returning component spans into the input.
    /// Throws <see cref="FrameMalformedException"/> or
    /// <see cref="FrameVersionMismatchException"/> on any structural error.
    /// </summary>
    /// <param name="framed">The complete sealed frame buffer.</param>
    /// <returns>A view of the parsed components, aliasing <paramref name="framed"/>.</returns>
    internal static SealedFrameView Decode(ReadOnlySpan<byte> framed)
    {
        if (framed.Length < SealedFrameLayout.CONSTRAINT_MIN_FRAME_SIZE)
        {
            throw new FrameMalformedException(
                $"Sealed frame too short: {framed.Length} bytes " +
                $"(min {SealedFrameLayout.CONSTRAINT_MIN_FRAME_SIZE}).");
        }

        var version = framed[SealedFrameLayout.VERSION_OFFSET];

        if (version != SealedFrameLayout.CURRENT_VERSION)
            throw new FrameVersionMismatchException(version);

        int kidLength = framed[SealedFrameLayout.RECIPIENT_KID_LENGTH_OFFSET];

        if (kidLength is < SealedFrameLayout.CONSTRAINT_MIN_KID_LENGTH
            or > SealedFrameLayout.CONSTRAINT_MAX_KID_LENGTH)
        {
            throw new FrameMalformedException(
                $"Sealed frame recipient_kid_length {kidLength} is outside " +
                $"[{SealedFrameLayout.CONSTRAINT_MIN_KID_LENGTH}, " +
                $"{SealedFrameLayout.CONSTRAINT_MAX_KID_LENGTH}].");
        }

        var ephLenOffset = SealedFrameLayout.RECIPIENT_KID_OFFSET + kidLength;

        if (framed.Length < ephLenOffset + SealedFrameLayout.EPH_PUB_LENGTH_LENGTH)
        {
            throw new FrameMalformedException(
                $"Sealed frame too short for declared recipient_kid_length={kidLength}: " +
                "the eph_pub length prefix overruns the buffer.");
        }

        int ephLength = BinaryPrimitives.ReadUInt16BigEndian(framed[ephLenOffset..]);

        if (ephLength < 1)
        {
            throw new FrameMalformedException(
                "Sealed frame eph_pub_len is zero — an empty ephemeral public key " +
                "cannot exist in a valid frame.");
        }

        if (ephLength > SealedFrameLayout.CONSTRAINT_MAX_EPH_PUB_LENGTH)
        {
            throw new FrameMalformedException(
                $"Sealed frame eph_pub_len {ephLength} exceeds the cap " +
                $"{SealedFrameLayout.CONSTRAINT_MAX_EPH_PUB_LENGTH}.");
        }

        var ephOffset = ephLenOffset + SealedFrameLayout.EPH_PUB_LENGTH_LENGTH;
        var nonceOffset = ephOffset + ephLength;
        var ciphertextOffset = nonceOffset + SealedFrameLayout.CONSTRAINT_NONCE_LENGTH;

        if (framed.Length < ciphertextOffset + SealedFrameLayout.CONSTRAINT_TAG_LENGTH)
        {
            throw new FrameMalformedException(
                $"Sealed frame too short for declared eph_pub_len={ephLength}: " +
                $"have {framed.Length}, need ≥ " +
                $"{ciphertextOffset + SealedFrameLayout.CONSTRAINT_TAG_LENGTH}.");
        }

        string recipientKid;

        try
        {
            recipientKid = sr_strictUtf8.GetString(
                framed.Slice(SealedFrameLayout.RECIPIENT_KID_OFFSET, kidLength));
        }
        catch (DecoderFallbackException)
        {
            throw new FrameMalformedException("Sealed frame recipient_kid is not valid UTF-8.");
        }

        return new SealedFrameView(
            version: version,
            recipientKid: recipientKid,
            ephemeralPublicSpki: framed.Slice(ephOffset, ephLength),
            nonce: framed.Slice(nonceOffset, SealedFrameLayout.CONSTRAINT_NONCE_LENGTH),
            ciphertextWithTag: framed[ciphertextOffset..]);
    }
}
