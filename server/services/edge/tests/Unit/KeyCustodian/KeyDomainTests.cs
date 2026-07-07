// -----------------------------------------------------------------------
// <copyright file="KeyDomainTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// Adversarial unit tests for <see cref="KeyDomain"/>.
/// </summary>
public sealed class KeyDomainTests
{
    // -----------------------------------------------------------------------
    // All catalog — content verification
    // -----------------------------------------------------------------------

    [Fact]
    public void All_DoesNotContainPlaintext()
    {
        // Key design decision: "plaintext" is excluded from the KC catalog
        // because it is a no-encrypt sentinel, not a real keyring.
        KeyDomain.All.Should().NotContain(d => d.Value == "plaintext");
    }

    [Theory]
    [InlineData(KeyDomain.JWKS_SIGNING)]
    [InlineData(KeyDomain.COOKIE)]
    [InlineData(KeyDomain.CLIENT_SECRET)]
    [InlineData(KeyDomain.MTLS_CA_ROOT)]
    [InlineData(KeyDomain.MTLS_CA_INTERMEDIATE)]
    public void All_ContainsExpectedDomains(string domain)
    {
        KeyDomain.All.Should().Contain(d => d.Value == domain);
    }

    // -----------------------------------------------------------------------
    // Create — valid catalog members
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("jwks-signing")]
    [InlineData("cookie")]
    [InlineData("client-secret")]
    [InlineData("mtls-ca-root")]
    [InlineData("mtls-ca-intermediate")]
    public void Create_KnownDomain_ReturnsOk(string domain)
    {
        var result = KeyDomain.Create(domain);
        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be(domain);
    }

    // -----------------------------------------------------------------------
    // Create — plaintext exclusion (design decision pin)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_Plaintext_ReturnsValidationFailed()
    {
        // Explicit pin: "plaintext" is NOT a valid KC key domain.
        var result = KeyDomain.Create("plaintext");
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Create — unknown domain
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_UnknownDomain_ReturnsValidationFailed()
    {
        var result = KeyDomain.Create("unknown-domain");
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Create — null / empty / whitespace
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_Null_ReturnsValidationFailed()
    {
        var result = KeyDomain.Create(null);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Create_Empty_ReturnsValidationFailed()
    {
        var result = KeyDomain.Create(string.Empty);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Create_Whitespace_ReturnsValidationFailed()
    {
        var result = KeyDomain.Create("   ");
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Create — normalization (case + whitespace)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_UpperCaseFixtureDomain_NormalizesToLowercaseAndTrims()
    {
        using var fixtureSeam = FixturePayloadDomains.Register();

        var result = KeyDomain.Create(" PAYLOAD-FIXTURE-A ");
        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be(FixturePayloadDomains.PAYLOAD_A);
    }

    [Fact]
    public void Create_MixedCaseFixtureDomain_NormalizesToLowercase()
    {
        using var fixtureSeam = FixturePayloadDomains.Register();

        var result = KeyDomain.Create("Payload-Fixture-A");
        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be(FixturePayloadDomains.PAYLOAD_A);
    }

    // -----------------------------------------------------------------------
    // FromTrusted
    // -----------------------------------------------------------------------

    [Fact]
    public void FromTrusted_CatalogValue_ResolvesCanonicalEntry()
    {
        using var fixtureSeam = FixturePayloadDomains.Register();

        var domain = KeyDomain.FromTrusted(FixturePayloadDomains.PAYLOAD_A);
        domain.Value.Should().Be(FixturePayloadDomains.PAYLOAD_A);
    }

    [Fact]
    public void FromTrusted_Null_ThrowsArgumentNullException()
    {
        var act = () => KeyDomain.FromTrusted(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromTrusted_Empty_ThrowsArgumentException()
    {
        // A corrupt DB row with an empty domain value must not silently create a
        // KeyDomain with a blank Value — data-corruption, not a valid input path.
        var act = () => KeyDomain.FromTrusted(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromTrusted_Whitespace_ThrowsArgumentException()
    {
        var act = () => KeyDomain.FromTrusted("   ");
        act.Should().Throw<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    // Static accessors
    // -----------------------------------------------------------------------

    [Fact]
    public void JwksSigning_HasCorrectValue()
    {
        KeyDomain.JwksSigning.Value.Should().Be(KeyDomain.JWKS_SIGNING);
    }

    [Fact]
    public void Cookie_HasCorrectValue()
    {
        KeyDomain.Cookie.Value.Should().Be(KeyDomain.COOKIE);
    }

    [Fact]
    public void ClientSecret_HasCorrectValue()
    {
        KeyDomain.ClientSecret.Value.Should().Be(KeyDomain.CLIENT_SECRET);
    }

    [Fact]
    public void MtlsCaRoot_HasCorrectValue()
    {
        KeyDomain.MtlsCaRoot.Value.Should().Be(KeyDomain.MTLS_CA_ROOT);
    }

    [Fact]
    public void MtlsCaIntermediate_HasCorrectValue()
    {
        KeyDomain.MtlsCaIntermediate.Value.Should().Be(KeyDomain.MTLS_CA_INTERMEDIATE);
    }

    // -----------------------------------------------------------------------
    // Seal family — pattern-based (NOT a member of the closed All catalog)
    // -----------------------------------------------------------------------

    [Fact]
    public void All_ContainsNoSealDomain()
    {
        // The seal:<serviceId> family is unbounded (one domain per service, provisioned
        // lazily) — it is resolved by pattern, never a member of the closed catalog.
        KeyDomain.All.Should().NotContain(
            d => d.Value.StartsWith(KeyDomain.SEAL_PREFIX, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("audit", "seal:audit")]
    [InlineData("files", "seal:files")]
    [InlineData("a", "seal:a")]
    [InlineData("a-b-c", "seal:a-b-c")]
    public void ForSeal_ValidServiceId_ReturnsSealDomainBoundToEcdhSealing(
        string serviceId, string expected)
    {
        var result = KeyDomain.ForSeal(serviceId);

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be(expected);
        result.Data!.KeyType.Should().Be(KeyType.EcdhSealing);
    }

    [Fact]
    public void ForSeal_UppercaseServiceId_NormalizesToLowercase()
    {
        var result = KeyDomain.ForSeal("AUDIT");

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("seal:audit");
    }

    [Fact]
    public void ForSeal_ServiceIdExactly64Chars_ReturnsOk()
    {
        var serviceId = new string('a', 64);
        var result = KeyDomain.ForSeal(serviceId);

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("seal:" + serviceId);
    }

    [Fact]
    public void ForSeal_ServiceIdOver64Chars_ReturnsUnknownKeyDomain()
    {
        var result = KeyDomain.ForSeal(new string('a', 65));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad service")]
    [InlineData("under_score")]
    [InlineData("dot.dot")]
    [InlineData("café")]
    [InlineData("UPPER_SNAKE")]
    public void ForSeal_InvalidServiceId_ReturnsUnknownKeyDomain(string? serviceId)
    {
        var result = KeyDomain.ForSeal(serviceId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Create_SealDomain_ResolvesEcdhSealingBinding()
    {
        var result = KeyDomain.Create("seal:audit");

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("seal:audit");
        result.Data!.KeyType.Should().Be(KeyType.EcdhSealing);
    }

    [Fact]
    public void Create_SealDomainUppercase_NormalizesToLowercase()
    {
        var result = KeyDomain.Create("SEAL:AUDIT");

        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("seal:audit");
    }

    [Fact]
    public void Create_SealDomainMalformedSuffix_ReturnsUnknownKeyDomain()
    {
        var result = KeyDomain.Create("seal:bad service");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN);
    }

    [Fact]
    public void FromTrusted_SealDomain_RehydratesEcdhSealing()
    {
        var domain = KeyDomain.FromTrusted("seal:audit");

        domain.Value.Should().Be("seal:audit");
        domain.KeyType.Should().Be(KeyType.EcdhSealing);
    }

    [Fact]
    public void FromTrusted_SealDomainMalformedSuffix_ThrowsArgumentException()
    {
        // A stored seal domain with an invalid suffix is a corrupt row — fail loud.
        var act = () => KeyDomain.FromTrusted("seal:bad service");

        act.Should().Throw<ArgumentException>();
    }
}
