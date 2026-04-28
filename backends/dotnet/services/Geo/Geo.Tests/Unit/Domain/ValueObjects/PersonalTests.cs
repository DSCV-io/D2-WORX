// -----------------------------------------------------------------------
// <copyright file="PersonalTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Tests.Unit.Domain.ValueObjects;

using System.Collections.Immutable;
using D2.Geo.Domain.Enums;
using D2.Geo.Domain.Exceptions;
using D2.Geo.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

/// <summary>
/// Unit tests for <see cref="Personal"/>.
/// </summary>
public class PersonalTests
{
    #region Valid Creation

    /// <summary>
    /// Tests that creating Personal with only the required first name succeeds.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with all fields populated succeeds.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with some optional fields populated succeeds.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with invalid first name throws GeoValidationException.
    /// </summary>
    ///
    /// <param name="firstName">
    /// The invalid first name value.
    /// </param>
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

    /// <summary>
    /// Tests that creating Personal with clean first name preserves the value.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with clean preferred name preserves the value.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with dirty first name cleans whitespace.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with dirty preferred name cleans whitespace.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with dirty middle name cleans whitespace.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with dirty last name cleans whitespace.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with all dirty name fields cleans whitespace.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with whitespace-only preferred name sets it to null.
    /// </summary>
    ///
    /// <param name="preferredName">
    /// The whitespace-only preferred name value.
    /// </param>
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

    /// <summary>
    /// Tests that creating Personal with whitespace-only middle name sets it to null.
    /// </summary>
    ///
    /// <param name="middleName">
    /// The whitespace-only middle name value.
    /// </param>
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

    /// <summary>
    /// Tests that creating Personal with whitespace-only last name sets it to null.
    /// </summary>
    ///
    /// <param name="lastName">
    /// The whitespace-only last name value.
    /// </param>
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

    /// <summary>
    /// Tests that creating Personal with null professional credentials returns an empty list.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with empty professional credentials returns an empty list.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with valid professional credentials returns an immutable list.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with dirty professional credentials cleans whitespace.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with professional credentials containing whitespace-only
    /// entries removes those entries.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with valid title preserves the value.
    /// </summary>
    ///
    /// <param name="title">
    /// The valid title value.
    /// </param>
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

    /// <summary>
    /// Tests that creating Personal with valid generational suffix preserves the value.
    /// </summary>
    ///
    /// <param name="suffix">
    /// The valid generational suffix value.
    /// </param>
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

    /// <summary>
    /// Tests that creating Personal with valid biological sex preserves the value.
    /// </summary>
    ///
    /// <param name="sex">
    /// The valid biological sex value.
    /// </param>
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

    /// <summary>
    /// Tests that creating Personal with valid date of birth preserves the value.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal with null date of birth sets it to null.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal from an existing instance creates a new instance.
    /// </summary>
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

    /// <summary>
    /// Tests that creating Personal from an invalid existing instance throws
    /// GeoValidationException.
    /// </summary>
    [Fact]
    public void Create_WithInvalidExistingPersonal_ThrowsGeoValidationException()
    {
        // Arrange - Create invalid personal by bypassing factory
        var invalid = new Personal
        {
            FirstName = "   ", // Invalid - whitespace only
        };

        // Act
        var act = () => Personal.Create(invalid);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion

    #region Value Equality

    /// <summary>
    /// Tests that two Personal instances with the same values are equal.
    /// </summary>
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

    /// <summary>
    /// Tests that two Personal instances with different first names are not equal.
    /// </summary>
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

    /// <summary>
    /// Tests that two Personal instances with different last names are not equal.
    /// </summary>
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

    #region GetHashCode

    /// <summary>
    /// Tests that GetHashCode returns the same value for equal Personal instances.
    /// </summary>
    [Fact]
    public void GetHashCode_WithEqualInstances_ReturnsSameValue()
    {
        // Arrange
        var personal1 = Personal.Create("John", lastName: "Doe", professionalCredentials: ["PhD"]);
        var personal2 = Personal.Create("John", lastName: "Doe", professionalCredentials: ["PhD"]);

        // Act & Assert
        personal1.GetHashCode().Should().Be(personal2.GetHashCode());
    }

    /// <summary>
    /// Tests that GetHashCode returns different values for different Personal instances.
    /// </summary>
    [Fact]
    public void GetHashCode_WithDifferentValues_ReturnsDifferentValue()
    {
        // Arrange
        var personal1 = Personal.Create("John");
        var personal2 = Personal.Create("Jane");

        // Act & Assert
        personal1.GetHashCode().Should().NotBe(personal2.GetHashCode());
    }

    #endregion

    #region Equals Edge Cases

    /// <summary>
    /// Tests that Equals returns false when comparing with null.
    /// </summary>
    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        // Arrange
        var personal = Personal.Create("John");

        // Act & Assert
        personal.Equals(null).Should().BeFalse();
    }

    /// <summary>
    /// Tests that Equals returns false when ProfessionalCredentials differ.
    /// </summary>
    [Fact]
    public void Equals_WithDifferentCredentials_ReturnsFalse()
    {
        // Arrange
        var personal1 = Personal.Create("John", professionalCredentials: ["PhD"]);
        var personal2 = Personal.Create("John", professionalCredentials: ["MBA"]);

        // Act & Assert
        personal1.Equals(personal2).Should().BeFalse();
    }

    #endregion
}
