// -----------------------------------------------------------------------
// <copyright file="EnumerableExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit;

using D2.Shared.Utilities.Extensions;
using FluentAssertions;

/// <summary>
/// Unit tests for <see cref="EnumerableExtensions"/>.
/// </summary>
public class EnumerableExtensionsTests
{
    #region Truthy Tests

    /// <summary>
    /// Tests that Truthy returns true when the enumerable contains multiple elements.
    /// </summary>
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

    /// <summary>
    /// Tests that Truthy returns true when the enumerable contains a single element.
    /// </summary>
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

    /// <summary>
    /// Tests that Truthy returns false when the enumerable is empty.
    /// </summary>
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

    /// <summary>
    /// Tests that Truthy returns false when the enumerable is null.
    /// </summary>
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

    /// <summary>
    /// Tests that Falsey returns true when the enumerable is empty.
    /// </summary>
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

    /// <summary>
    /// Tests that Falsey returns true when the enumerable is null.
    /// </summary>
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

    /// <summary>
    /// Tests that Falsey returns false when the enumerable contains elements.
    /// </summary>
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

    /// <summary>
    /// Tests that Clean applies the cleaner function to all elements and returns a cleaned enumerable.
    /// </summary>
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

    /// <summary>
    /// Tests that Clean removes null elements by default after cleaning.
    /// </summary>
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

    /// <summary>
    /// Tests that Clean returns null when the input enumerable is null with default behavior.
    /// </summary>
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

    /// <summary>
    /// Tests that Clean returns null when the input enumerable is empty with default behavior.
    /// </summary>
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

    /// <summary>
    /// Tests that Clean returns null when all elements are null after cleaning.
    /// </summary>
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

    /// <summary>
    /// Tests that Clean returns an empty enumerable when input is null and behavior is ReturnEmpty.
    /// </summary>
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

    /// <summary>
    /// Tests that Clean returns an empty enumerable when input is empty and behavior is ReturnEmpty.
    /// </summary>
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

    /// <summary>
    /// Tests that Clean returns an empty enumerable when all elements are null and behavior is ReturnEmpty.
    /// </summary>
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

    /// <summary>
    /// Tests that Clean throws an ArgumentException when input is null and behavior is Throw.
    /// </summary>
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

    /// <summary>
    /// Tests that Clean throws an ArgumentException when input is empty and behavior is Throw.
    /// </summary>
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

    /// <summary>
    /// Tests that Clean throws an ArgumentException when all elements are null and behavior is Throw.
    /// </summary>
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

    /// <summary>
    /// Tests that Clean throws a NullReferenceException when a null value is encountered and value null behavior is ThrowOnNull.
    /// </summary>
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
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Clean Tests - Complex Scenarios

    /// <summary>
    /// Tests that Clean correctly applies a custom transformation function to all elements.
    /// </summary>
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

    /// <summary>
    /// Tests that Clean can filter elements by returning null from the cleaner function.
    /// </summary>
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

    /// <summary>
    /// Tests that Clean correctly handles null values when cleaning reference types.
    /// </summary>
    [Fact]
    public void Clean_WithReferenceTypes_HandlesNullsCorrectly()
    {
        // Arrange
        string[] enumerable = ["  keep  ", string.Empty, "  also  ", "   "];
        string? Cleaner(string s)
        {
            var trimmed = s.Trim();
            return trimmed.Falsey() ? null : trimmed;
        }

        // Act
        var result = enumerable.Clean(Cleaner)?.ToList();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo("keep", "also");
    }

    #endregion
}
