// -----------------------------------------------------------------------
// <copyright file="IPayloadCrypto.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Security.Cryptography;

/// <summary>
/// AEAD payload encryption with JWKS-style multi-kid keyring. Implementations
/// MUST be safe to share across threads.
/// </summary>
/// <remarks>
/// The wire frame produced by <see cref="Encrypt"/> is self-contained: it
/// carries the version byte, the kid (so the receiver can pick the right key
/// without out-of-band coordination), the GCM nonce, and the ciphertext +
/// auth tag. <see cref="Decrypt"/> reads the kid from the frame and looks
/// it up in the keyring; tampered frames or wrong AAD produce
/// <see cref="AuthenticationTagMismatchException"/>.
/// </remarks>
public interface IPayloadCrypto
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> with the keyring's active kid
    /// and returns a self-contained frame. Each call generates a fresh
    /// 12-byte nonce via <see cref="RandomNumberGenerator"/>.
    /// </summary>
    /// <param name="plaintext">Bytes to encrypt. Empty input is allowed.</param>
    /// <returns>
    /// Frame: <c>[version=1][kid_len:1][kid:UTF-8][nonce:12][ciphertext+tag]</c>.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The keyring has been disposed.</exception>
    /// <exception cref="KidNotInKeyringException">
    /// The keyring's <see cref="PayloadCryptoKeyring.ActiveKid"/> is no
    /// longer present (programmer error — a keyring must always contain
    /// its declared active kid).
    /// </exception>
    byte[] Encrypt(ReadOnlySpan<byte> plaintext);

    /// <summary>
    /// Decrypts a frame produced by <see cref="Encrypt"/>. The kid is read
    /// from the frame; the keyring resolves it to a key.
    /// </summary>
    /// <param name="framed">A complete frame in the format above.</param>
    /// <returns>The recovered plaintext.</returns>
    /// <exception cref="ObjectDisposedException">The keyring has been disposed.</exception>
    /// <exception cref="FrameMalformedException">
    /// Frame is shorter than the minimum, or any length prefix overruns
    /// the available bytes.
    /// </exception>
    /// <exception cref="FrameVersionMismatchException">
    /// Frame's version byte is not <c>1</c>.
    /// </exception>
    /// <exception cref="KidNotInKeyringException">
    /// Frame's declared kid is not present in the current keyring.
    /// </exception>
    /// <exception cref="AuthenticationTagMismatchException">
    /// AEAD authentication failed — ciphertext tampered, AAD mismatch, or
    /// wrong key for kid.
    /// </exception>
    byte[] Decrypt(ReadOnlySpan<byte> framed);
}
