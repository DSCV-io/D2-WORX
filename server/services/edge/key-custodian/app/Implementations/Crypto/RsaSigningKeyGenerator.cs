// -----------------------------------------------------------------------
// <copyright file="RsaSigningKeyGenerator.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Implementations.Crypto;

using System.Security.Cryptography;
using D2.Edge.KeyCustodian.App.Crypto;
using D2.Edge.KeyCustodian.App.Interfaces.Crypto;
using D2.Edge.KeyCustodian.App.Options;
using D2.Edge.KeyCustodian.Domain.Enums;
using Microsoft.Extensions.Options;

/// <summary>
/// Generates RSA signing key pairs (RS256). Emits the PKCS#8-encoded private key
/// as the plaintext material (to be root-wrapped) and the SPKI-encoded public
/// key as the public component (published via JWKS).
/// </summary>
/// <param name="options">KeyCustodian options supplying the RSA modulus size.</param>
public sealed class RsaSigningKeyGenerator(IOptions<KeyCustodianOptions> options) : IKeyGenerator
{
    /// <inheritdoc/>
    public KeyType Handles => KeyType.RsaSigning;

    /// <inheritdoc/>
    public GeneratedKeyMaterial Generate()
    {
        using var rsa = RSA.Create(options.Value.RsaKeySizeBits);
        var pkcs8 = rsa.ExportPkcs8PrivateKey();
        var spki = rsa.ExportSubjectPublicKeyInfo();
        return new GeneratedKeyMaterial(pkcs8, spki);
    }
}
