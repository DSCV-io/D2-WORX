// -----------------------------------------------------------------------
// <copyright file="RecipientPrivateKeyring.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using System.Formats.Asn1;
using System.Security.Cryptography;

/// <summary>
/// Immutable keyring holding THIS service's PRIVATE sealing keys (active +
/// any retiring) — the consumer side of the sealed encryption mode. Holds
/// raw private key material — never log, serialize, or otherwise expose a
/// keyring instance through any I/O path.
/// </summary>
/// <remarks>
/// Constructor copies all key bytes into private buffers so callers may
/// zeroize their own copies immediately. <see cref="Dispose"/> zeroes the
/// internal buffers via <see cref="CryptographicOperations.ZeroMemory"/>;
/// any <see cref="PayloadOpener"/> still referencing this keyring at
/// dispose time will throw <see cref="ObjectDisposedException"/> on its
/// next call. The constructor validates every entry imports as a P-256
/// private key — public-only input, a wrong curve, or garbage fails loud at
/// the construction boundary.
/// </remarks>
public sealed class RecipientPrivateKeyring : IDisposable
{
    private readonly Dictionary<string, byte[]> r_privateKeys;
    private bool _disposed;

    /// <summary>
    /// Initializes a new recipient private keyring.
    /// </summary>
    /// <param name="recipientServiceId">
    /// This service's id (lowercase <c>[a-z0-9-]</c>, at most 64 characters
    /// — the workload service-id grammar). Anchors the AEAD binding and the
    /// key derivation; must equal the id producers seal to.
    /// </param>
    /// <param name="privateKeysByKid">
    /// All recipient kids this service can open (active + retiring). Each
    /// value must be a valid P-256 PKCS#8 PrivateKeyInfo. Must be non-empty.
    /// </param>
    /// <exception cref="ArgumentException">
    /// A constructor argument violates a stated invariant.
    /// </exception>
    public RecipientPrivateKeyring(
        string recipientServiceId,
        IReadOnlyDictionary<string, byte[]> privateKeysByKid)
    {
        SealedKeyringValidation.ValidateServiceId(recipientServiceId, nameof(recipientServiceId));
        ArgumentNullException.ThrowIfNull(privateKeysByKid);

        if (privateKeysByKid.Count == 0)
        {
            throw new ArgumentException(
                "privateKeysByKid must contain at least one key — an empty " +
                "private keyring can never open anything.",
                nameof(privateKeysByKid));
        }

        r_privateKeys = new Dictionary<string, byte[]>(
            privateKeysByKid.Count, StringComparer.Ordinal);

        foreach (var (kid, pkcs8) in privateKeysByKid)
        {
            ArgumentNullException.ThrowIfNull(kid, nameof(privateKeysByKid));
            ArgumentNullException.ThrowIfNull(pkcs8, nameof(privateKeysByKid));
            SealedKeyringValidation.ValidateKid(kid, nameof(privateKeysByKid));

            // Fail loud at the boundary: the bytes must import as a P-256
            // PRIVATE key (an SPKI/public-only blob is rejected by the
            // PKCS#8 import) AND complete a real agreement.
            try
            {
                using var imported = EcdhP256.ImportPrivatePkcs8P256(pkcs8);
                EcdhP256.ProbeP256Agreement(imported);
            }
            catch (Exception ex) when (ex is CryptographicException or AsnContentException)
            {
                throw new ArgumentException(
                    $"private key for kid '{kid}' is not a valid P-256 " +
                    "PKCS#8 PrivateKeyInfo.",
                    nameof(privateKeysByKid),
                    ex);
            }

            // Defensive copy — caller may zeroize their original immediately.
            var copy = new byte[pkcs8.Length];
            pkcs8.CopyTo(copy, 0);
            r_privateKeys[kid] = copy;
        }

        RecipientServiceId = recipientServiceId;
    }

    /// <summary>
    /// Gets this service's id — the recipient identity the AEAD binding and
    /// key derivation are anchored on.
    /// </summary>
    public string RecipientServiceId { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        foreach (var key in r_privateKeys.Values)
            CryptographicOperations.ZeroMemory(key);
        r_privateKeys.Clear();
        _disposed = true;
    }

    /// <summary>
    /// Returns a redacted string — never includes key bytes.
    /// </summary>
    public override string ToString()
        => _disposed
            ? "RecipientPrivateKeyring(disposed)"
            : $"RecipientPrivateKeyring(RecipientServiceId={RecipientServiceId}, " +
              $"Kids={r_privateKeys.Count})";

    /// <summary>
    /// Resolves a recipient kid to its private key bytes (PKCS#8 DER).
    /// </summary>
    /// <param name="kid">
    /// The kid to look up. Null returns false
    /// (defensive — the kid comes from the wire frame).
    /// </param>
    /// <param name="privateKeyPkcs8">The private key bytes when found; default otherwise.</param>
    /// <returns>True when the kid is present; false otherwise.</returns>
    /// <exception cref="ObjectDisposedException">The keyring has been disposed.</exception>
    internal bool TryGetPrivateKey(string? kid, out ReadOnlyMemory<byte> privateKeyPkcs8)
    {
        ThrowIfDisposed();

        if (kid is not null && r_privateKeys.TryGetValue(kid, out var bytes))
        {
            privateKeyPkcs8 = bytes;
            return true;
        }

        privateKeyPkcs8 = default;
        return false;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
