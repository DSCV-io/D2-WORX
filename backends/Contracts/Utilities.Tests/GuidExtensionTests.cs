using D2.Contracts.Utilities;
using FluentAssertions;

namespace Utilities.Tests;

/// <summary>
/// Unit tests for <see cref="GuidExtensions"/>.
/// </summary>
public class GuidExtensionsTests
{
    #region Truthy Tests - Nullable Guid

    [Fact]
    public void Truthy_WithValidNullableGuid_ReturnsTrue()
    {
        // Arrange
        Guid? guid = Guid.NewGuid();

        // Act
        var result = guid.Truthy();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Truthy_WithNullGuid_ReturnsFalse()
    {
        // Arrange
        Guid? guid = null;

        // Act
        var result = guid.Truthy();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Truthy_WithEmptyNullableGuid_ReturnsFalse()
    {
        // Arrange
        Guid? guid = Guid.Empty;

        // Act
        var result = guid.Truthy();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Truthy Tests - Non-Nullable Guid

    [Fact]
    public void Truthy_WithValidGuid_ReturnsTrue()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var result = guid.Truthy();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Truthy_WithEmptyGuid_ReturnsFalse()
    {
        // Arrange
        var guid = Guid.Empty;

        // Act
        var result = guid.Truthy();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Truthy_WithDefaultGuid_ReturnsFalse()
    {
        // Arrange
        var guid = Guid.Empty;

        // Act
        var result = guid.Truthy();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Falsey Tests - Nullable Guid

    [Fact]
    public void Falsey_WithNullGuid_ReturnsTrue()
    {
        // Arrange
        Guid? guid = null;

        // Act
        var result = guid.Falsey();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Falsey_WithEmptyNullableGuid_ReturnsTrue()
    {
        // Arrange
        Guid? guid = Guid.Empty;

        // Act
        var result = guid.Falsey();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Falsey_WithValidNullableGuid_ReturnsFalse()
    {
        // Arrange
        Guid? guid = Guid.NewGuid();

        // Act
        var result = guid.Falsey();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Falsey Tests - Non-Nullable Guid

    [Fact]
    public void Falsey_WithEmptyGuid_ReturnsTrue()
    {
        // Arrange
        var guid = Guid.Empty;

        // Act
        var result = guid.Falsey();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Falsey_WithDefaultGuid_ReturnsTrue()
    {
        // Arrange
        var guid = Guid.Empty;

        // Act
        var result = guid.Falsey();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Falsey_WithValidGuid_ReturnsFalse()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var result = guid.Falsey();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Edge Cases and Consistency Tests

    [Fact]
    public void Truthy_And_Falsey_AreOpposites_ForNullableGuid()
    {
        // Arrange
        Guid? guid = Guid.NewGuid();

        // Act
        var truthy = guid.Truthy();
        var falsey = guid.Falsey();

        // Assert
        truthy.Should().NotBe(falsey);
    }

    [Fact]
    public void Truthy_And_Falsey_AreOpposites_ForNonNullableGuid()
    {
        // Arrange
        var guid = Guid.NewGuid();

        // Act
        var truthy = guid.Truthy();
        var falsey = guid.Falsey();

        // Assert
        truthy.Should().NotBe(falsey);
    }

    [Fact]
    public void Truthy_And_Falsey_AreOpposites_ForEmptyGuid()
    {
        // Arrange
        var guid = Guid.Empty;

        // Act
        var truthy = guid.Truthy();
        var falsey = guid.Falsey();

        // Assert
        truthy.Should().BeFalse();
        falsey.Should().BeTrue();
        truthy.Should().NotBe(falsey);
    }

    [Fact]
    public void Truthy_And_Falsey_AreOpposites_ForNullGuid()
    {
        // Arrange
        Guid? guid = null;

        // Act
        var truthy = guid.Truthy();
        var falsey = guid.Falsey();

        // Assert
        truthy.Should().BeFalse();
        falsey.Should().BeTrue();
        truthy.Should().NotBe(falsey);
    }

    [Fact]
    public void EmptyGuid_EqualsDefaultGuid()
    {
        // Arrange & Act
        var empty = Guid.Empty;
        var defaultGuid = Guid.Empty;

        // Assert
        empty.Should().Be(defaultGuid);
        empty.Truthy().Should().Be(defaultGuid.Truthy());
        empty.Falsey().Should().Be(defaultGuid.Falsey());
    }

    [Fact]
    public void SpecificGuid_ConsistentBehavior()
    {
        // Arrange
        var specificGuid = new Guid("12345678-1234-1234-1234-123456789012");
        Guid? nullableSpecific = specificGuid;

        // Act & Assert
        specificGuid.Truthy().Should().BeTrue();
        specificGuid.Falsey().Should().BeFalse();
        nullableSpecific.Truthy().Should().BeTrue();
        nullableSpecific.Falsey().Should().BeFalse();
    }

    [Fact]
    public void AllZerosGuid_IsSameAsEmpty()
    {
        // Arrange
        var allZeros = new Guid("00000000-0000-0000-0000-000000000000");

        // Act & Assert
        allZeros.Should().Be(Guid.Empty);
        allZeros.Truthy().Should().BeFalse();
        allZeros.Falsey().Should().BeTrue();
    }

    #endregion
}
