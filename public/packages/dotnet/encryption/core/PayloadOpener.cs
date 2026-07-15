// -----------------------------------------------------------------------
// <copyright file="PayloadOpener.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Formats.Asn1;
using System.Security.Cryptography;

/// <summary>
/// Default <see cref="IPayloadOpener"/> implementation: opens version-2
/// sealed frames with this service's <see cref="RecipientPrivateKeyring"/>
/// under the same frozen <see cref="SealedKeyDerivation"/> conventions the
/// sealer uses.
/// </summary>
/// <remarks>
/// Per open: parse the sealed frame → resolve the recipient kid against the
/// private keyring → import the frame's ephemeral public key (rejecting
/// non-P-256 material as frame malformation) → ECDH → HKDF-SHA256 → AES-256-GCM
/// decrypt. Tampering, a wrong-recipient frame, or a mismatched keypair all
/// surface as <see cref="AuthenticationTagMismatchException"/>. The shared
/// secret and the derived key are zeroized in <c>finally</c> on every path.
/// Thread-safe: no shared mutable state.
/// </remarks>
public sealed class PayloadOpener : IPayloadOpener
{
    private readonly RecipientPrivateKeyring r_keyring;
    private readonly byte[] r_serviceIdBytes;

    /// <summary>
    /// Initializes a new <see cref="PayloadOpener"/> bound to the given
    /// recipient private keyring.
    /// </summary>
    /// <param name="keyring">The private keyring to open against.</param>
    public PayloadOpener(RecipientPrivateKeyring keyring)
    {
        ArgumentNullException.ThrowIfNull(keyring);
        r_keyring = keyring;

        // Salt + AAD bytes are fixed per recipient — non-secret, computed once.
        r_serviceIdBytes = SealedKeyDerivation.ServiceIdBytes(keyring.RecipientServiceId);
    }

    /// <inheritdoc />
    public byte[] Open(ReadOnlySpan<byte> framed)
    {
        var view = SealedFrame.Decode(framed);

        if (!r_keyring.TryGetPrivateKey(view.RecipientKid, out var privatePkcs8))
            throw new KidNotInKeyringException(view.RecipientKid);

        byte[]? sharedSecret = null;
        Span<byte> dek = stackalloc byte[SealedKeyDerivation.DEK_SIZE_BYTES];

        try
        {
            using var recipientPrivate = EcdhP256.ImportPrivatePkcs8P256(privatePkcs8.Span);

            ECDiffieHellman ephemeral;

            try
            {
                ephemeral = EcdhP256.ImportPublicP256(view.EphemeralPublicSpki);
            }
            catch (Exception ex) when (ex is CryptographicException or AsnContentException)
            {
                throw new FrameMalformedException(
                    "Sealed frame eph_pub is not a valid P-256 SubjectPublicKeyInfo.", ex);
            }

            using (ephemeral)
            {
                using var ephemeralPublic = ephemeral.PublicKey;
                sharedSecret = recipientPrivate.DeriveRawSecretAgreement(ephemeralPublic);
            }

            SealedKeyDerivation.DeriveDek(
                sharedSecret, r_serviceIdBytes, view.EphemeralPublicSpki, dek);

            var ciphertextLength =
                view.CiphertextWithTag.Length - SealedFrameLayout.CONSTRAINT_TAG_LENGTH;
            var plaintext = new byte[ciphertextLength];
            var ciphertext = view.CiphertextWithTag[..ciphertextLength];
            var tag = view.CiphertextWithTag[ciphertextLength..];

            using var gcm = new AesGcm(dek, SealedFrameLayout.CONSTRAINT_TAG_LENGTH);
            gcm.Decrypt(view.Nonce, ciphertext, tag, plaintext, r_serviceIdBytes);

            return plaintext;
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
    public override string ToString() => nameof(PayloadOpener);
}
