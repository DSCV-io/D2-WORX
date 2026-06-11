// -----------------------------------------------------------------------
// <copyright file="KeyGeneration.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Rules;

using System.Security.Cryptography;

/// <summary>
/// Pure rule that generates fresh key material for a <see cref="KeyType"/>.
/// </summary>
/// <remarks>
/// The sizing tunables (<c>rsaModulusBits</c> for RSA, <c>secretLengthBytes</c>
/// for the opaque secret) are method parameters the handler passes in from
/// configuration — the rule reads no options and holds no DI. Each arm emits the
/// raw plaintext material (to be root-wrapped) plus, for asymmetric keys, the
/// SPKI-encoded public component (published via JWKS).
/// </remarks>
public static class KeyGeneration
{
    /// <summary>AES-256 key length in bytes.</summary>
    private const int _AES_256_KEY_BYTES = 32;

    /// <summary>
    /// Generates fresh key material for <paramref name="keyType"/>. The returned
    /// <see cref="GeneratedKeyMaterial.Plaintext"/> is raw and unencrypted — the
    /// caller root-wraps it then zeroes it.
    /// </summary>
    /// <param name="keyType">The cryptographic algorithm category to generate.</param>
    /// <param name="rsaModulusBits">
    /// The RSA modulus size in bits (used only for <c>RsaSigning</c>).
    /// </param>
    /// <param name="secretLengthBytes">
    /// The opaque-secret length in bytes (used only for <c>Secret</c>).
    /// </param>
    /// <returns>
    /// <c>Ok(<see cref="GeneratedKeyMaterial"/>)</c> on success; a flagged
    /// <c>KEYCUSTODIAN_PRECONDITION_VIOLATED</c> failure when <paramref name="keyType"/>
    /// is not a recognized <see cref="KeyType"/>. The closed enum makes this
    /// unreachable from valid call sites; an unknown type is a precondition violation
    /// surfaced as a flagged 500 result (carrying telemetry) rather than a thrown exception.
    /// </returns>
    public static D2Result<GeneratedKeyMaterial> Generate(
        KeyType keyType,
        int rsaModulusBits,
        int secretLengthBytes) =>
        keyType switch
        {
            KeyType.RsaSigning => D2Result<GeneratedKeyMaterial>.Ok(
                GenerateRsaSigning(rsaModulusBits)),
            KeyType.AesPayload => D2Result<GeneratedKeyMaterial>.Ok(GenerateAesPayload()),
            KeyType.Secret => D2Result<GeneratedKeyMaterial>.Ok(GenerateSecret(secretLengthBytes)),
            _ => KeyCustodianFailures<GeneratedKeyMaterial>.PreconditionViolated(
                messages: [TK.Keycustodian.Internal.PRECONDITION_VIOLATED]),
        };

    private static GeneratedKeyMaterial GenerateRsaSigning(int rsaModulusBits)
    {
        using var rsa = RSA.Create(rsaModulusBits);
        var pkcs8 = rsa.ExportPkcs8PrivateKey();
        var spki = rsa.ExportSubjectPublicKeyInfo();
        return new GeneratedKeyMaterial(pkcs8, spki);
    }

    private static GeneratedKeyMaterial GenerateAesPayload()
    {
        var key = RandomNumberGenerator.GetBytes(_AES_256_KEY_BYTES);
        return new GeneratedKeyMaterial(key, publicSpki: null);
    }

    private static GeneratedKeyMaterial GenerateSecret(int secretLengthBytes)
    {
        var secret = RandomNumberGenerator.GetBytes(secretLengthBytes);
        return new GeneratedKeyMaterial(secret, publicSpki: null);
    }
}
