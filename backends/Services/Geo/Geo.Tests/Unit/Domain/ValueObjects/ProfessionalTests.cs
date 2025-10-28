using FluentAssertions;
using Geo.Domain.Exceptions;
using Geo.Domain.ValueObjects;
using Xunit;

namespace Geo.Tests.Unit.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="Geo.Domain.ValueObjects.Professional"/>.
/// </summary>
public class ProfessionalTests
{
    #region Valid Creation

    [Fact]
    public void Create_WithCompanyNameOnly_Success()
    {
        // Arrange
        const string company_name = "ACME, LLC";

        // Act
        var professional = Professional.Create(company_name);

        // Assert
        professional.Should().NotBeNull();
        professional.CompanyName.Should().Be("ACME, LLC");
        professional.JobTitle.Should().BeNull();
        professional.Department.Should().BeNull();
        professional.CompanyWebsite.Should().BeNull();
    }

    [Fact]
    public void Create_WithAllFields_Success()
    {
        // Arrange
        const string company_name = "ACME, LLC";
        const string job_title = "Software Engineer";
        const string department = "Research and Development";
        var website = new Uri("https://www.acme.com");

        // Act
        var professional = Professional.Create(company_name, job_title, department, website);

        // Assert
        professional.Should().NotBeNull();
        professional.CompanyName.Should().Be("ACME, LLC");
        professional.JobTitle.Should().Be("Software Engineer");
        professional.Department.Should().Be("Research and Development");
        professional.CompanyWebsite.Should().Be(website);
    }

    [Fact]
    public void Create_WithJobTitleOnly_Success()
    {
        // Arrange
        const string company_name = "ACME, LLC";
        const string job_title = "Software Engineer";

        // Act
        var professional = Professional.Create(company_name, job_title);

        // Assert
        professional.CompanyName.Should().Be("ACME, LLC");
        professional.JobTitle.Should().Be("Software Engineer");
        professional.Department.Should().BeNull();
        professional.CompanyWebsite.Should().BeNull();
    }

    #endregion

    #region CompanyName Validation - Required

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Create_WithInvalidCompanyName_ThrowsGeoValidationException(string? companyName)
    {
        // Act
        var act = () => Professional.Create(companyName!);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion

    #region Clean Strings - No Changes

    [Fact]
    public void Create_WithCleanCompanyName_NoChanges()
    {
        // Arrange
        const string clean_company_name = "ACME, LLC";

        // Act
        var professional = Professional.Create(clean_company_name);

        // Assert
        professional.CompanyName.Should().Be(clean_company_name);
    }

    [Fact]
    public void Create_WithAllCleanFields_NoChanges()
    {
        // Arrange
        const string company_name = "ACME, LLC";
        const string job_title = "Software Engineer";
        const string department = "Research and Development";

        // Act
        var professional = Professional.Create(company_name, job_title, department);

        // Assert
        professional.CompanyName.Should().Be(company_name);
        professional.JobTitle.Should().Be(job_title);
        professional.Department.Should().Be(department);
    }

    #endregion

    #region Dirty Strings - Cleanup

    [Fact]
    public void Create_WithDirtyCompanyName_CleansWhitespace()
    {
        // Arrange
        const string dirty_company_name = "  ACME,   LLC  ";

        // Act
        var professional = Professional.Create(dirty_company_name);

        // Assert
        professional.CompanyName.Should().Be("ACME, LLC");
    }

    [Fact]
    public void Create_WithDirtyJobTitle_CleansWhitespace()
    {
        // Arrange
        const string company_name = "ACME, LLC";
        const string dirty_job_title = "  Software   Engineer  ";

        // Act
        var professional = Professional.Create(company_name, dirty_job_title);

        // Assert
        professional.JobTitle.Should().Be("Software Engineer");
    }

    [Fact]
    public void Create_WithDirtyDepartment_CleansWhitespace()
    {
        // Arrange
        const string company_name = "ACME, LLC";
        const string dirty_department = "  Research   and   Development  ";

        // Act
        var professional = Professional.Create(company_name, department: dirty_department);

        // Assert
        professional.Department.Should().Be("Research and Development");
    }

    [Fact]
    public void Create_WithAllDirtyFields_CleansAll()
    {
        // Arrange
        const string dirty_company_name = "  ACME,   LLC  ";
        const string dirty_job_title = "  Software   Engineer  ";
        const string dirty_department = "  Research   and   Development  ";

        // Act
        var professional = Professional.Create(dirty_company_name, dirty_job_title, dirty_department);

        // Assert
        professional.CompanyName.Should().Be("ACME, LLC");
        professional.JobTitle.Should().Be("Software Engineer");
        professional.Department.Should().Be("Research and Development");
    }

    #endregion

    #region Whitespace-Only Fields Become Null

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithWhitespaceOnlyJobTitle_SetsToNull(string jobTitle)
    {
        // Arrange
        const string company_name = "ACME, LLC";

        // Act
        var professional = Professional.Create(company_name, jobTitle);

        // Assert
        professional.JobTitle.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithWhitespaceOnlyDepartment_SetsToNull(string department)
    {
        // Arrange
        const string company_name = "ACME, LLC";

        // Act
        var professional = Professional.Create(company_name, department: department);

        // Assert
        professional.Department.Should().BeNull();
    }

    #endregion

    #region Uri Validation

    [Fact]
    public void Create_WithValidUri_PreservesUri()
    {
        // Arrange
        const string company_name = "ACME, LLC";
        var website = new Uri("https://www.acme.com");

        // Act
        var professional = Professional.Create(company_name, companyWebsite: website);

        // Assert
        professional.CompanyWebsite.Should().Be(website);
        professional.CompanyWebsite!.AbsoluteUri.Should().Be("https://www.acme.com/");
    }

    [Fact]
    public void Create_WithNullUri_Success()
    {
        // Arrange
        const string company_name = "ACME, LLC";

        // Act
        var professional = Professional.Create(company_name, companyWebsite: null);

        // Assert
        professional.CompanyWebsite.Should().BeNull();
    }

    #endregion

    #region Create Overload Tests

    [Fact]
    public void Create_WithExistingProfessional_CreatesNewInstance()
    {
        // Arrange
        var original = Professional.Create(
            "ACME, LLC",
            "Software Engineer",
            "Research and Development",
            new Uri("https://www.acme.com"));

        // Act
        var copy = Professional.Create(original);

        // Assert
        copy.Should().NotBeNull();
        copy.CompanyName.Should().Be(original.CompanyName);
        copy.JobTitle.Should().Be(original.JobTitle);
        copy.Department.Should().Be(original.Department);
        copy.CompanyWebsite.Should().Be(original.CompanyWebsite);
        copy.Should().Be(original); // Value equality
    }

    [Fact]
    public void Create_WithInvalidExistingProfessional_ThrowsGeoValidationException()
    {
        // Arrange - Create invalid professional by bypassing factory
        var invalid = new Professional
        {
            CompanyName = "   " // Invalid - whitespace only
        };

        // Act
        var act = () => Professional.Create(invalid);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion

    #region Value Equality

    [Fact]
    public void Professional_WithSameValues_AreEqual()
    {
        // Arrange
        var professional1 = Professional.Create("ACME, LLC", "Software Engineer");
        var professional2 = Professional.Create("ACME, LLC", "Software Engineer");

        // Assert
        professional1.Should().Be(professional2);
        (professional1 == professional2).Should().BeTrue();
    }

    [Fact]
    public void Professional_WithDifferentCompanyName_AreNotEqual()
    {
        // Arrange
        var professional1 = Professional.Create("ACME, LLC");
        var professional2 = Professional.Create("TechCorp");

        // Assert
        professional1.Should().NotBe(professional2);
        (professional1 != professional2).Should().BeTrue();
    }

    [Fact]
    public void Professional_WithDifferentJobTitle_AreNotEqual()
    {
        // Arrange
        var professional1 = Professional.Create("ACME, LLC", "Software Engineer");
        var professional2 = Professional.Create("ACME, LLC", "Senior Software Engineer");

        // Assert
        professional1.Should().NotBe(professional2);
    }

    #endregion
}
