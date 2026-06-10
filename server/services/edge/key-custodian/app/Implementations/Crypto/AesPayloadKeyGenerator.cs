// -----------------------------------------------------------------------
// <copyright file="AesPayloadKeyGenerator.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Implementations.Crypto;

using System.Security.Cryptography;
using D2.Edge.KeyCustodian.App.Crypto;
using D2.Edge.KeyCustodian.App.Interfaces.Crypto;
using D2.Edge.KeyCustodian.Domain.Enums;

/// <summary>
/// Generates AES-256 payload encryption keys — 32 random bytes, no public
/// component.
/// </summary>
public sealed class AesPayloadKeyGenerator : IKeyGenerator
{
    /// <summary>AES-256 key length in bytes.</summary>
    private const int _AES_256_KEY_BYTES = 32;

    /// <inheritdoc/>
    public KeyType Handles => KeyType.AesPayload;

    /// <inheritdoc/>
    public GeneratedKeyMaterial Generate()
    {
        var key = RandomNumberGenerator.GetBytes(_AES_256_KEY_BYTES);
        return new GeneratedKeyMaterial(key, publicSpki: null);
    }
}
