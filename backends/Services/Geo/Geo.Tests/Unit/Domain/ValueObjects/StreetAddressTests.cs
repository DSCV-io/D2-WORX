using FluentAssertions;
using Geo.Domain.Exceptions;
using Geo.Domain.ValueObjects;
using Xunit;

namespace Geo.Tests.Unit.Domain.ValueObjects;

/// <summary>
/// Unit tests for <see cref="Geo.Domain.ValueObjects.StreetAddress"/>.
/// </summary>
public class StreetAddressTests
{
     #region Valid Creation

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
}
