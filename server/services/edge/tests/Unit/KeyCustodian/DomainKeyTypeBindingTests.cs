// -----------------------------------------------------------------------
// <copyright file="DomainKeyTypeBindingTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// Pins the canonical domain→key-type binding carried by every
/// <see cref="KeyDomain"/> catalog entry: the exact per-domain
/// <see cref="KeyType"/> table (a closed-set pin — a new domain without a conscious
/// binding decision trips it), the trust-anchor invariant (the ONLY RSA-signing
/// domain is the minter-only cluster root, and both CA domains are certificate
/// keys — no reachable domain is simultaneously RSA-signing-provisioned and a trust
/// anchor), and the strict <see cref="KeyDomain.FromTrusted"/> read side (a stored
/// value with no binding is data corruption and throws).
/// </summary>
public sealed class DomainKeyTypeBindingTests
{
    // -----------------------------------------------------------------------
    // The canonical binding table — closed-set pin over the WHOLE catalog
    // -----------------------------------------------------------------------

    [Fact]
    public void All_EveryEntryCarriesItsCanonicalBoundKeyType()
    {
        // The load-bearing table: any new domain, removed domain, or changed binding
        // must consciously update this pin (and the provisioning that goes with it).
        var expected = new Dictionary<string, KeyType>(StringComparer.Ordinal)
        {
            [KeyDomain.JWKS_SIGNING] = KeyType.RsaSigning,
            [KeyDomain.COOKIE] = KeyType.Secret,
            [KeyDomain.CLIENT_SECRET] = KeyType.Secret,
            [KeyDomain.MTLS_CA_ROOT] = KeyType.X509CaCertificate,
            [KeyDomain.MTLS_CA_INTERMEDIATE] = KeyType.X509CaCertificate,
            ["audit"] = KeyType.AesPayload,
            ["notifications"] = KeyType.AesPayload,
            ["courier"] = KeyType.AesPayload,
        };

        var actual = KeyDomain.All.ToDictionary(
            d => d.Value, d => d.KeyType, StringComparer.Ordinal);

        actual.Should().Equal(
            expected,
            "every catalog entry carries exactly its canonical bound key type — a new "
            + "domain without a conscious binding decision must trip this pin");
    }

    [Theory]
    [InlineData("audit", KeyType.AesPayload)]
    [InlineData(KeyDomain.JWKS_SIGNING, KeyType.RsaSigning)]
    [InlineData(KeyDomain.COOKIE, KeyType.Secret)]
    [InlineData(KeyDomain.MTLS_CA_ROOT, KeyType.X509CaCertificate)]
    public void Create_ResolvedEntry_CarriesTheBoundKeyType(string domain, KeyType expected)
    {
        KeyDomain.Create(domain).Data!.KeyType.Should().Be(expected);
    }

    // -----------------------------------------------------------------------
    // The trust-anchor invariant, pinned structurally: the RSA-signing set is
    // EXACTLY the minter-only cluster root; both CA domains are certificate keys.
    // -----------------------------------------------------------------------

    [Fact]
    public void RsaSigningBoundDomains_IsExactlyJwksSigning_AndMinterOnly()
    {
        var rsaBound = KeyDomain.All
            .Where(d => d.KeyType == KeyType.RsaSigning)
            .Select(d => d.Value)
            .ToList();

        rsaBound.Should().BeEquivalentTo(
            new[] { KeyDomain.JWKS_SIGNING },
            "exactly one catalog domain is RSA-signing-provisioned — the cluster root; "
            + "a second one would silently widen the raw-signing surface");

        WorkloadCapabilityAuthority.MinterOnlySigningDomains
            .Should().Contain(
                KeyDomain.JWKS_SIGNING,
                "the sole RSA-signing domain is reachable only through the minter");
    }

    [Fact]
    public void CaDomains_AreCertificateBound_NeverRsaSigningProvisioned()
    {
        // No reachable domain is simultaneously RSA-signing-provisioned and a trust
        // anchor: both CA domains are bound to X509CaCertificate by data, so the
        // combination is uncompilable-by-data rather than an unstated convention.
        KeyDomain.MtlsCaRoot.KeyType.Should().Be(KeyType.X509CaCertificate);
        KeyDomain.MtlsCaIntermediate.KeyType.Should().Be(KeyType.X509CaCertificate);
    }

    // -----------------------------------------------------------------------
    // FromTrusted — strict fail-loud read side
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("not-a-real-domain")]
    [InlineData("plaintext")]
    [InlineData(" audit ")]
    public void FromTrusted_NonCatalogStoredValue_Throws_DataCorruption(string stored)
    {
        // A stored domain value with no catalog entry has no key-type binding — data
        // corruption (every valid row is written through Create-validated paths).
        var act = () => KeyDomain.FromTrusted(stored);

        act.Should().Throw<ArgumentException>(
            "a corrupt row must fail loud, never silently produce a domain without "
            + "a key-type binding");
    }

    [Theory]
    [InlineData("audit", "audit", KeyType.AesPayload)]
    [InlineData("JWKS-SIGNING", KeyDomain.JWKS_SIGNING, KeyType.RsaSigning)]
    [InlineData("Mtls-Ca-Root", KeyDomain.MTLS_CA_ROOT, KeyType.X509CaCertificate)]
    public void FromTrusted_CatalogValue_ResolvesCanonicalEntryWithBinding(
        string stored, string expectedValue, KeyType expectedType)
    {
        // Case-insensitive resolution: a legacy non-lowercase stored value still
        // resolves to its canonical catalog entry (binding attached), never to a
        // bindingless verbatim wrapper.
        var domain = KeyDomain.FromTrusted(stored);

        domain.Value.Should().Be(expectedValue);
        domain.KeyType.Should().Be(expectedType);
    }
}
