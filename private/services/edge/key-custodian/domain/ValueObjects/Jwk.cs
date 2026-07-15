// -----------------------------------------------------------------------
// <copyright file="Jwk.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Domain.ValueObjects;

/// <summary>
/// A single RFC 7517 JSON Web Key for an RS256 public signing key. All fields
/// are public (this is the JWKS surface) — there is no private material here.
/// </summary>
/// <param name="Kid">The key identifier (matches the JWT <c>kid</c> header).</param>
/// <param name="N">The RSA modulus, base64url-encoded (unpadded).</param>
/// <param name="E">The RSA public exponent, base64url-encoded (unpadded).</param>
public sealed record Jwk(string Kid, string N, string E)
{
    /// <summary>Gets the key type — always <c>RSA</c> for the signing domain.</summary>
    public string Kty { get; init; } = "RSA";

    /// <summary>Gets the intended key use — always <c>sig</c> (signature verification).</summary>
    public string Use { get; init; } = "sig";

    /// <summary>Gets the signing algorithm — always <c>RS256</c>.</summary>
    public string Alg { get; init; } = "RS256";
}
