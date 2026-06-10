// -----------------------------------------------------------------------
// <copyright file="SecretKeyGenerator.cs" company="DCSV">
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
/// Generates opaque symmetric secret keys (e.g. cookie-signing HMAC,
/// client-secret material) — configurable byte length (default 64), no public
/// component.
/// </summary>
/// <param name="options">KeyCustodian options supplying the secret length.</param>
public sealed class SecretKeyGenerator(IOptions<KeyCustodianOptions> options) : IKeyGenerator
{
    /// <inheritdoc/>
    public KeyType Handles => KeyType.Secret;

    /// <inheritdoc/>
    public GeneratedKeyMaterial Generate()
    {
        var secret = RandomNumberGenerator.GetBytes(options.Value.SecretLengthBytes);
        return new GeneratedKeyMaterial(secret, publicSpki: null);
    }
}
