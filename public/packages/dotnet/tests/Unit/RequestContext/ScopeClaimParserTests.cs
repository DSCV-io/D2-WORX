// -----------------------------------------------------------------------
// <copyright file="ScopeClaimParserTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.RequestContext;

using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Context.Abstractions;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="ScopeClaimParser"/>.
/// Per RFC 6749 §3.3 the separator is ASCII SP only — accepting tab / CR /
/// LF / unicode whitespace would diverge from issuer-side validation and
/// could let scope strings through that the issuer never intended to grant.
/// </summary>
public sealed class ScopeClaimParserTests
{
    // ------------------------------------------------------------------
    // ParseString — happy path + null/whitespace
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void ParseString_NullOrWhitespace_ReturnsEmptySet(string? input)
    {
        // Note: " " (single space) → Falsey returns false; the string IS split
        // and produces an empty set after RemoveEmptyEntries. Either way, count == 0.
        var result = ScopeClaimParser.ParseString(input);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseString_SingleScope_ReturnsOneElement()
    {
        const string input = "self.read";

        var result = ScopeClaimParser.ParseString(input);

        result.Should().ContainSingle().Which.Should().Be("self.read");
    }

    [Fact]
    public void ParseString_MultipleSpaceSeparated_ReturnsAll()
    {
        const string input = "self.read self.write auth.password.change";

        var result = ScopeClaimParser.ParseString(input);

        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(["self.read", "self.write", "auth.password.change"]);
    }

    [Fact]
    public void ParseString_DuplicateScopes_Deduplicated()
    {
        const string input = "self.read self.write self.read self.read";

        var result = ScopeClaimParser.ParseString(input);

        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(["self.read", "self.write"]);
    }

    [Fact]
    public void ParseString_MultipleConsecutiveSpaces_TreatedAsOneSeparator()
    {
        // RemoveEmptyEntries handles consecutive spaces.
        const string input = "a  b   c";

        var result = ScopeClaimParser.ParseString(input);

        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(["a", "b", "c"]);
    }

    // ------------------------------------------------------------------
    // ParseString — RFC 6749 §3.3 strictness (THE CRITICAL TESTS)
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("a\tb")]
    [InlineData("a\rb")]
    [InlineData("a\nb")]
    [InlineData("a\r\nb")]
    [InlineData("a\fb")]
    [InlineData("a\vb")]
    public void ParseString_AsciiWhitespaceOtherThanSpace_DoesNotSplit(string input)
    {
        // Adversarial: a sloppy parser using string.Split() with no separator
        // arg would split on all whitespace. RFC 6749 §3.3 grammar = SP only.
        // The whole input must remain a single scope token.
        var result = ScopeClaimParser.ParseString(input);

        result.Should().ContainSingle().Which.Should().Be(input);
    }

    [Theory]
    [InlineData("a b")] // NBSP
    [InlineData("a b")] // EM SPACE
    [InlineData("a b")] // THIN SPACE
    [InlineData("a　b")] // IDEOGRAPHIC SPACE
    public void ParseString_UnicodeWhitespace_DoesNotSplit(string input)
    {
        // Adversarial: a parser using char.IsWhiteSpace() would split here.
        // We must preserve the whole string as one token.
        var result = ScopeClaimParser.ParseString(input);

        result.Should().ContainSingle().Which.Should().Be(input);
    }

    // ------------------------------------------------------------------
    // Parse(JsonElement) — String shape
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_StringElement_DelegatesToParseString()
    {
        using var doc = JsonDocument.Parse("\"self.read self.write\"");

        var result = ScopeClaimParser.Parse(doc.RootElement);

        result.Should().BeEquivalentTo(["self.read", "self.write"]);
    }

    [Fact]
    public void Parse_EmptyStringElement_ReturnsEmptySet()
    {
        using var doc = JsonDocument.Parse("\"\"");

        var result = ScopeClaimParser.Parse(doc.RootElement);

        result.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // Parse(JsonElement) — Array shape
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_ArrayElement_ReturnsSet()
    {
        using var doc = JsonDocument.Parse("""["self.read","self.write"]""");

        var result = ScopeClaimParser.Parse(doc.RootElement);

        result.Should().BeEquivalentTo(["self.read", "self.write"]);
    }

    [Fact]
    public void Parse_ArrayWithEmptyStrings_DropsEmpty()
    {
        // No-empty-strings-as-data invariant — array elements that are "" must
        // be filtered, not added to the set.
        using var doc = JsonDocument.Parse("""["self.read","","self.write",""]""");

        var result = ScopeClaimParser.Parse(doc.RootElement);

        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(["self.read", "self.write"]);
    }

    [Fact]
    public void Parse_ArrayWithNonStringElements_DropsNonStrings()
    {
        // Adversarial: array containing numbers / objects / null. Only string
        // elements should make it into the set.
        using var doc = JsonDocument.Parse("""["self.read",42,null,{"x":1},"self.write",true]""");

        var result = ScopeClaimParser.Parse(doc.RootElement);

        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(["self.read", "self.write"]);
    }

    [Fact]
    public void Parse_EmptyArray_ReturnsEmptySet()
    {
        using var doc = JsonDocument.Parse("[]");

        var result = ScopeClaimParser.Parse(doc.RootElement);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ArrayDoesNotApplyRfcSplitting()
    {
        // Adversarial: array element containing a space should remain ONE token,
        // not get split. Splitting only happens in the String shape.
        using var doc = JsonDocument.Parse("""["self.read self.write"]""");

        var result = ScopeClaimParser.Parse(doc.RootElement);

        result.Should().ContainSingle().Which.Should().Be("self.read self.write");
    }

    // ------------------------------------------------------------------
    // Parse(JsonElement) — non-string-non-array shapes
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("{\"k\":\"v\"}")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    public void Parse_NonStringNonArrayElement_ReturnsEmptySet(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var result = ScopeClaimParser.Parse(doc.RootElement);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_UndefinedElement_ReturnsEmptySet()
    {
        var result = ScopeClaimParser.Parse(default);

        result.Should().BeEmpty();
    }

    // ------------------------------------------------------------------
    // Adversarial — DoS / pathological size
    // ------------------------------------------------------------------

    [Fact]
    public void ParseString_VeryLongSingleScope_Preserved()
    {
        // 1000-char scope token — non-standard but must round-trip cleanly.
        var longScope = new string('x', 1000);

        var result = ScopeClaimParser.ParseString(longScope);

        result.Should().ContainSingle().Which.Should().HaveLength(1000);
    }

    [Fact]
    public void ParseString_ManyScopes_Handled()
    {
        // 1000 unique scopes — typical token will have a handful, but must scale.
        var sb = new StringBuilder();
        for (var i = 0; i < 1000; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append("scope.").Append(i);
        }

        var result = ScopeClaimParser.ParseString(sb.ToString());

        result.Should().HaveCount(1000);
    }

    // ------------------------------------------------------------------
    // RFC 6749 grammar — visible ASCII scope-token chars survive
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("self.read.profile")]
    [InlineData("auth-password-change")]
    [InlineData("files/upload")]
    [InlineData("admin:write")]
    [InlineData("rfc#fragment")]
    [InlineData("scope+plus")]
    public void ParseString_VisibleAsciiSpecialChars_Preserved(string scope)
    {
        var result = ScopeClaimParser.ParseString(scope);

        result.Should().ContainSingle().Which.Should().Be(scope);
    }

    [Fact]
    public void ParseString_OrdinalCaseSensitive_DoesNotDeduplicateAcrossCase()
    {
        // HashSet uses StringComparer.Ordinal — scope.read and SCOPE.READ are
        // distinct tokens. Forces scope-mint discipline (canonical case at issuer).
        const string input = "self.read SELF.READ Self.Read";

        var result = ScopeClaimParser.ParseString(input);

        result.Should().HaveCount(3);
    }
}
