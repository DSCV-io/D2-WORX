// -----------------------------------------------------------------------
// <copyright file="KidMinting.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Rules;

/// <summary>
/// Pure rule that mints a fresh, JWKS-safe key identifier.
/// </summary>
public static class KidMinting
{
    /// <summary>Number of random bytes a freshly-minted kid is derived from.</summary>
    private const int _KID_ENTROPY_BYTES = 16;

    /// <summary>
    /// Mints a fresh, JWKS-safe kid string: 16 random bytes rendered as unpadded
    /// base64url (22 characters, charset <c>[A-Za-z0-9_-]</c>). The result is
    /// guaranteed to pass <c>Kid.Create</c>.
    /// </summary>
    /// <returns>A new random kid string.</returns>
    public static string Mint()
    {
        Span<byte> entropy = stackalloc byte[_KID_ENTROPY_BYTES];
        RandomNumberGenerator.Fill(entropy);
        return Base64Url.EncodeToString(entropy);
    }
}
