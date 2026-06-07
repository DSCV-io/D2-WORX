// -----------------------------------------------------------------------
// <copyright file="KeyType.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Enums;

/// <summary>
/// Discriminates between the cryptographic algorithms a managed key employs.
/// </summary>
/// <remarks>
/// <c>RsaSigning</c> keys are asymmetric: they carry an encrypted private key
/// in <c>KeyMaterialEncrypted</c> and an unencrypted public key in
/// <c>PublicKeyMaterial</c> — the latter feeds the JWKS endpoint.
/// <c>AesPayload</c> and <c>Secret</c> keys are symmetric: they carry only
/// encrypted key material and have no public component.
/// </remarks>
public enum KeyType
{
    /// <summary>RS256 asymmetric signing key — private material encrypted, public material stored plaintext for JWKS.</summary>
    RsaSigning,

    /// <summary>AES-256 payload encryption key — symmetric, no public component.</summary>
    AesPayload,

    /// <summary>Opaque symmetric secret key (e.g. cookie-signing HMAC, client-secret material) — no public component.</summary>
    Secret,
}
