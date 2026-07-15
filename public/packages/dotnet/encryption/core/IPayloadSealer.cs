// -----------------------------------------------------------------------
// <copyright file="IPayloadSealer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

/// <summary>
/// Encrypt-only capability of the sealed (asymmetric) payload-encryption
/// mode: seals a payload TO a recipient service's public key. Structurally
/// CANNOT open — there is no <c>Open</c> member, so a producer holding only
/// a sealer can never decrypt any sealed frame, including its own output.
/// The decrypt capability lives on the separate <see cref="IPayloadOpener"/>,
/// backed by the recipient's private keyring. Implementations MUST be safe
/// to share across threads.
/// </summary>
/// <remarks>
/// The API deliberately mirrors <see cref="IPayloadCrypto.Encrypt"/> — no
/// AAD parameter. The backing <see cref="RecipientPublicKeyring"/> carries
/// the recipient service id, and the AEAD associated data is derived from
/// it structurally, so a producer and its consumer can never disagree on
/// the binding by passing different values.
/// </remarks>
public interface IPayloadSealer
{
    /// <summary>
    /// Seals <paramref name="plaintext"/> to the recipient's active public
    /// key and returns a self-contained version-2 sealed frame. Each call
    /// generates a fresh ephemeral P-256 keypair and a fresh 12-byte nonce —
    /// two seals of the same plaintext never produce the same frame.
    /// </summary>
    /// <param name="plaintext">Bytes to seal. Empty input is allowed.</param>
    /// <returns>
    /// Frame: <c>[version=2][recipient_kid_len:1][recipient_kid:UTF-8][eph_pub_len:2 BE][eph_pub:SPKI][nonce:12][ciphertext+tag]</c>.
    /// </returns>
    /// <exception cref="KidNotInKeyringException">
    /// The keyring's active kid is no longer present (programmer error — a
    /// keyring must always contain its declared active kid).
    /// </exception>
    byte[] Seal(ReadOnlySpan<byte> plaintext);
}
