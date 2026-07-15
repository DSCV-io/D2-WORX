// -----------------------------------------------------------------------
// <copyright file="CsrVerification.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Rules;

using System.Formats.Asn1;

/// <summary>
/// Pure rule that verifies a caller-supplied PKCS#10 certificate-signing request
/// for workload leaf issuance and exposes ONLY its public key.
/// </summary>
/// <remarks>
/// <para>
/// BCL crypto only (<c>CertificateRequest.LoadSigningRequest</c> +
/// <see cref="AsnReader"/>), no IO / DI / Options. Layered checks, all folded
/// into ONE coarse <c>KEYCUSTODIAN_INVALID_CSR</c> (400) failure so the surface
/// does not leak which check failed (mirroring the
/// <c>INVALID_WORKLOAD_IDENTITY</c> posture):
/// </para>
/// <list type="number">
///   <item><b>Size cap</b> — the DER length is bounded BEFORE any parse (a P-256
///   CSR is well under 1 KiB; the explicit cap never relies on the transport's
///   message-size limit alone).</item>
///   <item><b>PKCS#10 parse + proof-of-possession</b> —
///   <c>CertificateRequest.LoadSigningRequest</c> with default options
///   verifies the CSR's self-signature against its embedded public key (a CSR
///   whose signature does not validate proves nothing about key possession) and
///   does NOT load the requested extensions.</item>
///   <item><b>Leaf key policy</b> — the public key must be ECDSA P-256, enforced
///   by CURVE OID (<c>1.2.840.10045.3.1.7</c> / prime256v1), not merely
///   key-type-is-EC: RSA keys AND wrong-curve elliptic-curve keys (e.g. P-384)
///   are both rejected. The named-curve OID is read from the
///   SubjectPublicKeyInfo's algorithm parameters, so an explicit-parameters curve
///   encoding (the classic curve-confusion attack shape) is also rejected — only
///   the exact named-curve form passes.</item>
/// </list>
/// <para>
/// <b>Nothing but the public key is surfaced.</b> The CSR's subject, SAN, and
/// requested extensions are never read and never returned — the issuance handler
/// derives the leaf's subject-alternative-name from the authenticated mTLS peer
/// identity, so nothing from the CSR except its public key can reach the leaf.
/// </para>
/// <para>
/// A CSR is PUBLIC material by construction (public key + request metadata + a
/// self-signature) — it never carries the private key, so nothing here needs
/// redaction or zeroing.
/// </para>
/// </remarks>
public static class CsrVerification
{
    /// <summary>
    /// The maximum accepted CSR DER length in bytes. A P-256 PKCS#10 CSR is well
    /// under 1 KiB; 4 KiB leaves generous headroom for benign encoder variance
    /// while bounding adversarial input before any parse work.
    /// </summary>
    public const int MAX_CSR_DER_BYTES = 4096;

    // id-ecPublicKey — the SubjectPublicKeyInfo algorithm OID for every EC key.
    private const string _EC_PUBLIC_KEY_OID = "1.2.840.10045.2.1";

    // prime256v1 / secp256r1 / NIST P-256 — the ONLY leaf curve the mesh accepts.
    private const string _P256_CURVE_OID = "1.2.840.10045.3.1.7";

    // LoadSigningRequest requires a signer hash algorithm for the RETURNED
    // request object; the leaf signing path uses SHA-256 (the CA's fixed hash).
    private static readonly HashAlgorithmName sr_hash = HashAlgorithmName.SHA256;

    /// <summary>
    /// Verifies a PKCS#10 certificate-signing request and extracts its public key.
    /// </summary>
    /// <param name="csrDer">The DER-encoded PKCS#10 CSR bytes.</param>
    /// <returns>
    /// <c>Ok(<see cref="PublicKey"/>)</c> carrying the CSR's verified ECDSA P-256
    /// public key on success; a flagged <c>KEYCUSTODIAN_INVALID_CSR</c> (400)
    /// failure when the input is null / empty / oversized / malformed, the
    /// proof-of-possession self-signature does not validate, or the public key is
    /// not ECDSA P-256 by curve OID.
    /// </returns>
    public static D2Result<PublicKey> Verify(byte[]? csrDer)
    {
        // (1) Bound the input BEFORE any parse — an empty or oversized blob is
        // rejected without touching the ASN.1 decoder.
        if (csrDer.Falsey() || csrDer!.Length > MAX_CSR_DER_BYTES)
            return KeyCustodianFailures<PublicKey>.InvalidCsr();

        CertificateRequest request;

        try
        {
            // (2) Parse + proof-of-possession. The DEFAULT load options verify the
            // self-signature against the embedded public key and DO NOT load the
            // requested extensions (which this surface ignores by design).
            request = CertificateRequest.LoadSigningRequest(csrDer, sr_hash);
        }
        catch (Exception ex) when (ex is CryptographicException or AsnContentException)
        {
            // Malformed DER, trailing data, or a failed self-signature — one coarse
            // failure, no which-check-failed leak.
            return KeyCustodianFailures<PublicKey>.InvalidCsr();
        }

        // (3) Leaf key policy: ECDSA P-256 by curve OID. The algorithm must be
        // id-ecPublicKey AND the algorithm parameters must be exactly the P-256
        // NAMED-curve OID — RSA (different algorithm OID), wrong-curve EC (P-384
        // etc.), and explicit-parameters encodings all fail here.
        if (!string.Equals(
                request.PublicKey.Oid.Value, _EC_PUBLIC_KEY_OID, StringComparison.Ordinal))
            return KeyCustodianFailures<PublicKey>.InvalidCsr();

        var encodedParameters = request.PublicKey.EncodedParameters;

        if (encodedParameters is null || encodedParameters.RawData.Falsey())
            return KeyCustodianFailures<PublicKey>.InvalidCsr();

        try
        {
            var reader = new AsnReader(encodedParameters.RawData, AsnEncodingRules.DER);
            var curveOid = reader.ReadObjectIdentifier();
            reader.ThrowIfNotEmpty();

            if (!string.Equals(curveOid, _P256_CURVE_OID, StringComparison.Ordinal))
                return KeyCustodianFailures<PublicKey>.InvalidCsr();
        }
        catch (AsnContentException)
        {
            // Parameters are not a bare named-curve OID (e.g. explicit ECParameters)
            // — reject; only the exact named-curve form is accepted.
            return KeyCustodianFailures<PublicKey>.InvalidCsr();
        }

        return D2Result<PublicKey>.Ok(request.PublicKey);
    }
}
