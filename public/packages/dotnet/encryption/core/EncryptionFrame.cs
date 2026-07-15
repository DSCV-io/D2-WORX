// -----------------------------------------------------------------------
// <copyright file="EncryptionFrame.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Text;

/// <summary>
/// Internal codec for the on-wire encryption frame.
/// Layout: <c>[version:1][kid_len:1][kid:UTF-8 N bytes][nonce:12][ciphertext+tag:M bytes]</c>.
/// </summary>
internal static class EncryptionFrame
{
    /// <summary>
    /// GCM nonce length in bytes. Used by <see cref="PayloadCrypto"/> when
    /// allocating the nonce span.
    /// </summary>
    internal const int NONCE_SIZE = 12;

    /// <summary>
    /// GCM authentication-tag length in bytes. Used by <see cref="PayloadCrypto"/>
    /// when sizing the ciphertext+tag buffer.
    /// </summary>
    internal const int TAG_SIZE = 16;

    private const byte _CURRENT_VERSION = 1;
    private const int _VERSION_SIZE = 1;
    private const int _KID_LENGTH_PREFIX_SIZE = 1;

    /// <summary>
    /// Smallest possible frame: 1-byte version + 1-byte kid_length (validated
    /// to ≥ 1 separately) + 12-byte nonce + 16-byte tag + 0-byte plaintext.
    /// </summary>
    private const int _MIN_FRAME_SIZE =
        _VERSION_SIZE + _KID_LENGTH_PREFIX_SIZE + NONCE_SIZE + TAG_SIZE;

    /// <summary>
    /// Encodes a frame around an already-encrypted ciphertext+tag span.
    /// </summary>
    /// <param name="kid">The kid to embed in the frame header.</param>
    /// <param name="nonce">
    /// The nonce used for encryption (must be exactly <see cref="NONCE_SIZE"/> bytes).
    /// </param>
    /// <param name="ciphertextWithTag">The encrypted bytes followed by the auth tag.</param>
    /// <returns>The complete frame.</returns>
    internal static byte[] Encode(
        string kid, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> ciphertextWithTag)
    {
        var kidByteCount = Encoding.UTF8.GetByteCount(kid);
        if (kidByteCount is < PayloadCryptoKeyring.MIN_KID_LENGTH
            or > PayloadCryptoKeyring.MAX_KID_LENGTH)
        {
            throw new ArgumentException(
                "kid UTF-8 byte length must be in " +
                $"[{PayloadCryptoKeyring.MIN_KID_LENGTH}, {PayloadCryptoKeyring.MAX_KID_LENGTH}].",
                nameof(kid));
        }

        if (nonce.Length != NONCE_SIZE)
        {
            throw new ArgumentException(
                $"nonce must be exactly {NONCE_SIZE} bytes (got {nonce.Length}).", nameof(nonce));
        }

        if (ciphertextWithTag.Length < TAG_SIZE)
        {
            throw new ArgumentException(
                $"ciphertextWithTag must be at least {TAG_SIZE} bytes (the tag).",
                nameof(ciphertextWithTag));
        }

        var totalSize = _VERSION_SIZE + _KID_LENGTH_PREFIX_SIZE
            + kidByteCount + NONCE_SIZE + ciphertextWithTag.Length;
        var frame = new byte[totalSize];
        var span = frame.AsSpan();

        span[0] = _CURRENT_VERSION;
        span[1] = (byte)kidByteCount;
        Encoding.UTF8.GetBytes(kid, span[2..(2 + kidByteCount)]);
        nonce.CopyTo(span[(2 + kidByteCount)..]);
        ciphertextWithTag.CopyTo(span[(2 + kidByteCount + NONCE_SIZE)..]);

        return frame;
    }

    /// <summary>
    /// Decodes a frame, returning component spans into the input. Throws
    /// <see cref="FrameMalformedException"/> or
    /// <see cref="FrameVersionMismatchException"/> on any structural error.
    /// </summary>
    /// <param name="framed">The complete frame buffer.</param>
    /// <returns>A view of the parsed components, aliasing <paramref name="framed"/>.</returns>
    internal static FrameView Decode(ReadOnlySpan<byte> framed)
    {
        if (framed.Length < _MIN_FRAME_SIZE)
        {
            throw new FrameMalformedException(
                $"Frame too short: {framed.Length} bytes (min {_MIN_FRAME_SIZE}).");
        }

        var version = framed[0];
        if (version != _CURRENT_VERSION)
            throw new FrameVersionMismatchException(version);

        int kidLength = framed[1];
        if (kidLength < PayloadCryptoKeyring.MIN_KID_LENGTH)
        {
            throw new FrameMalformedException(
                $"Frame kid_length {kidLength} is below minimum " +
                $"{PayloadCryptoKeyring.MIN_KID_LENGTH}.");
        }

        var headerSize = _VERSION_SIZE + _KID_LENGTH_PREFIX_SIZE + kidLength + NONCE_SIZE;
        if (framed.Length < headerSize + TAG_SIZE)
        {
            throw new FrameMalformedException(
                $"Frame too short for declared kid_length={kidLength}: " +
                $"have {framed.Length}, need ≥ {headerSize + TAG_SIZE}.");
        }

        string kid;
        try
        {
            kid = Encoding.UTF8.GetString(framed.Slice(2, kidLength));
        }
        catch (DecoderFallbackException)
        {
            throw new FrameMalformedException("Frame kid is not valid UTF-8.");
        }

        return new FrameView(
            version: version,
            kid: kid,
            nonce: framed.Slice(2 + kidLength, NONCE_SIZE),
            ciphertextWithTag: framed[(2 + kidLength + NONCE_SIZE)..]);
    }
}
