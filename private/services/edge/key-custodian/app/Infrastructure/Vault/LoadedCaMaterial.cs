// -----------------------------------------------------------------------
// <copyright file="LoadedCaMaterial.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Infrastructure.Vault;

/// <summary>
/// Short-lived carrier for the loaded + chain-validated dev certificate-authority
/// hierarchy (root + issuing intermediate) before the private keys are
/// root-wrapped and persisted as managed keys.
/// </summary>
/// <remarks>
/// <b>Plaintext lifetime.</b> <see cref="RootPrivateKeyPkcs8"/> and
/// <see cref="IntermediatePrivateKeyPkcs8"/> hold the raw, unencrypted PKCS#8
/// ECDSA private keys. The caller (the seeding command handler) MUST zero these
/// bytes via <see cref="CryptographicOperations.ZeroMemory(System.Span{byte})"/>
/// as soon as it has root-wrapped them — call <see cref="Zero"/> for that.
///
/// <b>Certificates are public.</b> <see cref="RootCertificateDer"/> and
/// <see cref="IntermediateCertificateDer"/> are the DER-encoded CA certificates —
/// not secret; they are pinned as trust anchors and presented on the wire.
///
/// <b>No <c>ToString</c> leak.</b> A <c>byte[]</c> field would otherwise dump in
/// any interpolation / log; this class overrides <see cref="ToString"/> to emit
/// only byte counts (and a redaction sentinel for the two private keys).
///
/// <b>Sibling of <c>GeneratedCaMaterial</c>.</b> Same zero-after-wrap shape, but
/// it carries BOTH tiers' material so the seeder can persist the full hierarchy in
/// one pass.
/// </remarks>
public sealed class LoadedCaMaterial
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LoadedCaMaterial"/> class.
    /// </summary>
    /// <param name="rootCertificateDer">DER-encoded root CA certificate. Must be non-empty.</param>
    /// <param name="rootPrivateKeyPkcs8">Raw PKCS#8 root ECDSA private key. Must be non-empty.</param>
    /// <param name="intermediateCertificateDer">
    /// DER-encoded intermediate CA certificate. Must be non-empty.
    /// </param>
    /// <param name="intermediatePrivateKeyPkcs8">
    /// Raw PKCS#8 intermediate ECDSA private key. Must be non-empty.
    /// </param>
    /// <exception cref="ArgumentException">Any buffer is empty.</exception>
    public LoadedCaMaterial(
        byte[] rootCertificateDer,
        byte[] rootPrivateKeyPkcs8,
        byte[] intermediateCertificateDer,
        byte[] intermediatePrivateKeyPkcs8)
    {
        // §5.1a carve-out: reference-type null-guards (byte[]) — no present-but-falsey concept.
        ArgumentNullException.ThrowIfNull(rootCertificateDer);
        ArgumentNullException.ThrowIfNull(rootPrivateKeyPkcs8);
        ArgumentNullException.ThrowIfNull(intermediateCertificateDer);
        ArgumentNullException.ThrowIfNull(intermediatePrivateKeyPkcs8);
        ThrowIfEmpty(rootCertificateDer, nameof(rootCertificateDer));
        ThrowIfEmpty(rootPrivateKeyPkcs8, nameof(rootPrivateKeyPkcs8));
        ThrowIfEmpty(intermediateCertificateDer, nameof(intermediateCertificateDer));
        ThrowIfEmpty(intermediatePrivateKeyPkcs8, nameof(intermediatePrivateKeyPkcs8));

        RootCertificateDer = rootCertificateDer;
        RootPrivateKeyPkcs8 = rootPrivateKeyPkcs8;
        IntermediateCertificateDer = intermediateCertificateDer;
        IntermediatePrivateKeyPkcs8 = intermediatePrivateKeyPkcs8;
    }

    /// <summary>
    /// Gets the DER-encoded root CA certificate. Not secret — the trust anchor.
    /// </summary>
    public byte[] RootCertificateDer { get; }

    /// <summary>Gets the raw PKCS#8 root private key bytes. Zero after wrapping — never log.</summary>
    public byte[] RootPrivateKeyPkcs8 { get; }

    /// <summary>
    /// Gets the DER-encoded intermediate (issuing) CA certificate. Not secret —
    /// presented on the wire.
    /// </summary>
    public byte[] IntermediateCertificateDer { get; }

    /// <summary>
    /// Gets the raw PKCS#8 intermediate private key bytes. Zero after wrapping —
    /// never log.
    /// </summary>
    public byte[] IntermediatePrivateKeyPkcs8 { get; }

    /// <summary>
    /// Zeroes both private-key buffers. Call after root-wrapping both tiers.
    /// </summary>
    public void Zero()
    {
        CryptographicOperations.ZeroMemory(RootPrivateKeyPkcs8);
        CryptographicOperations.ZeroMemory(IntermediatePrivateKeyPkcs8);
    }

    /// <inheritdoc/>
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"LoadedCaMaterial {{ RootCertificateDer = [{RootCertificateDer.Length} bytes], "
        + $"RootPrivateKeyPkcs8 = [REDACTED, {RootPrivateKeyPkcs8.Length} bytes], "
        + $"IntermediateCertificateDer = [{IntermediateCertificateDer.Length} bytes], "
        + $"IntermediatePrivateKeyPkcs8 = [REDACTED, {IntermediatePrivateKeyPkcs8.Length} bytes] }}");

    private static void ThrowIfEmpty(byte[] bytes, string paramName)
    {
        if (bytes.Length == 0)
            throw new ArgumentException("Loaded CA material buffer must not be empty.", paramName);
    }
}
