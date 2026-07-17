// -----------------------------------------------------------------------
// <copyright file="FieldConstraintsRuntimeEmissionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation;

using AwesomeAssertions;
using DcsvIo.D2.Validation.Abstractions;
using Xunit;

/// <summary>
/// Runtime-emission pin tests for the codegen-emitted <see cref="FieldConstraints"/>
/// catalog. Each enumerated bound is asserted against its locked spec value on
/// the EMITTED <c>DcsvIo.D2.Validation.Abstractions</c> surface (not the emitter
/// internals) — a spec edit that changes a value flips exactly the affected row.
/// </summary>
public sealed class FieldConstraintsRuntimeEmissionTests
{
    [Fact]
    public void FirstNameMax_EqualsLockedValue() =>
        FieldConstraints.FIRST_NAME_MAX.Should().Be(255);

    [Fact]
    public void MiddleNameMax_EqualsLockedValue() =>
        FieldConstraints.MIDDLE_NAME_MAX.Should().Be(255);

    [Fact]
    public void LastNameMax_EqualsLockedValue() =>
        FieldConstraints.LAST_NAME_MAX.Should().Be(255);

    [Fact]
    public void PreferredNameMax_EqualsLockedValue() =>
        FieldConstraints.PREFERRED_NAME_MAX.Should().Be(255);

    [Fact]
    public void CompanyNameMax_EqualsLockedValue() =>
        FieldConstraints.COMPANY_NAME_MAX.Should().Be(255);

    [Fact]
    public void JobTitleMax_EqualsLockedValue() =>
        FieldConstraints.JOB_TITLE_MAX.Should().Be(255);

    [Fact]
    public void DepartmentMax_EqualsLockedValue() =>
        FieldConstraints.DEPARTMENT_MAX.Should().Be(255);

    [Fact]
    public void StreetLineMax_EqualsLockedValue() =>
        FieldConstraints.STREET_LINE_MAX.Should().Be(255);

    [Fact]
    public void CityMax_EqualsLockedValue() => FieldConstraints.CITY_MAX.Should().Be(255);

    [Fact]
    public void CompanyWebsiteMax_EqualsLockedValue() =>
        FieldConstraints.COMPANY_WEBSITE_MAX.Should().Be(2048);

    [Fact]
    public void AffixCustomMax_EqualsLockedValue() =>
        FieldConstraints.AFFIX_CUSTOM_MAX.Should().Be(64);

    [Fact]
    public void PostalCodeMax_EqualsLockedValue() =>
        FieldConstraints.POSTAL_CODE_MAX.Should().Be(16);

    [Fact]
    public void EmailMax_EqualsLockedValue() => FieldConstraints.EMAIL_MAX.Should().Be(254);

    [Fact]
    public void PhoneE164Max_EqualsLockedValue() =>
        FieldConstraints.PHONE_E164_MAX.Should().Be(32);

    [Fact]
    public void PhoneMinDigits_EqualsLockedValue() =>
        FieldConstraints.PHONE_MIN_DIGITS.Should().Be(7);

    [Fact]
    public void PhoneMaxDigits_EqualsLockedValue() =>
        FieldConstraints.PHONE_MAX_DIGITS.Should().Be(15);
}
