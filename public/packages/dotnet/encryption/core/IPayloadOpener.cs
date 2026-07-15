// -----------------------------------------------------------------------
// <copyright file="IPayloadOpener.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Security.Cryptography;

/// <summary>
/// Decrypt-only capability of the sealed (asymmetric) payload-encryption
/// mode: opens frames sealed to this service's public key, using the
/// recipient's private keyring. The encrypt capability lives on the separate
/// <see cref="IPayloadSealer"/> — the capability split is compile-time.
/// Implementations MUST be safe to share across threads.
/// </summary>
/// <remarks>
/// The API deliberately mirrors <see cref="IPayloadCrypto.Decrypt"/> — no
/// AAD parameter. The backing <see cref="RecipientPrivateKeyring"/> carries
/// the recipient service id and the AEAD associated data is derived from it
/// structurally.
/// </remarks>
public interface IPayloadOpener
{
    /// <summary>
    /// Opens a sealed frame produced by <see cref="IPayloadSealer.Seal"/>.
    /// The recipient kid is read from the frame; the private keyring
    /// resolves it to this service's private key.
    /// </summary>
    /// <param name="framed">A complete version-2 sealed frame.</param>
    /// <returns>The recovered plaintext.</returns>
    /// <exception cref="ObjectDisposedException">The private keyring has been disposed.</exception>
    /// <exception cref="FrameMalformedException">
    /// Frame is structurally invalid — shorter than the minimum, a length
    /// prefix overruns the buffer, the declared ephemeral key exceeds the
    /// cap, or the ephemeral key bytes are not a valid P-256
    /// SubjectPublicKeyInfo.
    /// </exception>
    /// <exception cref="FrameVersionMismatchException">
    /// Frame's version byte is not <c>2</c> (a symmetric version-1 frame is
    /// rejected here, never mis-parsed).
    /// </exception>
    /// <exception cref="KidNotInKeyringException">
    /// Frame's declared recipient kid is not present in the private keyring.
    /// </exception>
    /// <exception cref="AuthenticationTagMismatchException">
    /// AEAD authentication failed — ciphertext tampered, the frame was
    /// sealed to a DIFFERENT recipient service (AAD/derivation mismatch), or
    /// the resolved private key does not match the sealing public key.
    /// </exception>
    byte[] Open(ReadOnlySpan<byte> framed);
}
