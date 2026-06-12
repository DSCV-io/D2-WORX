// -----------------------------------------------------------------------
// <copyright file="GeneratedKeyMaterial.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.ValueObjects;

using System.Globalization;
using System.Security.Cryptography;

/// <summary>
/// Short-lived carrier for freshly-generated key material before it is
/// root-wrapped.
/// </summary>
/// <remarks>
/// <b>Plaintext lifetime.</b> <see cref="Plaintext"/> holds raw, unencrypted key
/// bytes (the PKCS#8 private key for RSA, the raw symmetric key for AES/Secret).
/// The caller (the command handler) MUST zero these bytes via
/// <see cref="CryptographicOperations.ZeroMemory(System.Span{byte})"/> as soon
/// as it has root-wrapped them — call <see cref="Zero"/> for that.
///
/// <b>No <c>ToString</c> leak.</b> A <c>byte[]</c> field would otherwise dump in
/// any interpolation / log; this class overrides <see cref="ToString"/> to emit
/// only byte counts.
/// </remarks>
public sealed class GeneratedKeyMaterial
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedKeyMaterial"/> class.
    /// </summary>
    /// <param name="plaintext">Raw private/symmetric key bytes. Must be non-empty.</param>
    /// <param name="publicSpki">
    /// The SPKI-encoded public key for asymmetric keys; <see langword="null"/> for
    /// symmetric keys.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="plaintext"/> is empty.</exception>
    public GeneratedKeyMaterial(byte[] plaintext, byte[]? publicSpki)
    {
        // §5.1a carve-out: reference-type null-guard (byte[]) — no present-but-falsey concept.
        ArgumentNullException.ThrowIfNull(plaintext);
        if (plaintext.Length == 0)
        {
            throw new ArgumentException(
                "Generated plaintext key material must not be empty.",
                nameof(plaintext));
        }

        Plaintext = plaintext;
        PublicSpki = publicSpki;
    }

    /// <summary>Gets the raw, unencrypted key bytes. Zero after wrapping — never log.</summary>
    public byte[] Plaintext { get; }

    /// <summary>
    /// Gets the SPKI-encoded public key bytes for asymmetric keys;
    /// <see langword="null"/> for symmetric keys. Not secret — published via JWKS.
    /// </summary>
    public byte[]? PublicSpki { get; }

    /// <summary>
    /// Zeroes the <see cref="Plaintext"/> buffer. Call after root-wrapping.
    /// </summary>
    public void Zero() => CryptographicOperations.ZeroMemory(Plaintext);

    /// <inheritdoc/>
    public override string ToString()
    {
        var len = Plaintext.Length;
        var spki = Describe(PublicSpki);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"GeneratedKeyMaterial {{ Plaintext = [REDACTED, {len} bytes], PublicSpki = {spki} }}");
    }

    private static string Describe(byte[]? bytes) =>
        bytes is null
            ? "null"
            : string.Create(CultureInfo.InvariantCulture, $"[{bytes.Length} bytes]");
}
