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
    [InlineData("audit")]
    [InlineData("notifications")]
    [InlineData("courier")]
    [InlineData(KeyDomain.JWKS_SIGNING)]
    [InlineData(KeyDomain.COOKIE)]
    [InlineData(KeyDomain.CLIENT_SECRET)]
    [InlineData(KeyDomain.MTLS_CA_ROOT)]
    [InlineData(KeyDomain.MTLS_CA_INTERMEDIATE)]
    public void All_ContainsExpectedDomains(string domain)
    {
        KeyDomain.All.Should().Contain(d => d.Value == domain);
    }

    [Fact]
    public void All_ContainsExactly8Entries()
    {
        // audit, notifications, courier (3 non-plaintext from EncryptionDomains)
        // + jwks-signing, cookie, client-secret, mtls-ca-root, mtls-ca-intermediate
        // (5 KC-only) = 8
        KeyDomain.All.Count.Should().Be(8);
    }

    // -----------------------------------------------------------------------
    // Create — valid catalog members
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("audit")]
    [InlineData("notifications")]
    [InlineData("courier")]
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
    public void Create_UpperCaseAudit_NormalizesToLowercase()
    {
        var result = KeyDomain.Create(" AUDIT ");
        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("audit");
    }

    [Fact]
    public void Create_MixedCaseNotifications_NormalizesToLowercase()
    {
        var result = KeyDomain.Create("Notifications");
        result.Success.Should().BeTrue();
        result.Data!.Value.Should().Be("notifications");
    }

    // -----------------------------------------------------------------------
    // FromTrusted
    // -----------------------------------------------------------------------

    [Fact]
    public void FromTrusted_CatalogValue_ResolvesCanonicalEntry()
    {
        var domain = KeyDomain.FromTrusted("audit");
        domain.Value.Should().Be("audit");
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
}
