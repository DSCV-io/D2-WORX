// -----------------------------------------------------------------------
// <copyright file="PayloadSealer.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Security.Cryptography;

/// <summary>
/// Default <see cref="IPayloadSealer"/> implementation: the P-256 ECDH-ES →
/// HKDF-SHA256 → AES-256-GCM hybrid seal over a
/// <see cref="RecipientPublicKeyring"/>.
/// </summary>
/// <remarks>
/// <para>
/// Per seal: a fresh ephemeral P-256 keypair → ECDH against the recipient's
/// active public key → HKDF-SHA256 (under the frozen
/// <see cref="SealedKeyDerivation"/> conventions) → a per-message AES-256-GCM
/// content-encryption key → a fresh 12-byte nonce → the version-2 sealed
/// frame. The shared secret and the derived key are zeroized in
/// <c>finally</c> on every path.
/// </para>
/// <para>
/// Thread-safe: no shared mutable state — every operation is self-contained
/// (per-call ephemeral keypair, per-call <see cref="AesGcm"/>, mirroring the
/// per-call discipline of <see cref="PayloadCrypto"/>).
/// </para>
/// </remarks>
public sealed class PayloadSealer : IPayloadSealer
{
    private readonly RecipientPublicKeyring r_keyring;
    private readonly byte[] r_serviceIdBytes;

    /// <summary>
    /// Initializes a new <see cref="PayloadSealer"/> bound to the given
    /// recipient public keyring.
    /// </summary>
    /// <param name="keyring">The recipient public keyring to seal against.</param>
    public PayloadSealer(RecipientPublicKeyring keyring)
    {
        ArgumentNullException.ThrowIfNull(keyring);
        r_keyring = keyring;

        // Salt + AAD bytes are fixed per recipient — non-secret, computed once.
        r_serviceIdBytes = SealedKeyDerivation.ServiceIdBytes(keyring.RecipientServiceId);
    }

    /// <inheritdoc />
    public byte[] Seal(ReadOnlySpan<byte> plaintext)
    {
        var activeKid = r_keyring.ActiveKid;

        if (!r_keyring.TryGetPublicKey(activeKid, out var recipientSpki))
            throw new KidNotInKeyringException(activeKid);

        byte[]? sharedSecret = null;
        Span<byte> dek = stackalloc byte[SealedKeyDerivation.DEK_SIZE_BYTES];

        try
        {
            using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            using var recipient = EcdhP256.ImportPublicP256(recipientSpki.Span);
            using var recipientPublic = recipient.PublicKey;
            sharedSecret = ephemeral.DeriveRawSecretAgreement(recipientPublic);
            var ephemeralSpki = ephemeral.ExportSubjectPublicKeyInfo();

            SealedKeyDerivation.DeriveDek(sharedSecret, r_serviceIdBytes, ephemeralSpki, dek);

            Span<byte> nonce = stackalloc byte[SealedFrameLayout.CONSTRAINT_NONCE_LENGTH];
            RandomNumberGenerator.Fill(nonce);

            var ciphertextWithTag =
                new byte[plaintext.Length + SealedFrameLayout.CONSTRAINT_TAG_LENGTH];
            var ciphertext = ciphertextWithTag.AsSpan(0, plaintext.Length);
            var tag = ciphertextWithTag.AsSpan(
                plaintext.Length, SealedFrameLayout.CONSTRAINT_TAG_LENGTH);

            using var gcm = new AesGcm(dek, SealedFrameLayout.CONSTRAINT_TAG_LENGTH);
            gcm.Encrypt(nonce, plaintext, ciphertext, tag, r_serviceIdBytes);

            return SealedFrame.Encode(activeKid, ephemeralSpki, nonce, ciphertextWithTag);
        }
        finally
        {
            if (sharedSecret is not null)
                CryptographicOperations.ZeroMemory(sharedSecret);
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    /// <summary>
    /// Returns the type name only — never includes keyring contents.
    /// </summary>
    public override string ToString() => nameof(PayloadSealer);
}
