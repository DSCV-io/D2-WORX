// -----------------------------------------------------------------------
// <copyright file="RecipientPublicKeyring.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Formats.Asn1;
using System.Security.Cryptography;

/// <summary>
/// Immutable keyring holding a recipient service's PUBLIC sealing keys
/// (active + any retiring) — the producer side of the sealed encryption
/// mode. Sibling of <see cref="PayloadCryptoKeyring"/> for the asymmetric
/// family: it carries the recipient service id that anchors the AEAD
/// binding, so producers never pass an AAD by hand.
/// </summary>
/// <remarks>
/// Public keys are wire-public by design — NOT zeroize-sensitive, so this
/// type is not disposable. Key material is still never rendered by
/// <see cref="ToString"/> (the no-key-bytes-in-logs invariant is uniform
/// across every keyring type). The constructor validates every entry
/// imports as a P-256 public key — a wrong-curve or garbage key fails loud
/// at the construction boundary, not at first <see cref="IPayloadSealer.Seal"/>.
/// </remarks>
public sealed class RecipientPublicKeyring
{
    private readonly Dictionary<string, byte[]> r_publicKeys;

    /// <summary>
    /// Initializes a new recipient public keyring.
    /// </summary>
    /// <param name="recipientServiceId">
    /// The recipient service id (lowercase <c>[a-z0-9-]</c>, at most 64
    /// characters — the workload service-id grammar). Anchors the AEAD
    /// binding and the key derivation.
    /// </param>
    /// <param name="activeKid">
    /// The recipient kid used for new seals. Must be present in
    /// <paramref name="publicKeysByKid"/>.
    /// </param>
    /// <param name="publicKeysByKid">
    /// All recipient kids a producer may seal under. Each value must be a
    /// valid P-256 SubjectPublicKeyInfo, at most
    /// <see cref="SealedFrameLayout.CONSTRAINT_MAX_EPH_PUB_LENGTH"/> bytes.
    /// </param>
    /// <exception cref="ArgumentException">
    /// A constructor argument violates a stated invariant.
    /// </exception>
    public RecipientPublicKeyring(
        string recipientServiceId,
        string activeKid,
        IReadOnlyDictionary<string, byte[]> publicKeysByKid)
    {
        SealedKeyringValidation.ValidateServiceId(recipientServiceId, nameof(recipientServiceId));
        ArgumentNullException.ThrowIfNull(activeKid);
        ArgumentNullException.ThrowIfNull(publicKeysByKid);
        SealedKeyringValidation.ValidateKid(activeKid, nameof(activeKid));

        if (!publicKeysByKid.ContainsKey(activeKid))
        {
            throw new ArgumentException(
                $"activeKid '{activeKid}' is not present in publicKeysByKid.",
                nameof(activeKid));
        }

        r_publicKeys = new Dictionary<string, byte[]>(
            publicKeysByKid.Count, StringComparer.Ordinal);

        foreach (var (kid, spki) in publicKeysByKid)
        {
            ArgumentNullException.ThrowIfNull(kid, nameof(publicKeysByKid));
            ArgumentNullException.ThrowIfNull(spki, nameof(publicKeysByKid));
            SealedKeyringValidation.ValidateKid(kid, nameof(publicKeysByKid));

            if (spki.Length is < 1 or > SealedFrameLayout.CONSTRAINT_MAX_EPH_PUB_LENGTH)
            {
                throw new ArgumentException(
                    $"public key for kid '{kid}' must be in " +
                    $"[1, {SealedFrameLayout.CONSTRAINT_MAX_EPH_PUB_LENGTH}] bytes " +
                    $"(got {spki.Length}).",
                    nameof(publicKeysByKid));
            }

            // Fail loud at the boundary: the SPKI must import as a P-256
            // public key AND complete a real agreement (catches same-size
            // non-P-256 curves + invalid points).
            try
            {
                using var imported = EcdhP256.ImportPublicP256(spki);
                EcdhP256.ProbeP256Agreement(imported);
            }
            catch (Exception ex) when (ex is CryptographicException or AsnContentException)
            {
                throw new ArgumentException(
                    $"public key for kid '{kid}' is not a valid P-256 " +
                    "SubjectPublicKeyInfo.",
                    nameof(publicKeysByKid),
                    ex);
            }

            // Defensive copy — caller may mutate their original.
            var copy = new byte[spki.Length];
            spki.CopyTo(copy, 0);
            r_publicKeys[kid] = copy;
        }

        RecipientServiceId = recipientServiceId;
        ActiveKid = activeKid;
    }

    /// <summary>
    /// Gets the recipient service id the AEAD binding and key derivation are
    /// anchored on.
    /// </summary>
    public string RecipientServiceId { get; }

    /// <summary>
    /// Gets the recipient kid used for new seals. Always present in the keyring.
    /// </summary>
    public string ActiveKid { get; }

    /// <summary>
    /// Returns a redacted string — never includes key bytes (uniform
    /// no-key-bytes-in-logs invariant, even for public material).
    /// </summary>
    public override string ToString()
        => $"RecipientPublicKeyring(RecipientServiceId={RecipientServiceId}, " +
           $"ActiveKid={ActiveKid}, Kids={r_publicKeys.Count})";

    /// <summary>
    /// Resolves a recipient kid to its public key bytes (SPKI DER).
    /// </summary>
    /// <param name="kid">
    /// The kid to look up. Null returns false
    /// (defensive — callers may pass externally-sourced strings).
    /// </param>
    /// <param name="publicKeySpki">The public key bytes when found; default otherwise.</param>
    /// <returns>True when the kid is present; false otherwise.</returns>
    internal bool TryGetPublicKey(string? kid, out ReadOnlyMemory<byte> publicKeySpki)
    {
        if (kid is not null && r_publicKeys.TryGetValue(kid, out var bytes))
        {
            publicKeySpki = bytes;
            return true;
        }

        publicKeySpki = default;
        return false;
    }
}
