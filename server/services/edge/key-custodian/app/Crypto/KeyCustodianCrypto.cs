// -----------------------------------------------------------------------
// <copyright file="KeyCustodianCrypto.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Crypto;

using System;
using System.Buffers.Text;
using System.Security.Cryptography;

/// <summary>
/// KeyCustodian crypto constants + the kid minter.
/// </summary>
public static class KeyCustodianCrypto
{
    /// <summary>
    /// The keyed-services discriminator under which the root keyring +
    /// <c>IPayloadCrypto</c> are registered. Handlers inject the root crypto via
    /// <c>[FromKeyedServices(KeyCustodianCrypto.ROOT_SERVICE_KEY)] IPayloadCrypto</c>.
    /// </summary>
    public const string ROOT_SERVICE_KEY = "keycustodian-root";

    /// <summary>Number of random bytes a freshly-minted kid is derived from.</summary>
    private const int _KID_ENTROPY_BYTES = 16;

    /// <summary>
    /// Mints a fresh, JWKS-safe kid string: 16 random bytes rendered as unpadded
    /// base64url (22 characters, charset <c>[A-Za-z0-9_-]</c>). The result is
    /// guaranteed to pass <c>Kid.Create</c>.
    /// </summary>
    /// <returns>A new random kid string.</returns>
    public static string MintKid()
    {
        Span<byte> entropy = stackalloc byte[_KID_ENTROPY_BYTES];
        RandomNumberGenerator.Fill(entropy);
        return Base64Url.EncodeToString(entropy);
    }
}
