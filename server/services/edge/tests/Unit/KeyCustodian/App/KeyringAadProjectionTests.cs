// -----------------------------------------------------------------------
// <copyright file="KeyringAadProjectionTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

/// <summary>
/// FREEZE tests for <see cref="KeyringAadProjection"/>. The AAD is authenticated context
/// bound into every (en|de)crypt operation for a payload domain — changing the projection
/// would make everything already encrypted under the old AAD fail to decrypt. These pins
/// carry the exact byte layout per payload domain as INDEPENDENT literal arrays (hand-typed
/// UTF-8 code points, NOT recomputed via the same Encoding.UTF8 path — non-tautological per
/// §1.20), plus a catalog-walk so a NEW payload domain fails the freeze until it is pinned.
/// </summary>
public sealed class KeyringAadProjectionTests
{
    private const string _AUDIT = "audit";

    /// <summary>
    /// The frozen expected AAD bytes per payload domain — the UTF-8 code points of
    /// <c>"d2/&lt;domain&gt;"</c>, typed as explicit integers so the expectation is
    /// independent of the production Encoding.UTF8.GetBytes path.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, byte[]> sr_frozen =
        new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            // "d2/audit"
            [_AUDIT] = [100, 50, 47, 97, 117, 100, 105, 116],

            // "d2/notifications"
            ["notifications"] =
                [100, 50, 47, 110, 111, 116, 105, 102, 105, 99, 97, 116, 105, 111, 110, 115],

            // "d2/courier"
            ["courier"] = [100, 50, 47, 99, 111, 117, 114, 105, 101, 114],
        };

    [Theory]
    [InlineData(_AUDIT)]
    [InlineData("notifications")]
    [InlineData("courier")]
    public void For_PayloadDomain_ProducesFrozenBytes(string domainValue)
    {
        var domain = KeyDomain.Create(domainValue).Data!;

        KeyringAadProjection.For(domain).Should().Equal(
            sr_frozen[domainValue],
            $"the AAD for '{domainValue}' is frozen-for-life — changing it breaks decryption "
            + "of everything already encrypted under the domain");
    }

    [Fact]
    public void For_EveryPayloadCatalogDomain_HasAPinnedFreezeRow()
    {
        // A NEW payload domain added to the catalog fails this test until its exact AAD is
        // pinned in sr_frozen — the freeze cannot silently drift as the catalog grows.
        var payloadDomains = KeyDomain.All
            .Where(d => d.KeyType == KeyType.AesPayload)
            .Select(d => d.Value)
            .ToList();

        payloadDomains.Should().OnlyContain(
            d => sr_frozen.ContainsKey(d),
            "every AesPayload catalog domain must have a pinned AAD freeze row");

        // And the freeze set carries no stale entry for a removed domain.
        sr_frozen.Keys.Should().OnlyContain(
            k => payloadDomains.Contains(k),
            "the freeze set carries no stale entry for a non-catalog domain");
    }

    [Fact]
    public void For_IsDeterministic_ReturnsEqualButIndependentArrays()
    {
        var domain = KeyDomain.Create(_AUDIT).Data!;

        var first = KeyringAadProjection.For(domain);
        var second = KeyringAadProjection.For(domain);

        first.Should().Equal(second, "the projection is deterministic per domain");
        first.Should().NotBeSameAs(second, "each call returns a fresh array the caller owns");
    }

    [Fact]
    public void For_NullDomain_Throws()
    {
        var act = () => KeyringAadProjection.For(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
