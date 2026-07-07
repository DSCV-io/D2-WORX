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
/// §1.20). After the sealed-domain removal no production symmetric payload domain remains, so
/// the frozen <c>"d2/&lt;domain&gt;"</c> convention is pinned on the registered fixture
/// payload domains.
/// </summary>
public sealed class KeyringAadProjectionTests
{
    /// <summary>
    /// The frozen expected AAD bytes per fixture payload domain — the UTF-8 code points of
    /// <c>"d2/&lt;domain&gt;"</c>, typed as explicit integers so the expectation is
    /// independent of the production Encoding.UTF8.GetBytes path.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, byte[]> sr_frozen =
        new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            // "d2/payload-fixture-a"
            [FixturePayloadDomains.PAYLOAD_A] =
                [100, 50, 47, 112, 97, 121, 108, 111, 97, 100, 45, 102, 105, 120, 116, 117, 114, 101, 45, 97],

            // "d2/payload-fixture-b"
            [FixturePayloadDomains.PAYLOAD_B] =
                [100, 50, 47, 112, 97, 121, 108, 111, 97, 100, 45, 102, 105, 120, 116, 117, 114, 101, 45, 98],
        };

    [Theory]
    [InlineData(FixturePayloadDomains.PAYLOAD_A)]
    [InlineData(FixturePayloadDomains.PAYLOAD_B)]
    public void For_PayloadDomain_ProducesFrozenBytes(string domainValue)
    {
        using var fixtureSeam = FixturePayloadDomains.Register(
            FixturePayloadDomains.PAYLOAD_A, FixturePayloadDomains.PAYLOAD_B);

        var domain = KeyDomain.Create(domainValue).Data!;

        KeyringAadProjection.For(domain).Should().Equal(
            sr_frozen[domainValue],
            $"the AAD for '{domainValue}' is frozen-for-life — changing it breaks decryption "
            + "of everything already encrypted under the domain");
    }

    [Fact]
    public void For_IsDeterministic_ReturnsEqualButIndependentArrays()
    {
        using var fixtureSeam = FixturePayloadDomains.Register();

        var domain = KeyDomain.Create(FixturePayloadDomains.PAYLOAD_A).Data!;

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
