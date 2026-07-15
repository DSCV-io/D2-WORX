// -----------------------------------------------------------------------
// <copyright file="PayloadCrypto.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Security.Cryptography;

/// <summary>
/// Default <see cref="IPayloadCrypto"/> implementation: AES-256-GCM with
/// a JWKS-style multi-kid keyring and AAD bound to the keyring's
/// AAD context.
/// </summary>
/// <remarks>
/// Per-call <see cref="AesGcm"/> instantiation. AesGcm holds pinned native
/// memory for the key schedule; per-call <c>using</c> avoids leaking that
/// across encrypts and sidesteps the documented thread-safety constraints
/// on a shared instance. The (small) key-schedule cost is irrelevant at
/// realistic message rates.
/// </remarks>
public sealed class PayloadCrypto : IPayloadCrypto
{
    private readonly PayloadCryptoKeyring r_keyring;

    /// <summary>
    /// Initializes a new <see cref="PayloadCrypto"/> bound to the given keyring.
    /// </summary>
    /// <param name="keyring">The keyring to encrypt and decrypt against.</param>
    public PayloadCrypto(PayloadCryptoKeyring keyring)
    {
        ArgumentNullException.ThrowIfNull(keyring);
        r_keyring = keyring;
    }

    /// <inheritdoc />
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
    {
        var activeKid = r_keyring.ActiveKid;
        if (!r_keyring.TryGetKey(activeKid, out var keyMemory))
            throw new KidNotInKeyringException(activeKid);

        Span<byte> nonce = stackalloc byte[EncryptionFrame.NONCE_SIZE];
        RandomNumberGenerator.Fill(nonce);

        var ciphertextWithTag = new byte[plaintext.Length + EncryptionFrame.TAG_SIZE];
        var ciphertext = ciphertextWithTag.AsSpan(0, plaintext.Length);
        var tag = ciphertextWithTag.AsSpan(plaintext.Length, EncryptionFrame.TAG_SIZE);

        using var gcm = new AesGcm(keyMemory.Span, EncryptionFrame.TAG_SIZE);
        gcm.Encrypt(nonce, plaintext, ciphertext, tag, r_keyring.AadContext.Span);

        return EncryptionFrame.Encode(activeKid, nonce, ciphertextWithTag);
    }

    /// <inheritdoc />
    public byte[] Decrypt(ReadOnlySpan<byte> framed)
    {
        var view = EncryptionFrame.Decode(framed);

        if (!r_keyring.TryGetKey(view.Kid, out var keyMemory))
            throw new KidNotInKeyringException(view.Kid);

        var ciphertextLength = view.CiphertextWithTag.Length - EncryptionFrame.TAG_SIZE;
        var plaintext = new byte[ciphertextLength];
        var ciphertext = view.CiphertextWithTag[..ciphertextLength];
        var tag = view.CiphertextWithTag[ciphertextLength..];

        using var gcm = new AesGcm(keyMemory.Span, EncryptionFrame.TAG_SIZE);
        gcm.Decrypt(view.Nonce, ciphertext, tag, plaintext, r_keyring.AadContext.Span);

        return plaintext;
    }

    /// <summary>
    /// Returns the type name only — never includes keyring contents.
    /// </summary>
    public override string ToString() => nameof(PayloadCrypto);
}
