// -----------------------------------------------------------------------
// <copyright file="StreetAddressTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Tests.Unit.Domain.ValueObjects;

using D2.Geo.Domain.Exceptions;
using D2.Geo.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

/// <summary>
/// Unit tests for <see cref="StreetAddress"/>.
/// </summary>
public class StreetAddressTests
{
    #region Valid Creation

    /// <summary>
    /// Tests that creating StreetAddress with only Line1 succeeds.
    /// </summary>
    [Fact]
    public void Create_WithLine1Only_Success()
    {
        // Arrange
        const string line1 = "123 Main St";

        // Act
        var address = StreetAddress.Create(line1);

        // Assert
        address.Should().NotBeNull();
        address.Line1.Should().Be("123 Main St");
        address.Line2.Should().BeNull();
        address.Line3.Should().BeNull();
    }

    /// <summary>
    /// Tests that creating StreetAddress with Line1 and Line2 succeeds.
    /// </summary>
    [Fact]
    public void Create_WithLine1AndLine2_Success()
    {
        // Arrange
        const string line1 = "123 Main St";
        const string line2 = "Building B";

        // Act
        var address = StreetAddress.Create(line1, line2);

        // Assert
        address.Should().NotBeNull();
        address.Line1.Should().Be("123 Main St");
        address.Line2.Should().Be("Building B");
        address.Line3.Should().BeNull();
    }

    /// <summary>
    /// Tests that creating StreetAddress with all three lines succeeds.
    /// </summary>
    [Fact]
    public void Create_WithAllThreeLines_Success()
    {
        // Arrange
        const string line1 = "123 Main St";
        const string line2 = "Building B";
        const string line3 = "Suite 400";

        // Act
        var address = StreetAddress.Create(line1, line2, line3);

        // Assert
        address.Should().NotBeNull();
        address.Line1.Should().Be("123 Main St");
        address.Line2.Should().Be("Building B");
        address.Line3.Should().Be("Suite 400");
    }

    #endregion

    #region Clean Strings - No Changes

    /// <summary>
    /// Tests that creating StreetAddress with clean Line1 makes no changes.
    /// </summary>
    [Fact]
    public void Create_WithCleanLine1_NoChanges()
    {
        // Arrange
        const string clean_line1 = "123 Main St";

        // Act
        var address = StreetAddress.Create(clean_line1);

        // Assert
        address.Line1.Should().Be(clean_line1);
    }

    /// <summary>
    /// Tests that creating StreetAddress with clean Line2 makes no changes.
    /// </summary>
    [Fact]
    public void Create_WithAllCleanLines_NoChanges()
    {
        // Arrange
        const string line1 = "123 Main St";
        const string line2 = "Building B";
        const string line3 = "Suite 400";

        // Act
        var address = StreetAddress.Create(line1, line2, line3);

        // Assert
        address.Line1.Should().Be(line1);
        address.Line2.Should().Be(line2);
        address.Line3.Should().Be(line3);
    }

    #endregion

    #region Dirty Strings - Cleanup

    /// <summary>
    /// Tests that creating StreetAddress with dirty Line1 cleans up whitespace.
    /// </summary>
    [Fact]
    public void Create_WithDirtyLine1_CleansWhitespace()
    {
        // Arrange
        const string dirty_line1 = "  123   Main   St  ";

        // Act
        var address = StreetAddress.Create(dirty_line1);

        // Assert
        address.Line1.Should().Be("123 Main St");
    }

    /// <summary>
    /// Tests that creating StreetAddress with dirty Line2 cleans up whitespace.
    /// </summary>
    [Fact]
    public void Create_WithDirtyLine2_CleansWhitespace()
    {
        // Arrange
        const string line1 = "123 Main St";
        const string dirty_line2 = "  Building   B  ";

        // Act
        var address = StreetAddress.Create(line1, dirty_line2);

        // Assert
        address.Line2.Should().Be("Building B");
    }

    /// <summary>
    /// Tests that creating StreetAddress with dirty Line3 cleans up whitespace.
    /// </summary>
    [Fact]
    public void Create_WithDirtyLine3_CleansWhitespace()
    {
        // Arrange
        const string line1 = "123 Main St";
        const string line2 = "Building B";
        const string dirty_line3 = "  Suite   400  ";

        // Act
        var address = StreetAddress.Create(line1, line2, dirty_line3);

        // Assert
        address.Line3.Should().Be("Suite 400");
    }

    /// <summary>
    /// Tests that creating StreetAddress with tabs and newlines normalizes them to spaces.
    /// </summary>
    [Fact]
    public void Create_WithTabsAndNewlines_NormalizesToSpaces()
    {
        // Arrange
        const string dirty_line1 = "  123\tMain\n\nSt  ";

        // Act
        var address = StreetAddress.Create(dirty_line1);

        // Assert
        address.Line1.Should().Be("123 Main St");
    }

    /// <summary>
    /// Tests that creating StreetAddress with all dirty lines cleans them up.
    /// </summary>
    [Fact]
    public void Create_WithAllDirtyLines_CleansAll()
    {
        // Arrange
        const string dirty_line1 = "  123   Main   St  ";
        const string dirty_line2 = "  Building   B  ";
        const string dirty_line3 = "  Suite   400  ";

        // Act
        var address = StreetAddress.Create(dirty_line1, dirty_line2, dirty_line3);

        // Assert
        address.Line1.Should().Be("123 Main St");
        address.Line2.Should().Be("Building B");
        address.Line3.Should().Be("Suite 400");
    }

    #endregion

    #region Line1 Validation - Required

    /// <summary>
    /// Tests that creating StreetAddress with invalid Line1 throws GeoValidationException.
    /// </summary>
    ///
    /// <param name="line1">
    /// The Line1 value to test.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Create_WithInvalidLine1_ThrowsGeoValidationException(string? line1)
    {
        // Act
        var act = () => StreetAddress.Create(line1!);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion

    #region Line3 Requires Line2 - Business Rule

    /// <summary>
    /// Tests that creating StreetAddress with Line3 but null Line2 throws GeoValidationException.
    /// </summary>
    [Fact]
    public void Create_WithLine3ButNullLine2_ThrowsGeoValidationException()
    {
        // Arrange
        const string line1 = "123 Main St";
        const string line3 = "Suite 400";

        // Act
        var act = () => StreetAddress.Create(line1, null, line3);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    /// <summary>
    /// Tests that creating StreetAddress with Line3 but empty Line2 throws GeoValidationException.
    /// </summary>
    [Fact]
    public void Create_WithLine3ButEmptyLine2_ThrowsGeoValidationException()
    {
        // Arrange
        const string line1 = "123 Main St";
        const string line2 = "";
        const string line3 = "Suite 400";

        // Act
        var act = () => StreetAddress.Create(line1, line2, line3);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    /// <summary>
    /// Tests that creating StreetAddress with Line3 but whitespace Line2 throws
    /// GeoValidationException.
    /// </summary>
    [Fact]
    public void Create_WithLine3ButWhitespaceLine2_ThrowsGeoValidationException()
    {
        // Arrange
        const string line1 = "123 Main St";
        const string line2 = "   ";
        const string line3 = "Suite 400";

        // Act
        var act = () => StreetAddress.Create(line1, line2, line3);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion

    #region Whitespace-Only Lines Become Null

    /// <summary>
    /// Tests that creating StreetAddress with whitespace-only Line2 sets it to null.
    /// </summary>
    ///
    /// <param name="line2">
    /// The Line2 value to test.
    /// </param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithWhitespaceOnlyLine2_SetsToNull(string line2)
    {
        // Arrange
        const string line1 = "123 Main St";

        // Act
        var address = StreetAddress.Create(line1, line2);

        // Assert
        address.Line2.Should().BeNull();
    }

    /// <summary>
    /// Tests that creating StreetAddress with whitespace-only Line3 sets it to null.
    /// </summary>
    ///
    /// <param name="line3">
    /// The Line3 value to test.
    /// </param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Create_WithWhitespaceOnlyLine3_SetsToNull(string line3)
    {
        // Arrange
        const string line1 = "123 Main St";
        const string line2 = "Building B";

        // Act
        var address = StreetAddress.Create(line1, line2, line3);

        // Assert
        address.Line3.Should().BeNull();
    }

    #endregion

    #region Value Equality

    /// <summary>
    /// Tests that two StreetAddress instances with the same values are equal.
    /// </summary>
    [Fact]
    public void StreetAddress_WithSameValues_AreEqual()
    {
        // Arrange
        var address1 = StreetAddress.Create("123 Main St", "Building B", "Suite 400");
        var address2 = StreetAddress.Create("123 Main St", "Building B", "Suite 400");

        // Assert
        address1.Should().Be(address2);
        (address1 == address2).Should().BeTrue();
    }

    /// <summary>
    /// Tests that two StreetAddress instances with different values are not equal.
    /// </summary>
    [Fact]
    public void StreetAddress_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var address1 = StreetAddress.Create("123 Main St", "Building B");
        var address2 = StreetAddress.Create("123 Main St", "Building A");

        // Assert
        address1.Should().NotBe(address2);
        (address1 != address2).Should().BeTrue();
    }

    #endregion

    #region Create From Existing

    /// <summary>
    /// Tests that Create from existing StreetAddress produces an equal instance.
    /// </summary>
    [Fact]
    public void Create_FromExistingStreetAddress_Success()
    {
        // Arrange
        var original = StreetAddress.Create("123 Main St", "Building B", "Suite 400");

        // Act
        var copy = StreetAddress.Create(original);

        // Assert
        copy.Should().Be(original);
    }

    /// <summary>
    /// Tests that Create from invalid existing StreetAddress throws GeoValidationException.
    /// </summary>
    [Fact]
    public void Create_WithInvalidExistingStreetAddress_ThrowsGeoValidationException()
    {
        // Arrange - Create invalid address by bypassing factory
        var invalid = new StreetAddress
        {
            Line1 = "   ", // Invalid - whitespace only
        };

        // Act
        var act = () => StreetAddress.Create(invalid);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion
}
