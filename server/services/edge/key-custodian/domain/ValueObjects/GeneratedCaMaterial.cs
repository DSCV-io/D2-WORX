// -----------------------------------------------------------------------
// <copyright file="GeneratedCaMaterial.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.ValueObjects;

/// <summary>
/// Short-lived carrier for a freshly-generated certificate-authority's private
/// key + certificate before the private key is root-wrapped.
/// </summary>
/// <remarks>
/// <b>Plaintext lifetime.</b> <see cref="PrivateKeyPkcs8"/> holds the raw,
/// unencrypted PKCS#8 ECDSA private key. The caller (the bootstrap command
/// handler) MUST zero these bytes via
/// <see cref="CryptographicOperations.ZeroMemory(System.Span{byte})"/> as soon as
/// it has root-wrapped them — call <see cref="Zero"/> for that.
///
/// <b><see cref="CertificateDer"/> is public.</b> The DER-encoded CA certificate
/// is not secret — it is pinned as a trust anchor and presented on the wire.
///
/// <b>No <c>ToString</c> leak.</b> A <c>byte[]</c> field would otherwise dump in
/// any interpolation / log; this class overrides <see cref="ToString"/> to emit
/// only byte counts (and a redaction sentinel for the private key).
///
/// <b>Sibling of <c>GeneratedKeyMaterial</c>.</b> Same zero-after-wrap shape, but
/// the public artifact is a full X.509 certificate, not a bare SPKI public key.
/// </remarks>
public sealed class GeneratedCaMaterial
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedCaMaterial"/> class.
    /// </summary>
    /// <param name="privateKeyPkcs8">Raw PKCS#8 ECDSA private key bytes. Must be non-empty.</param>
    /// <param name="certificateDer">DER-encoded CA certificate bytes. Must be non-empty.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="privateKeyPkcs8"/> or <paramref name="certificateDer"/> is empty.
    /// </exception>
    public GeneratedCaMaterial(byte[] privateKeyPkcs8, byte[] certificateDer)
    {
        // §5.1a carve-out: reference-type null-guards (byte[]) — no present-but-falsey concept.
        ArgumentNullException.ThrowIfNull(privateKeyPkcs8);
        ArgumentNullException.ThrowIfNull(certificateDer);

        if (privateKeyPkcs8.Length == 0)
        {
            throw new ArgumentException(
                "Generated CA private key material must not be empty.",
                nameof(privateKeyPkcs8));
        }

        if (certificateDer.Length == 0)
        {
            throw new ArgumentException(
                "Generated CA certificate material must not be empty.",
                nameof(certificateDer));
        }

        PrivateKeyPkcs8 = privateKeyPkcs8;
        CertificateDer = certificateDer;
    }

    /// <summary>Gets the raw PKCS#8 private key bytes. Zero after wrapping — never log.</summary>
    public byte[] PrivateKeyPkcs8 { get; }

    /// <summary>
    /// Gets the DER-encoded CA certificate bytes. Not secret — pinned as a trust
    /// anchor and presented on the wire.
    /// </summary>
    public byte[] CertificateDer { get; }

    /// <summary>
    /// Zeroes the <see cref="PrivateKeyPkcs8"/> buffer. Call after root-wrapping.
    /// </summary>
    public void Zero() => CryptographicOperations.ZeroMemory(PrivateKeyPkcs8);

    /// <inheritdoc/>
    public override string ToString()
    {
        var privateLen = PrivateKeyPkcs8.Length;
        var certLen = CertificateDer.Length;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"GeneratedCaMaterial {{ PrivateKeyPkcs8 = [REDACTED, {privateLen} bytes], "
            + $"CertificateDer = [{certLen} bytes] }}");
    }
}
