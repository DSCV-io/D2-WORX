// -----------------------------------------------------------------------
// <copyright file="CleanDisplayStrTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit;

using D2.Shared.Utilities.Extensions;
using FluentAssertions;

/// <summary>
/// Unit tests for <see cref="StringExtensions.CleanDisplayStr"/>.
/// </summary>
public class CleanDisplayStrTests
{
    #region Valid Names Pass Through

    /// <summary>
    /// Tests that CleanDisplayStr passes through valid display names unchanged.
    /// </summary>
    /// <param name="input">The input display name.</param>
    /// <param name="expected">The expected output.</param>
    [Theory]
    [InlineData("John", "John")]
    [InlineData("O'Brien", "O'Brien")]
    [InlineData("Mary-Jane", "Mary-Jane")]
    [InlineData("Dr. Smith", "Dr. Smith")]
    [InlineData("Jos\u00e9 Mar\u00eda", "Jos\u00e9 Mar\u00eda")]
    [InlineData("\u7530\u4E2D\u592A\u90CE", "\u7530\u4E2D\u592A\u90CE")]
    [InlineData("M\u00fcller, Hans", "M\u00fcller, Hans")]
    public void CleanDisplayStr_WithValidName_PassesThrough(string input, string expected)
    {
        var result = input.CleanDisplayStr();
        result.Should().Be(expected);
    }

    #endregion

    #region Strips Dangerous Characters

    /// <summary>
    /// Tests that CleanDisplayStr strips dangerous/unreasonable characters.
    /// </summary>
    /// <param name="input">The input containing dangerous characters.</param>
    /// <param name="expected">The expected sanitized output.</param>
    [Theory]
    [InlineData("<script>alert(1)</script>", "scriptalert1script")]
    [InlineData("**bold** text", "bold text")]
    [InlineData("[link](http://evil.com)", "linkhttpevil.com")]
    [InlineData("test`backtick`", "testbacktick")]
    [InlineData("$100 dollars", "100 dollars")]
    [InlineData("he said \"hello\"", "he said hello")]
    public void CleanDisplayStr_WithDangerousCharacters_StripsThemOut(string input, string expected)
    {
        var result = input.CleanDisplayStr();
        result.Should().Be(expected);
    }

    #endregion

    #region Whitespace Handling

    /// <summary>
    /// Tests that CleanDisplayStr trims and collapses whitespace.
    /// </summary>
    [Fact]
    public void CleanDisplayStr_WithExcessWhitespace_TrimsAndCollapses()
    {
        var result = "  hello  world  ".CleanDisplayStr();
        result.Should().Be("hello world");
    }

    /// <summary>
    /// Tests that CleanDisplayStr returns null for whitespace-only input.
    /// </summary>
    [Fact]
    public void CleanDisplayStr_WithWhitespaceOnly_ReturnsNull()
    {
        var result = "   ".CleanDisplayStr();
        result.Should().BeNull();
    }

    /// <summary>
    /// Tests that CleanDisplayStr returns null for empty string.
    /// </summary>
    [Fact]
    public void CleanDisplayStr_WithEmptyString_ReturnsNull()
    {
        var result = string.Empty.CleanDisplayStr();
        result.Should().BeNull();
    }

    #endregion

    #region Null Input

    /// <summary>
    /// Tests that CleanDisplayStr returns null for null input.
    /// </summary>
    [Fact]
    public void CleanDisplayStr_WithNull_ReturnsNull()
    {
        string? input = null;
        var result = input.CleanDisplayStr();
        result.Should().BeNull();
    }

    #endregion
}
