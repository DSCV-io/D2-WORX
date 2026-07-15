// -----------------------------------------------------------------------
// <copyright file="KeyType.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Enums;

/// <summary>
/// Discriminates between the cryptographic algorithms a managed key employs.
/// </summary>
/// <remarks>
/// <c>RsaSigning</c> keys are asymmetric: they carry an encrypted private key
/// in <c>KeyMaterialEncrypted</c> and an unencrypted public key in
/// <c>PublicKeyMaterial</c> — the latter feeds the JWKS endpoint.
/// <c>AesPayload</c> and <c>Secret</c> keys are symmetric: they carry only
/// encrypted key material and have no public component.
/// <c>X509CaCertificate</c> keys are asymmetric ECDSA P-256 certificate-authority
/// keys: they carry an encrypted private key in <c>KeyMaterialEncrypted</c> and
/// the CA certificate (not a bare public key) in <c>CaCertificateMaterial</c> —
/// the latter is presented on the wire during the TLS handshake.
/// <c>EcdhSealing</c> keys are asymmetric ECDH P-256 sealing keypairs: they carry
/// an encrypted private key in <c>KeyMaterialEncrypted</c> and the unencrypted SPKI
/// public key in <c>PublicKeyMaterial</c> — the latter is fetched by any workload
/// that wants to seal a payload TO the owning service; the private half opens frames
/// sealed to it.
/// </remarks>
public enum KeyType
{
    /// <summary>
    /// RS256 asymmetric signing key — private material encrypted, public material stored
    /// plaintext for JWKS.
    /// </summary>
    RsaSigning,

    /// <summary>AES-256 payload encryption key — symmetric, no public component.</summary>
    AesPayload,

    /// <summary>
    /// Opaque symmetric secret key (e.g. cookie-signing HMAC, client-secret material)
    /// — no public component.
    /// </summary>
    Secret,

    /// <summary>
    /// Internal X.509 certificate-authority key (root or intermediate) — asymmetric
    /// ECDSA P-256: the private key is encrypted in <c>KeyMaterialEncrypted</c> and the
    /// CA certificate is carried as <c>CaCertificateMaterial</c> (a full X.509
    /// certificate, not a bare SPKI public key). There is no leaf key type — workload
    /// leaf certificates are issued on demand and never persisted as managed keys.
    /// </summary>
    X509CaCertificate,

    /// <summary>
    /// Asymmetric ECDH P-256 sealing keypair — the private key is encrypted in
    /// <c>KeyMaterialEncrypted</c> and the SPKI public key is stored plaintext in
    /// <c>PublicKeyMaterial</c> (fetched to seal a payload TO the owning service).
    /// Provisioned per service under the <c>seal:&lt;serviceId&gt;</c> key domain
    /// family; the public key encrypts, the private key opens (the sealed
    /// asymmetric payload-encryption mode).
    /// </summary>
    EcdhSealing,
}
