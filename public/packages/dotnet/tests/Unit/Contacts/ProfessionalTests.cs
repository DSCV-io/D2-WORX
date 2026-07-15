// -----------------------------------------------------------------------
// <copyright file="ProfessionalTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Contacts;

using AwesomeAssertions;
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Validation.Abstractions;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="Professional"/>: company-name-required
/// floor, per-field length caps, and the company-website URL parser —
/// scheme enforcement, relative-URL rejection, and security-adversarial schemes.
/// </summary>
public sealed class ProfessionalTests
{
    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_AllFields_ReturnsOk_Cleaned()
    {
        var result = Professional.Create(
            "  DCSV  ", "  Engineer  ", "  Platform  ", "https://example.com");

        result.Success.Should().BeTrue();
        var pro = result.Data!;
        pro.CompanyName.Should().Be("DCSV");
        pro.JobTitle.Should().Be("Engineer");
        pro.Department.Should().Be("Platform");
        pro.CompanyWebsite.Should().Be(new Uri("https://example.com"));
    }

    [Fact]
    public void Create_CompanyNameOnly_ReturnsOk_OptionalsNull()
    {
        var result = Professional.Create("DCSV");

        result.Success.Should().BeTrue();
        result.Data!.JobTitle.Should().BeNull();
        result.Data!.Department.Should().BeNull();
        result.Data!.CompanyWebsite.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // CompanyName required
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Create_CompanyNameMissing_ReturnsCompanyNameRequired(string? companyName)
    {
        var result = Professional.Create(companyName);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.COMPANY_NAME_REQUIRED.Key);
    }

    // -----------------------------------------------------------------------
    // Length caps
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_CompanyNameExactlyMax_ReturnsOk()
    {
        var atMax = new string('a', FieldConstraints.COMPANY_NAME_MAX);
        var result = Professional.Create(atMax);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Create_CompanyNameOverMax_ReturnsCompanyNameTooLong()
    {
        var overMax = new string('a', FieldConstraints.COMPANY_NAME_MAX + 1);
        var result = Professional.Create(overMax);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.COMPANY_NAME_TOO_LONG.Key);
    }

    [Fact]
    public void Create_JobTitleExactlyMax_ReturnsOk()
    {
        var atMax = new string('j', FieldConstraints.JOB_TITLE_MAX);
        var result = Professional.Create("DCSV", atMax);

        result.Success.Should().BeTrue();
        result.Data!.JobTitle.Should().HaveLength(FieldConstraints.JOB_TITLE_MAX);
    }

    [Fact]
    public void Create_JobTitleOverMax_ReturnsJobTitleTooLong()
    {
        var overMax = new string('b', FieldConstraints.JOB_TITLE_MAX + 1);
        var result = Professional.Create("DCSV", overMax);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.JOB_TITLE_TOO_LONG.Key);
    }

    [Fact]
    public void Create_DepartmentExactlyMax_ReturnsOk()
    {
        var atMax = new string('d', FieldConstraints.DEPARTMENT_MAX);
        var result = Professional.Create("DCSV", department: atMax);

        result.Success.Should().BeTrue();
        result.Data!.Department.Should().HaveLength(FieldConstraints.DEPARTMENT_MAX);
    }

    [Fact]
    public void Create_DepartmentOverMax_ReturnsDepartmentTooLong()
    {
        var overMax = new string('c', FieldConstraints.DEPARTMENT_MAX + 1);
        var result = Professional.Create("DCSV", department: overMax);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.DEPARTMENT_TOO_LONG.Key);
    }

    // -----------------------------------------------------------------------
    // Website — valid
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_NoWebsite_ReturnsOk_WebsiteNull()
    {
        var result = Professional.Create("DCSV");

        result.Success.Should().BeTrue();
        result.Data!.CompanyWebsite.Should().BeNull();
    }

    [Fact]
    public void Create_HttpWebsite_ReturnsOk()
    {
        var result = Professional.Create("DCSV", companyWebsite: "http://example.com");

        result.Success.Should().BeTrue();
        result.Data!.CompanyWebsite!.Scheme.Should().Be(Uri.UriSchemeHttp);
    }

    [Fact]
    public void Create_HttpsWebsiteWithPathAndQuery_ReturnsOk()
    {
        var result = Professional.Create(
            "DCSV", companyWebsite: "https://example.com/path?q=1&r=2");

        result.Success.Should().BeTrue();
        result.Data!.CompanyWebsite.Should().Be(new Uri("https://example.com/path?q=1&r=2"));
    }

    [Fact]
    public void Create_MixedCaseScheme_ReturnsOk_SchemeLowercased()
    {
        var result = Professional.Create("DCSV", companyWebsite: "HTTP://example.com");

        result.Success.Should().BeTrue();
        result.Data!.CompanyWebsite!.Scheme.Should().Be(Uri.UriSchemeHttp);
    }

    // -----------------------------------------------------------------------
    // Website — invalid (adversarial)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("not a url")]
    [InlineData("example.com")]
    [InlineData("//example.com")]
    [InlineData("ftp://host")]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("mailto:user@example.com")]
    public void Create_InvalidWebsite_ReturnsWebsiteInvalid(string website)
    {
        var result = Professional.Create("DCSV", companyWebsite: website);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.WEBSITE_INVALID.Key);
    }

    [Fact]
    public void Create_WebsiteOverMaxRawLength_ReturnsWebsiteInvalid()
    {
        var longPath = new string('a', FieldConstraints.COMPANY_WEBSITE_MAX);
        var overMax = "https://example.com/" + longPath;
        var result = Professional.Create("DCSV", companyWebsite: overMax);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.WEBSITE_INVALID.Key);
    }
}
