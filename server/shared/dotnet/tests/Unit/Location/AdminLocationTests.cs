// -----------------------------------------------------------------------
// <copyright file="AdminLocationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Location;

using AwesomeAssertions;
using D2.Shared.Geo.Abstractions;
using D2.Shared.Location;
using D2.Shared.Location.ValueObjects;
using D2.Shared.Result;
using Xunit;

/// <summary>
/// Adversarial test coverage for <see cref="AdminLocation"/> per §7.1 matrix:
/// all 6 country/subdivision coherence cases (Amendment 50), all-null
/// rejection, postal-code validator integration, city / postal cleaning,
/// CRLF / NUL injection in city + postal, HashId invariants.
/// </summary>
public sealed class AdminLocationTests
{
    // -----------------------------------------------------------------------
    // Happy path — country + matching subdivision
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_CountryAndMatchingSubdivision_ReturnsOk()
    {
        var sub = SubdivisionCode.FromString("US-NY");
        var result = AdminLocation.Create(CountryCode.US, sub);

        result.Success.Should().BeTrue();
        result.Data!.CountryIso31661Alpha2Code.Should().Be(CountryCode.US);
        result.Data!.SubdivisionIso31662Code.Should().Be(sub);
    }

    // -----------------------------------------------------------------------
    // Amendment 50 — Case 1: all-null rejects
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_AllNullFields_ReturnsValidationFailed()
    {
        var result = AdminLocation.Create();
        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be("geo_validation_admin_empty_record");
    }

    [Fact]
    public void Create_AllWhitespaceFields_ReturnsValidationFailed()
    {
        var result = AdminLocation.Create(city: "   ", postalCode: "   ");
        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be("geo_validation_admin_empty_record");
    }

    // -----------------------------------------------------------------------
    // Amendment 50 — Case 2: country-only valid
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_CountryOnly_ReturnsOk()
    {
        var result = AdminLocation.Create(CountryCode.US);
        result.Success.Should().BeTrue();
        result.Data!.CountryIso31661Alpha2Code.Should().Be(CountryCode.US);
        result.Data!.SubdivisionIso31662Code.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Amendment 50 — Case 3: subdivision-only auto-populates country
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_SubdivisionOnly_AutoPopulatesCountry()
    {
        var sub = SubdivisionCode.FromString("US-NY");
        var result = AdminLocation.Create(subdivisionIso31662Code: sub);

        result.Success.Should().BeTrue();
        result.Data!.CountryIso31661Alpha2Code.Should().Be(CountryCode.US);
        result.Data!.SubdivisionIso31662Code.Should().Be(sub);
    }

    // -----------------------------------------------------------------------
    // Amendment 50 — Case 5: mismatch fails
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_CountryAndMismatchedSubdivision_ReturnsValidationFailed()
    {
        var sub = SubdivisionCode.FromString("US-NY");
        var result = AdminLocation.Create(CountryCode.CA, sub);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be("geo_validation_admin_country_subdivision_mismatch");
    }

    // -----------------------------------------------------------------------
    // City-only / postal-only — non-degenerate single-field cases
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_CityOnly_ReturnsOk()
    {
        var result = AdminLocation.Create(city: "Brooklyn");
        result.Success.Should().BeTrue();
        result.Data!.City.Should().Be("Brooklyn");
    }

    [Fact]
    public void Create_PostalOnlyNoValidator_ReturnsOk()
    {
        var result = AdminLocation.Create(postalCode: "90210");
        result.Success.Should().BeTrue();
        result.Data!.PostalCode.Should().Be("90210");
    }

    // -----------------------------------------------------------------------
    // City normalization
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_CityWhitespaceCollapsed()
    {
        var result = AdminLocation.Create(city: "  New    York  ");
        result.Success.Should().BeTrue();
        result.Data!.City.Should().Be("New York");
    }

    [Fact]
    public void Create_CityEmpty_StoredAsNull()
    {
        var result = AdminLocation.Create(CountryCode.US, city: string.Empty);
        result.Success.Should().BeTrue();
        result.Data!.City.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // Postal code validator integration
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_PostalValidatorReturnsValidationFailed_PropagatesFailure()
    {
        var result = AdminLocation.Create(
            CountryCode.US,
            postalCode: "AB",
            postalCodeValidator: new DefaultPostalCodeValidator());

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be("geo_validation_postal_code_invalid");
    }

    [Fact]
    public void Create_PostalValidatorReturnsOk_AdminLocationOk()
    {
        var result = AdminLocation.Create(
            CountryCode.US,
            postalCode: "90210",
            postalCodeValidator: new DefaultPostalCodeValidator());

        result.Success.Should().BeTrue();
        result.Data!.PostalCode.Should().Be("90210");
    }

    [Fact]
    public void Create_PostalCodeNullAndValidatorSupplied_NoValidationPerformed()
    {
        var result = AdminLocation.Create(
            CountryCode.US,
            postalCodeValidator: new DefaultPostalCodeValidator());

        result.Success.Should().BeTrue();
        result.Data!.PostalCode.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // HashId invariants
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_HashId_StartsWithV1Prefix()
    {
        var result = AdminLocation.Create(CountryCode.US, city: "Brooklyn");
        result.Data!.HashId.Should().StartWith("v1.");
    }

    [Fact]
    public void Create_HashId_HasCorrectLength()
    {
        var result = AdminLocation.Create(CountryCode.US);
        result.Data!.HashId.Length.Should().Be(67);
    }

    [Fact]
    public void Create_HashId_Deterministic_AcrossCalls()
    {
        var r1 = AdminLocation.Create(CountryCode.US, city: "Brooklyn", postalCode: "11201");
        var r2 = AdminLocation.Create(CountryCode.US, city: "Brooklyn", postalCode: "11201");

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    [Fact]
    public void Create_HashId_DiffersByCountry()
    {
        var r1 = AdminLocation.Create(CountryCode.US);
        var r2 = AdminLocation.Create(CountryCode.CA);

        r1.Data!.HashId.Should().NotBe(r2.Data!.HashId);
    }

    [Fact]
    public void Create_SubdivisionOnly_AndExplicitMatching_ProduceSameHashId()
    {
        var sub = SubdivisionCode.FromString("US-NY");
        var r1 = AdminLocation.Create(subdivisionIso31662Code: sub);
        var r2 = AdminLocation.Create(CountryCode.US, sub);

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    // -----------------------------------------------------------------------
    // Adversarial — CRLF / NUL in city + postal
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_CityCrLfInjected_StrippedOrCollapsedInStoredForm()
    {
        var result = AdminLocation.Create(CountryCode.US, city: "Brooklyn\r\nINJECTED");
        result.Success.Should().BeTrue();
        result.Data!.City.Should().NotContain("\r");
        result.Data!.City.Should().NotContain("\n");
    }

    [Fact]
    public void Create_PostalCrLfInjected_RegexRejects()
    {
        var result = AdminLocation.Create(
            CountryCode.US,
            postalCode: "90210\r\nINJECTED",
            postalCodeValidator: new DefaultPostalCodeValidator());

        result.Success.Should().BeFalse();
    }
}
