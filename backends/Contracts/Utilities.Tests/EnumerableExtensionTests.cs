using D2.Contracts.Utilities;
using FluentAssertions;
// ReSharper disable MoveLocalFunctionAfterJumpStatement

namespace Utilities.Tests;

/// <summary>
/// Unit tests for <see cref="EnumerableExtensions"/>.
/// </summary>
public class EnumerableExtensionsTests
{
    #region Truthy Tests

    [Fact]
    public void Truthy_WithNonEmptyEnumerable_ReturnsTrue()
    {
        // Arrange
        int[] enumerable = [1, 2, 3];

        // Act
        var result = enumerable.Truthy();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Truthy_WithSingleElement_ReturnsTrue()
    {
        // Arrange
        int[] enumerable = [1];

        // Act
        var result = enumerable.Truthy();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Truthy_WithEmptyEnumerable_ReturnsFalse()
    {
        // Arrange
        int[] enumerable = [];

        // Act
        var result = enumerable.Truthy();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Truthy_WithNull_ReturnsFalse()
    {
        // Arrange
        int[]? enumerable = null;

        // Act
        var result = enumerable.Truthy();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Falsey Tests

    [Fact]
    public void Falsey_WithEmptyEnumerable_ReturnsTrue()
    {
        // Arrange
        int[] enumerable = [];

        // Act
        var result = enumerable.Falsey();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Falsey_WithNull_ReturnsTrue()
    {
        // Arrange
        int[]? enumerable = null;

        // Act
        var result = enumerable.Falsey();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Falsey_WithNonEmptyEnumerable_ReturnsFalse()
    {
        // Arrange
        int[] enumerable = [1, 2, 3];

        // Act
        var result = enumerable.Falsey();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Clean Tests - ReturnNull Behavior

    [Fact]
    public void Clean_WithValidElements_ReturnsCleanedEnumerable()
    {
        // Arrange
        string[] enumerable = ["  hello  ", "WORLD", "  test  "];
        string Cleaner(string s) => s.Trim().ToLowerInvariant();

        // Act
        var result = enumerable.Clean(Cleaner)?.ToList();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo("hello", "world", "test");
    }

    [Fact]
    public void Clean_WithNullElements_RemovesNullsByDefault()
    {
        // Arrange
        string?[] enumerable = ["hello", null, "world"];
        string? Cleaner(string? s) => s?.Trim();

        // Act
        var result = enumerable.Clean(Cleaner)?.ToList();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo("hello", "world");
    }

    [Fact]
    public void Clean_WithNullInput_ReturnsNullByDefault()
    {
        // Arrange
        string[]? enumerable = null;
        string Cleaner(string s) => s.Trim();

        // Act
        var result = enumerable.Clean(Cleaner);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Clean_WithEmptyInput_ReturnsNullByDefault()
    {
        // Arrange
        string[] enumerable = [];
        string Cleaner(string s) => s.Trim();

        // Act
        var result = enumerable.Clean(Cleaner);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Clean_WithAllNullElements_ReturnsNull()
    {
        // Arrange
        string?[] enumerable = [null, null, null];
        string? Cleaner(string? s) => s?.Trim();

        // Act
        var result = enumerable.Clean(Cleaner);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Clean Tests - ReturnEmpty Behavior

    [Fact]
    public void Clean_WithNullInput_ReturnsEmpty_WhenBehaviorIsReturnEmpty()
    {
        // Arrange
        string[]? enumerable = null;
        string Cleaner(string s) => s.Trim();

        // Act
        var result = enumerable.Clean(
                Cleaner,
                EnumerableExtensions.CleanEnumEmptyBehavior.ReturnEmpty)?
            .ToList();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Clean_WithEmptyInput_ReturnsEmpty_WhenBehaviorIsReturnEmpty()
    {
        // Arrange
        string[] enumerable = [];
        string Cleaner(string s) => s.Trim();

        // Act
        var result = enumerable.Clean(
                Cleaner,
                EnumerableExtensions.CleanEnumEmptyBehavior.ReturnEmpty)?
            .ToList();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Clean_WithAllNullElements_ReturnsEmpty_WhenBehaviorIsReturnEmpty()
    {
        // Arrange
        string?[] enumerable = [null, null, null];
        string? Cleaner(string? s) => s?.Trim();

        // Act
        var result = enumerable.Clean(
                Cleaner,
                EnumerableExtensions.CleanEnumEmptyBehavior.ReturnEmpty)?
            .ToList();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion

    #region Clean Tests - Throw Behavior

    [Fact]
    public void Clean_WithNullInput_Throws_WhenBehaviorIsThrow()
    {
        // Arrange
        string[]? enumerable = null;
        string Cleaner(string s) => s.Trim();

        // Act
        var act = () => enumerable.Clean(
            Cleaner,
            EnumerableExtensions.CleanEnumEmptyBehavior.Throw);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Clean_WithEmptyInput_Throws_WhenBehaviorIsThrow()
    {
        // Arrange
        string[] enumerable = [];
        var cleaner = (string s) => s.Trim();

        // Act
        var act = () => enumerable.Clean(
            cleaner,
            EnumerableExtensions.CleanEnumEmptyBehavior.Throw);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Clean_WithAllNullElements_Throws_WhenBehaviorIsThrow()
    {
        // Arrange
        string?[] enumerable = [null, null, null];
        var cleaner = (string? s) => s?.Trim();

        // Act
        var act = () => enumerable.Clean(
            cleaner,
            EnumerableExtensions.CleanEnumEmptyBehavior.Throw);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Clean_WithNullValue_Throws_WhenValueNullBehaviorIsThrowOnNull()
    {
        // Arrange
        string?[] enumerable = ["hello", null, "world"];
        var cleaner = (string? s) => s?.Trim();

        // Act
        var act = () => enumerable.Clean(
            cleaner,
            EnumerableExtensions.CleanEnumEmptyBehavior.ReturnNull,
            EnumerableExtensions.CleanValueNullBehavior.ThrowOnNull);

        // Assert
        act.Should().Throw<NullReferenceException>();
    }

    #endregion

    #region Clean Tests - Complex Scenarios

    [Fact]
    public void Clean_WithCustomCleaner_AppliesCleanerCorrectly()
    {
        // Arrange
        int[] enumerable = [1, 2, 3, 4, 5];
        int Cleaner(int x) => x * 2;

        // Act
        var result = enumerable.Clean(Cleaner)?.ToList();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo([2, 4, 6, 8, 10]);
    }

    [Fact]
    public void Clean_WithFilteringCleaner_FiltersElements()
    {
        // Arrange
        int?[] integers = [1, 2, 3, 4, 5];
        int? Cleaner(int? x) => x % 2 == 0 ? x : null;

        // Act
        var result = integers.Clean(Cleaner)?.ToList();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new int?[] { 2, 4 });
    }

    [Fact]
    public void Clean_WithReferenceTypes_HandlesNullsCorrectly()
    {
        // Arrange
        string[] enumerable = ["  keep  ", "", "  also  ", "   "];
        string? Cleaner(string s)
        {
            var trimmed = s.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        // Act
        var result = enumerable.Clean(Cleaner)?.ToList();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo("keep", "also");
    }

    #endregion
}
