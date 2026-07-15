// -----------------------------------------------------------------------
// <copyright file="GuidExtensionsTryParseTruthyNullTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Extensions;

using System;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Utilities.Extensions;
using Xunit;

public sealed class GuidExtensionsTryParseTruthyNullTests
{
    // ----------------------------------------------------------------------
    // null / empty / whitespace inputs → false + null result
    // ----------------------------------------------------------------------

    [Fact]
    public void TryParseTruthyNull_OnNull_ReturnsFalseAndNull()
    {
        string? input = null;

        var ok = input.TryParseTruthyNull(out var result);

        ok.Should().BeFalse();
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    [InlineData("   \t  \n  ")]
    public void TryParseTruthyNull_OnEmptyOrWhitespace_ReturnsFalseAndNull(string input)
    {
        var ok = input.TryParseTruthyNull(out var result);

        ok.Should().BeFalse();
        result.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // valid GUID literals (multiple formats Guid.TryParse accepts)
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("a1b2c3d4-e5f6-4789-9abc-def012345678")] // D format (canonical)
    [InlineData("A1B2C3D4-E5F6-4789-9ABC-DEF012345678")] // uppercase
    [InlineData("a1b2c3d4e5f647899abcdef012345678")] // N format (no dashes)
    [InlineData("{a1b2c3d4-e5f6-4789-9abc-def012345678}")] // B format (braces)
    [InlineData("(a1b2c3d4-e5f6-4789-9abc-def012345678)")] // P format (parens)
    public void TryParseTruthyNull_OnValidGuidFormats_ReturnsTrueAndParsedGuid(string input)
    {
        var expected = new Guid("a1b2c3d4-e5f6-4789-9abc-def012345678");

        var ok = input.TryParseTruthyNull(out var result);

        ok.Should().BeTrue();
        result.Should().Be(expected);
    }

    [Fact]
    public void TryParseTruthyNull_OnMixedCase_ReturnsTrue()
    {
        // Adversarial: Guid.TryParse is case-insensitive; the wrapper must reflect that.
        const string mixed_case = "aBcDeFaB-cdEf-4123-8456-789012345678";
        var expected = new Guid("abcdefab-cdef-4123-8456-789012345678");

        var ok = mixed_case.TryParseTruthyNull(out var result);

        ok.Should().BeTrue();
        result.Should().Be(expected);
    }

    [Fact]
    public void TryParseTruthyNull_OnGuidEmptyLiteral_ReturnsFalseAndNull()
    {
        // Adversarial: the Truthy() filter rejects Guid.Empty even though
        // Guid.TryParse parses it successfully.
        const string empty_literal = "00000000-0000-0000-0000-000000000000";

        var ok = empty_literal.TryParseTruthyNull(out var result);

        ok.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryParseTruthyNull_OnGuidEmptyLiteralBracedOrNFormat_AlsoFalse()
    {
        // Adversarial: Empty in N / B formats must also be filtered.
        const string n_format_empty = "00000000000000000000000000000000";
        const string b_format_empty = "{00000000-0000-0000-0000-000000000000}";

        n_format_empty.TryParseTruthyNull(out var nResult).Should().BeFalse();
        nResult.Should().BeNull();

        b_format_empty.TryParseTruthyNull(out var bResult).Should().BeFalse();
        bResult.Should().BeNull();
    }

    [Fact]
    public void TryParseTruthyNull_OnGuidWithLeadingTrailingWhitespace_DocumentsBehavior()
    {
        // Adversarial: Guid.TryParse trims surrounding whitespace by default
        // (since .NET 5). Document the wrapper's pass-through behavior so a
        // future Guid.TryParse strictness change surfaces here.
        const string padded = "  a1b2c3d4-e5f6-4789-9abc-def012345678  ";
        var expected = new Guid("a1b2c3d4-e5f6-4789-9abc-def012345678");

        var ok = padded.TryParseTruthyNull(out var result);

        ok.Should().BeTrue();
        result.Should().Be(expected);
    }

    // ----------------------------------------------------------------------
    // malformed inputs → false + null result, no exception
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("a1b2c3d4-e5f6-4789-9abc")] // too short
    [InlineData("a1b2c3d4-e5f6-4789-9abc-def012345678-extra")] // too long
    [InlineData("g1b2c3d4-e5f6-4789-9abc-def012345678")] // 'g' is not hex
    [InlineData("a1b2c3d4e5f64789-9abc-def012345678")] // wrong dash positions
    [InlineData("a1b2c3d4-e5f6-4789-9abc-def01234567z")] // 'z' is not hex
    [InlineData("12345")]
    [InlineData("'; DROP TABLE users; --")]
    public void TryParseTruthyNull_OnMalformed_ReturnsFalseAndNull(string input)
    {
        var ok = input.TryParseTruthyNull(out var result);

        ok.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryParseTruthyNull_OnThousandCharacterRandomString_ReturnsFalseNoThrow()
    {
        // Adversarial: malicious / oversized input must not throw, must not allocate
        // unboundedly, must not catch fire. Random.Shared is thread-safe; the
        // input shape (1000 alphabetic chars) is the assertion target, not the
        // exact byte sequence.
        const int huge_length = 1000;
        var input = new string(Enumerable.Range(0, huge_length)
            .Select(_ => (char)('a' + Random.Shared.Next(26)))
            .ToArray());

        Action act = () => input.TryParseTruthyNull(out _);

        act.Should().NotThrow();
        input.TryParseTruthyNull(out var result).Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryParseTruthyNull_OnGuidWithUnicodeChars_ReturnsFalse()
    {
        // Adversarial: a GUID-shaped string with non-ASCII chars in the hex
        // positions must be rejected cleanly.
        const string unicode_garbage = "a1b2c3d4-e5f6-4789-9abc-def01234567é";

        var ok = unicode_garbage.TryParseTruthyNull(out var result);

        ok.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryParseTruthyNull_OnFullEmojiInput_ReturnsFalseNoThrow()
    {
        // Adversarial: surrogate pairs / emoji inputs must not crash.
        const string emoji = "😀😁😂";

        Action act = () => emoji.TryParseTruthyNull(out _);

        act.Should().NotThrow();
        emoji.TryParseTruthyNull(out var result).Should().BeFalse();
        result.Should().BeNull();
    }
}
