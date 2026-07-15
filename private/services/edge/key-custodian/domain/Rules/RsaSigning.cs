// -----------------------------------------------------------------------
// <copyright file="RsaSigning.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Rules;

/// <summary>
/// Pure rule that signs a payload with an RSA private key using RS256
/// (RSASSA-PKCS1-v1_5 over SHA-256 — the JWT RS256 algorithm).
/// </summary>
/// <remarks>
/// BCL crypto only (<see cref="RSA"/>), no IO / DI / Options. The rule receives
/// the ALREADY-UNWRAPPED PKCS#8 private key (the handler owns the decrypt + the
/// finally-zero — the rule does not own the buffer's lifetime). Every signing is
/// wrapped so a crypto import/sign exception becomes a typed
/// <c>KEYCUSTODIAN_PRECONDITION_VIOLATED</c> failure rather than a throw — the rule
/// never throws, mirroring <see cref="WorkloadCertificateIssuance"/>.
/// </remarks>
public static class RsaSigning
{
    /// <summary>
    /// Signs <paramref name="signingInput"/> with the supplied RSA private key
    /// (RS256). Returns the base64url-encoded (unpadded) signature.
    /// </summary>
    /// <param name="privatePkcs8">The already-unwrapped RSA private key (PKCS#8).</param>
    /// <param name="signingInput">The exact bytes to sign.</param>
    /// <returns>
    /// <c>Ok</c> with the base64url signature; a flagged
    /// <c>KEYCUSTODIAN_PRECONDITION_VIOLATED</c> (500) failure when the key import or
    /// the sign operation fails (corruption — the key came from the trusted store).
    /// </returns>
    public static D2Result<string> Sign(
        ReadOnlySpan<byte> privatePkcs8, ReadOnlySpan<byte> signingInput)
    {
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportPkcs8PrivateKey(privatePkcs8, out _);

            var signature = rsa.SignData(
                signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return D2Result<string>.Ok(Base64Url.EncodeToString(signature));
        }
        catch (CryptographicException)
        {
            // The key material came from the trusted store; a crypto failure at sign
            // time is corruption, surfaced as a flagged 500 (not a throw).
            return KeyCustodianFailures<string>.PreconditionViolated();
        }
    }
}
