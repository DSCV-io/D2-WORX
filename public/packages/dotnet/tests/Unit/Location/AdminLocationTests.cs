// -----------------------------------------------------------------------
// <copyright file="AdminLocationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Location;

using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Location;
using DcsvIo.D2.Location.ValueObjects;
using DcsvIo.D2.Result;
using DcsvIo.D2.Validation.Abstractions;
using Xunit;
using LocationPostalCodeValidator = DcsvIo.D2.Location.IPostalCodeValidator;

/// <summary>
/// Adversarial test coverage for <see cref="AdminLocation"/> per §7.1 matrix:
/// all 6 country/subdivision coherence cases, all-null
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
    // Case 1: all-null rejects
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_AllNullFields_ReturnsValidationFailed()
    {
        var result = AdminLocation.Create();
        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.ADMIN_EMPTY_RECORD.Key);
    }

    [Fact]
    public void Create_AllWhitespaceFields_ReturnsValidationFailed()
    {
        var result = AdminLocation.Create(city: "   ", postalCode: "   ");
        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.ADMIN_EMPTY_RECORD.Key);
    }

    // -----------------------------------------------------------------------
    // Case 2: country-only valid
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
    // Case 3: subdivision-only auto-populates country
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
    // Case 5: mismatch fails
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_CountryAndMismatchedSubdivision_ReturnsValidationFailed()
    {
        var sub = SubdivisionCode.FromString("US-NY");
        var result = AdminLocation.Create(CountryCode.CA, sub);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.ADMIN_COUNTRY_SUBDIVISION_MISMATCH.Key);
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
            .Which.Key.Should().Be(TK.Geo.Validation.POSTAL_CODE_INVALID.Key);
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

    // -----------------------------------------------------------------------
    // Regression: BubbleFail propagates full upstream failure metadata
    //
    // Before the fix, AdminLocation used ValidationFailed(messages: ...) which
    // dropped InputErrors (and StatusCode/ErrorCode/TraceId) from the upstream
    // postal-validator result. After the fix, BubbleFail is used and ALL
    // upstream failure metadata is preserved.
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_PostalValidatorFailureWithInputErrors_InputErrorsPropagated()
    {
        // Arrange — a stub validator that returns ValidationFailed with InputErrors.
        var validator = new StubPostalCodeValidatorWithInputErrors();

        // Act — before the BubbleFail fix, InputErrors would have been dropped.
        var result = AdminLocation.Create(
            CountryCode.US,
            postalCode: "INVALID",
            postalCodeValidator: validator);

        // Assert — failure AND InputErrors must be present.
        result.Success.Should().BeFalse();
        result.InputErrors.Should().NotBeNullOrEmpty();
        result.InputErrors.Should().ContainSingle()
            .Which.Field.Should().Be("postalCode");
    }

    // -----------------------------------------------------------------------
    // Length caps — City
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_City_ExactlyMax_ReturnsOk()
    {
        var result = AdminLocation.Create(city: new string('a', FieldConstraints.CITY_MAX));
        result.Success.Should().BeTrue();
        result.Data!.City!.Length.Should().Be(FieldConstraints.CITY_MAX);
    }

    [Fact]
    public void Create_City_OverMax_ReturnsTooLong()
    {
        var result = AdminLocation.Create(city: new string('a', FieldConstraints.CITY_MAX + 1));
        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.CITY_TOO_LONG.Key);
    }

    // -----------------------------------------------------------------------
    // Cap is measured on the post-clean value — City (T8)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_City_RawOver_CollapsesBelowMax_ReturnsOk()
    {
        // Raw input is CITY_MAX chars of 'a' followed by a trailing space.
        // CleanStr() trims trailing whitespace → post-clean length == CITY_MAX → accepted.
        var city = new string('a', FieldConstraints.CITY_MAX) + " ";
        var result = AdminLocation.Create(city: city);
        result.Success.Should().BeTrue();
        result.Data!.City!.Length.Should().Be(FieldConstraints.CITY_MAX);
    }

    // -----------------------------------------------------------------------
    // Hash-stability regression — golden-value pin (T9)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_HashId_KnownInput_MatchesGoldenValue()
    {
        // Pin: AdminLocation.Create(CountryCode.US) →
        //   hash input "|||US" → SHA-256(UTF-8) → v1.<64-hex>.
        // Golden value sourced from parity-fixtures.json cases
        // "admin-only-us-country" and "admin-country-only-us".
        // A hash algorithm change causes this to fail and prevents silent drift.
        const string expected_hash =
            "v1.3e2a439dd69d750bc122573cfa99710970cdb7d95342a4aa731a0245cd946981";
        var result = AdminLocation.Create(CountryCode.US);
        result.Data!.HashId.Should().Be(expected_hash);
    }

    // -----------------------------------------------------------------------
    // Length caps — PostalCode
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_PostalCode_ExactlyMax_ReturnsOk()
    {
        // POSTAL_CODE_MAX=16 — use a 16-char alphanumeric string (no validator).
        var result = AdminLocation.Create(
            postalCode: new string('9', FieldConstraints.POSTAL_CODE_MAX));
        result.Success.Should().BeTrue();
        result.Data!.PostalCode!.Length.Should().Be(FieldConstraints.POSTAL_CODE_MAX);
    }

    [Fact]
    public void Create_PostalCode_OverMax_ReturnsTooLong()
    {
        var result = AdminLocation.Create(
            postalCode: new string('9', FieldConstraints.POSTAL_CODE_MAX + 1));
        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.POSTAL_CODE_TOO_LONG.Key);
    }

    // -----------------------------------------------------------------------
    // Floor-before-validator composition
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_PostalCode_OverMax_WithValidator_CapFailsBeforeValidator()
    {
        // An over-max postal must fail with POSTAL_CODE_TOO_LONG and the
        // validator must never be invoked — the cap is the unconditional floor.
        var spy = new SpyPostalCodeValidator();
        var result = AdminLocation.Create(
            postalCode: new string('9', FieldConstraints.POSTAL_CODE_MAX + 1),
            postalCodeValidator: spy);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Validation.POSTAL_CODE_TOO_LONG.Key);
        spy.WasInvoked.Should().BeFalse();
    }

    [Fact]
    public void Create_PostalCode_ExactlyMax_WithoutValidator_ReturnsOk()
    {
        // A 16-char postal with no validator passes the floor and is accepted.
        var result = AdminLocation.Create(
            postalCode: new string('9', FieldConstraints.POSTAL_CODE_MAX));
        result.Success.Should().BeTrue();
        result.Data!.PostalCode!.Length.Should().Be(FieldConstraints.POSTAL_CODE_MAX);
    }

    // -----------------------------------------------------------------------
    // Stub helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Stub validator that fails with a <see cref="D2Result{TData}.ValidationFailed"/>
    /// carrying per-field <see cref="InputError"/> entries — used to verify that
    /// <see cref="AdminLocation.Create"/> propagates the full upstream failure via
    /// <see cref="D2Result{TData}.BubbleFail"/> rather than reconstructing a partial
    /// failure that drops <c>InputErrors</c>.
    /// </summary>
    private sealed class StubPostalCodeValidatorWithInputErrors : LocationPostalCodeValidator
    {
        public D2Result<string> Validate(string? postalCode, CountryCode? countryCode = null)
            => D2Result<string>.ValidationFailed(
                messages: [TK.Geo.Validation.POSTAL_CODE_INVALID],
                inputErrors: [new InputError(
                    "postalCode",
                    [TK.Geo.Validation.POSTAL_CODE_INVALID])]);
    }

    /// <summary>
    /// Spy validator that records whether it was invoked — used to prove
    /// that the length-cap floor fires BEFORE the validator is called.
    /// </summary>
    private sealed class SpyPostalCodeValidator : LocationPostalCodeValidator
    {
        public bool WasInvoked { get; private set; }

        public D2Result<string> Validate(string? postalCode, CountryCode? countryCode = null)
        {
            WasInvoked = true;
            return D2Result<string>.Ok(postalCode ?? string.Empty);
        }
    }
}
