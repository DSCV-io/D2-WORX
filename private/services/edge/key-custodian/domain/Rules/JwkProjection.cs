// -----------------------------------------------------------------------
// <copyright file="JwkProjection.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Rules;

/// <summary>
/// Pure rule that converts an RSA public key (SPKI bytes) to its RFC 7517
/// JWK representation: <c>{ kty:"RSA", use:"sig", alg:"RS256", kid, n, e }</c>
/// with <c>n</c> / <c>e</c> base64url-encoded (unpadded) per RFC 7518 §6.3.
/// </summary>
/// <remarks>
/// Stateless + side-effect-free — no DI, no clock. Imports the SPKI to recover
/// the modulus + exponent rather than re-parsing the DER by hand.
/// </remarks>
public static class JwkProjection
{
    /// <summary>
    /// Builds a <see cref="Jwk"/> from a kid + the SPKI-encoded public key.
    /// </summary>
    /// <param name="kid">The key identifier.</param>
    /// <param name="publicSpki">The SPKI-encoded RSA public key bytes.</param>
    /// <returns>The assembled JWK.</returns>
    public static Jwk ToJwk(string kid, ReadOnlySpan<byte> publicSpki)
    {
        using var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(publicSpki, out _);
        var parameters = rsa.ExportParameters(includePrivateParameters: false);

        // Modulus / Exponent are big-endian, leading-zero-trimmed by the BCL —
        // exactly the JWK n/e byte form, so base64url-encode them directly.
        var n = Base64Url.EncodeToString(parameters.Modulus!);
        var e = Base64Url.EncodeToString(parameters.Exponent!);
        return new Jwk(kid, n, e);
    }
}
