using System.Collections.Immutable;
using FluentAssertions;
using Geo.Domain.Enums;
using Geo.Domain.Exceptions;
using Geo.Domain.ValueObjects;
using Xunit;

namespace Geo.Tests.Unit.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="Geo.Domain.ValueObjects.Personal"/>.
/// </summary>
public class PersonalTests
{
    #region Valid Creation

    [Fact]
    public void Create_WithFirstNameOnly_Success()
    {
        // Arrange
        const string first_name = "John";

        // Act
        var personal = Personal.Create(first_name);

        // Assert
        personal.Should().NotBeNull();
        personal.FirstName.Should().Be("John");
        personal.Title.Should().BeNull();
        personal.PreferredName.Should().BeNull();
        personal.MiddleName.Should().BeNull();
        personal.LastName.Should().BeNull();
        personal.GenerationalSuffix.Should().BeNull();
        personal.ProfessionalCredentials.Should().BeEmpty();
        personal.DateOfBirth.Should().BeNull();
        personal.BiologicalSex.Should().BeNull();
    }

    [Fact]
    public void Create_WithAllFields_Success()
    {
        // Arrange
        const string first_name = "John";
        const NameTitle title = NameTitle.Dr;
        const string preferred_name = "Johnny";
        const string middle_name = "Michael";
        const string last_name = "Doe";
        const GenerationalSuffix suffix = GenerationalSuffix.Jr;
        string[] credentials = ["PhD", "CISSP"];
        var dateOfBirth = new DateOnly(1990, 5, 15);
        const BiologicalSex sex = BiologicalSex.Male;

        // Act
        var personal = Personal.Create(
            first_name,
            title,
            preferred_name,
            middle_name,
            last_name,
            suffix,
            credentials,
            dateOfBirth,
            sex);

        // Assert
        personal.Should().NotBeNull();
        personal.FirstName.Should().Be("John");
        personal.Title.Should().Be(NameTitle.Dr);
        personal.PreferredName.Should().Be("Johnny");
        personal.MiddleName.Should().Be("Michael");
        personal.LastName.Should().Be("Doe");
        personal.GenerationalSuffix.Should().Be(GenerationalSuffix.Jr);
        personal.ProfessionalCredentials.Should().BeEquivalentTo("PhD", "CISSP");
        personal.DateOfBirth.Should().Be(new DateOnly(1990, 5, 15));
        personal.BiologicalSex.Should().Be(BiologicalSex.Male);
    }

    [Fact]
    public void Create_WithPartialFields_Success()
    {
        // Arrange
        const string first_name = "John";
        const string last_name = "Doe";

        // Act
        var personal = Personal.Create(first_name, lastName: last_name);

        // Assert
        personal.FirstName.Should().Be("John");
        personal.LastName.Should().Be("Doe");
        personal.MiddleName.Should().BeNull();
        personal.PreferredName.Should().BeNull();
    }

    #endregion

    #region FirstName Validation - Required

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Create_WithInvalidFirstName_ThrowsGeoValidationException(string? firstName)
    {
        // Act
        var act = () => Personal.Create(firstName!);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion

    #region Clean Strings - No Changes

    [Fact]
    public void Create_WithCleanFirstName_NoChanges()
    {
        // Arrange
        const string clean_first_name = "John";

        // Act
        var personal = Personal.Create(clean_first_name);

        // Assert
        personal.FirstName.Should().Be(clean_first_name);
    }

    [Fact]
    public void Create_WithAllCleanNameFields_NoChanges()
    {
        // Arrange
        const string first_name = "John";
        const string preferred_name = "Johnny";
        const string middle_name = "Michael";
        const string last_name = "Doe";

        // Act
        var personal = Personal.Create(
            first_name,
            preferredName: preferred_name,
            middleName: middle_name,
            lastName: last_name);

        // Assert
        personal.FirstName.Should().Be(first_name);
        personal.PreferredName.Should().Be(preferred_name);
        personal.MiddleName.Should().Be(middle_name);
        personal.LastName.Should().Be(last_name);
    }

    #endregion

    #region Dirty Strings - Cleanup

    [Fact]
    public void Create_WithDirtyFirstName_CleansWhitespace()
    {
        // Arrange
        const string dirty_first_name = "  John  ";

        // Act
        var personal = Personal.Create(dirty_first_name);

        // Assert
        personal.FirstName.Should().Be("John");
    }

    [Fact]
    public void Create_WithDirtyPreferredName_CleansWhitespace()
    {
        // Arrange
        const string first_name = "John";
        const string dirty_preferred_name = "  Johnny  ";

        // Act
        var personal = Personal.Create(first_name, preferredName: dirty_preferred_name);

        // Assert
        personal.PreferredName.Should().Be("Johnny");
    }

    [Fact]
    public void Create_WithDirtyMiddleName_CleansWhitespace()
    {
        // Arrange
        const string first_name = "John";
        const string dirty_middle_name = "  Michael  ";

        // Act
        var personal = Personal.Create(first_name, middleName: dirty_middle_name);

        // Assert
        personal.MiddleName.Should().Be("Michael");
    }

    [Fact]
    public void Create_WithDirtyLastName_CleansWhitespace()
    {
        // Arrange
        const string first_name = "John";
        const string dirty_last_name = "  Doe  ";

        // Act
        var personal = Personal.Create(first_name, lastName: dirty_last_name);

        // Assert
        personal.LastName.Should().Be("Doe");
    }

    [Fact]
    public void Create_WithAllDirtyNameFields_CleansAll()
    {
        // Arrange
        const string dirty_first_name = "  John  ";
        const string dirty_preferred_name = "  Johnny  ";
        const string dirty_middle_name = "  Michael  ";
        const string dirty_last_name = "  Doe  ";

        // Act
        var personal = Personal.Create(
            dirty_first_name,
            preferredName: dirty_preferred_name,
            middleName: dirty_middle_name,
            lastName: dirty_last_name);

        // Assert
        personal.FirstName.Should().Be("John");
        personal.PreferredName.Should().Be("Johnny");
        personal.MiddleName.Should().Be("Michael");
        personal.LastName.Should().Be("Doe");
    }

    #endregion

    #region Whitespace-Only Fields Become Null

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithWhitespaceOnlyPreferredName_SetsToNull(string preferredName)
    {
        // Arrange
        const string first_name = "John";

        // Act
        var personal = Personal.Create(first_name, preferredName: preferredName);

        // Assert
        personal.PreferredName.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithWhitespaceOnlyMiddleName_SetsToNull(string middleName)
    {
        // Arrange
        const string first_name = "John";

        // Act
        var personal = Personal.Create(first_name, middleName: middleName);

        // Assert
        personal.MiddleName.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithWhitespaceOnlyLastName_SetsToNull(string lastName)
    {
        // Arrange
        const string first_name = "John";

        // Act
        var personal = Personal.Create(first_name, lastName: lastName);

        // Assert
        personal.LastName.Should().BeNull();
    }

    #endregion

    #region Professional Credentials

    [Fact]
    public void Create_WithNullCredentials_ReturnsEmptyList()
    {
        // Arrange
        const string first_name = "John";

        // Act
        var personal = Personal.Create(first_name, professionalCredentials: null);

        // Assert
        personal.ProfessionalCredentials.Should().NotBeNull();
        personal.ProfessionalCredentials.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEmptyCredentials_ReturnsEmptyList()
    {
        // Arrange
        const string first_name = "John";
        var credentials = Array.Empty<string>();

        // Act
        var personal = Personal.Create(first_name, professionalCredentials: credentials);

        // Assert
        personal.ProfessionalCredentials.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithValidCredentials_ReturnsImmutableList()
    {
        // Arrange
        const string first_name = "John";
        string[] credentials = ["PhD", "CISSP", "MBA"];

        // Act
        var personal = Personal.Create(first_name, professionalCredentials: credentials);

        // Assert
        personal.ProfessionalCredentials.Should().BeOfType<ImmutableList<string>>();
        personal.ProfessionalCredentials.Should().BeEquivalentTo("PhD", "CISSP", "MBA");
    }

    [Fact]
    public void Create_WithDirtyCredentials_CleansCredentials()
    {
        // Arrange
        const string first_name = "John";
        string[] dirtyCredentials = ["  PhD  ", "  CISSP  ", "  MBA  "];

        // Act
        var personal = Personal.Create(first_name, professionalCredentials: dirtyCredentials);

        // Assert
        personal.ProfessionalCredentials.Should().BeEquivalentTo("PhD", "CISSP", "MBA");
    }

    [Fact]
    public void Create_WithCredentialsContainingWhitespace_RemovesWhitespaceEntries()
    {
        // Arrange
        const string first_name = "John";
        string[] credentials = ["PhD", "   ", "CISSP"];

        // Act
        var personal = Personal.Create(first_name, professionalCredentials: credentials);

        // Assert
        personal.ProfessionalCredentials.Should().BeEquivalentTo("PhD", "CISSP");
    }

    #endregion

    #region Enums

    [Theory]
    [InlineData(NameTitle.Mr)]
    [InlineData(NameTitle.Ms)]
    [InlineData(NameTitle.Dr)]
    [InlineData(NameTitle.Prof)]
    public void Create_WithValidTitle_PreservesTitle(NameTitle title)
    {
        // Arrange
        const string first_name = "John";

        // Act
        var personal = Personal.Create(first_name, title);

        // Assert
        personal.Title.Should().Be(title);
    }

    [Theory]
    [InlineData(GenerationalSuffix.Jr)]
    [InlineData(GenerationalSuffix.Sr)]
    [InlineData(GenerationalSuffix.III)]
    public void Create_WithValidGenerationalSuffix_PreservesSuffix(GenerationalSuffix suffix)
    {
        // Arrange
        const string first_name = "John";

        // Act
        var personal = Personal.Create(first_name, generationalSuffix: suffix);

        // Assert
        personal.GenerationalSuffix.Should().Be(suffix);
    }

    [Theory]
    [InlineData(BiologicalSex.Male)]
    [InlineData(BiologicalSex.Female)]
    [InlineData(BiologicalSex.Intersex)]
    [InlineData(BiologicalSex.Unknown)]
    public void Create_WithValidBiologicalSex_PreservesSex(BiologicalSex sex)
    {
        // Arrange
        const string first_name = "John";

        // Act
        var personal = Personal.Create(first_name, biologicalSex: sex);

        // Assert
        personal.BiologicalSex.Should().Be(sex);
    }

    #endregion

    #region DateOnly

    [Fact]
    public void Create_WithValidDateOfBirth_PreservesDate()
    {
        // Arrange
        const string first_name = "John";
        var dateOfBirth = new DateOnly(1990, 5, 15);

        // Act
        var personal = Personal.Create(first_name, dateOfBirth: dateOfBirth);

        // Assert
        personal.DateOfBirth.Should().Be(new DateOnly(1990, 5, 15));
    }

    [Fact]
    public void Create_WithNullDateOfBirth_Success()
    {
        // Arrange
        const string first_name = "John";

        // Act
        var personal = Personal.Create(first_name, dateOfBirth: null);

        // Assert
        personal.DateOfBirth.Should().BeNull();
    }

    #endregion

    #region Create Overload Tests

    [Fact]
    public void Create_WithExistingPersonal_CreatesNewInstance()
    {
        // Arrange
        var original = Personal.Create(
            "John",
            NameTitle.Dr,
            "Johnny",
            "Michael",
            "Doe",
            GenerationalSuffix.Jr,
            ["PhD", "CISSP"],
            new DateOnly(1990, 5, 15),
            BiologicalSex.Male);

        // Act
        var copy = Personal.Create(original);

        // Assert
        copy.Should().NotBeNull();
        copy.FirstName.Should().Be(original.FirstName);
        copy.Title.Should().Be(original.Title);
        copy.PreferredName.Should().Be(original.PreferredName);
        copy.MiddleName.Should().Be(original.MiddleName);
        copy.LastName.Should().Be(original.LastName);
        copy.GenerationalSuffix.Should().Be(original.GenerationalSuffix);
        copy.ProfessionalCredentials.Should().BeEquivalentTo(original.ProfessionalCredentials);
        copy.DateOfBirth.Should().Be(original.DateOfBirth);
        copy.BiologicalSex.Should().Be(original.BiologicalSex);
        copy.Should().Be(original); // Value equality
    }

    [Fact]
    public void Create_WithInvalidExistingPersonal_ThrowsGeoValidationException()
    {
        // Arrange - Create invalid personal by bypassing factory
        var invalid = new Personal
        {
            FirstName = "   " // Invalid - whitespace only
        };

        // Act
        var act = () => Personal.Create(invalid);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion

    #region Value Equality

    [Fact]
    public void Personal_WithSameValues_AreEqual()
    {
        // Arrange
        var personal1 = Personal.Create("John", lastName: "Doe");
        var personal2 = Personal.Create("John", lastName: "Doe");

        // Assert
        personal1.Should().Be(personal2);
        (personal1 == personal2).Should().BeTrue();
    }

    [Fact]
    public void Personal_WithDifferentFirstName_AreNotEqual()
    {
        // Arrange
        var personal1 = Personal.Create("John");
        var personal2 = Personal.Create("Jane");

        // Assert
        personal1.Should().NotBe(personal2);
        (personal1 != personal2).Should().BeTrue();
    }

    [Fact]
    public void Personal_WithDifferentLastName_AreNotEqual()
    {
        // Arrange
        var personal1 = Personal.Create("John", lastName: "Doe");
        var personal2 = Personal.Create("John", lastName: "Smith");

        // Assert
        personal1.Should().NotBe(personal2);
    }

    #endregion
}
