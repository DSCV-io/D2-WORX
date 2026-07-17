// -----------------------------------------------------------------------
// <copyright file="KeyDecomposerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.I18n.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.I18n.SourceGen;
using Xunit;

public sealed class KeyDecomposerTests
{
    // ----------------------------------------------------------------------
    // Happy path — well-formed keys decompose to the expected TK path
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("common_errors_NOT_FOUND", "Common", "Errors", "NOT_FOUND")]
    [InlineData("common_errors_UNAUTHORIZED", "Common", "Errors", "UNAUTHORIZED")]
    [InlineData("common_errors_unknown", "Common", "Errors", "UNKNOWN")]
    [InlineData("auth_errors_INVALID_ROLE", "Auth", "Errors", "INVALID_ROLE")]
    [InlineData("auth_errors_SOLE_OWNER_OF_ORGS", "Auth", "Errors", "SOLE_OWNER_OF_ORGS")]
    [InlineData("geo_validation_ip_required", "Geo", "Validation", "IP_REQUIRED")]
    [InlineData(
        "geo_validation_address_line1_required",
        "Geo",
        "Validation",
        "ADDRESS_LINE1_REQUIRED")]
    [InlineData("middleware_errors_INSUFFICIENT_ROLE", "Middleware", "Errors", "INSUFFICIENT_ROLE")]
    public void Decompose_HappyPath_ProducesExpectedTriple(
        string key,
        string expectedDomain,
        string expectedCategory,
        string expectedConstant)
    {
        var result = KeyDecomposer.Decompose(key);

        result.IsValid.Should().BeTrue();
        result.OriginalKey.Should().Be(key);
        result.Domain.Should().Be(expectedDomain);
        result.Category.Should().Be(expectedCategory);
        result.ConstantName.Should().Be(expectedConstant);
        result.InvalidReason.Should().BeNull();
    }

    [Fact]
    public void Decompose_SingleCharSegments_ProducesValidTriple()
    {
        // Boundary: minimal valid key — every segment is one character.
        var result = KeyDecomposer.Decompose("a_b_c");

        result.IsValid.Should().BeTrue();
        result.Domain.Should().Be("A");
        result.Category.Should().Be("B");
        result.ConstantName.Should().Be("C");
    }

    [Fact]
    public void Decompose_AlreadyPascalCaseDomain_PreservesCasing()
    {
        // Adversarial: ToPascalCase is idempotent — a leading-uppercase segment
        // is left untouched. This means callers can mix conventions without breakage.
        var result = KeyDecomposer.Decompose("Common_Errors_NOT_FOUND");

        result.IsValid.Should().BeTrue();
        result.Domain.Should().Be("Common");
        result.Category.Should().Be("Errors");
        result.ConstantName.Should().Be("NOT_FOUND");
    }

    [Fact]
    public void Decompose_MultiWordIdentifier_IsJoinedAndUppercased()
    {
        var result = KeyDecomposer.Decompose("foo_bar_some_long_identifier");

        result.IsValid.Should().BeTrue();
        result.Domain.Should().Be("Foo");
        result.Category.Should().Be("Bar");
        result.ConstantName.Should().Be("SOME_LONG_IDENTIFIER");
    }

    // ----------------------------------------------------------------------
    // Invalid — too few segments
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Decompose_NullOrEmpty_IsInvalid(string? key)
    {
        var result = KeyDecomposer.Decompose(key);

        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().NotBeNullOrWhiteSpace();
        result.Domain.Should().BeEmpty();
        result.Category.Should().BeEmpty();
        result.ConstantName.Should().BeEmpty();
    }

    [Fact]
    public void Decompose_SingleSegment_IsInvalid()
    {
        var result = KeyDecomposer.Decompose("errors");

        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().Contain("3");
    }

    [Fact]
    public void Decompose_TwoSegments_IsInvalid()
    {
        var result = KeyDecomposer.Decompose("common_unknown");

        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().Contain("3");
    }

    // ----------------------------------------------------------------------
    // Invalid — empty segments (consecutive / leading / trailing underscores)
    // ----------------------------------------------------------------------

    [Fact]
    public void Decompose_LeadingUnderscore_IsInvalid()
    {
        // First segment is empty.
        var result = KeyDecomposer.Decompose("_common_errors_X");

        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().Contain("empty");
    }

    [Fact]
    public void Decompose_TrailingUnderscore_IsInvalid()
    {
        // Last segment is empty (after the trailing _).
        var result = KeyDecomposer.Decompose("common_errors_NOT_FOUND_");

        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().Contain("empty");
    }

    [Fact]
    public void Decompose_OnlyTrailingUnderscoreOnTwoSegments_IsInvalid()
    {
        var result = KeyDecomposer.Decompose("common_errors_");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Decompose_ConsecutiveUnderscoresInDomain_IsInvalid()
    {
        // common__errors_X → segments are ["common", "", "errors", "X"]; index 1 is empty.
        var result = KeyDecomposer.Decompose("common__errors_X");

        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().Contain("empty");
    }

    [Fact]
    public void Decompose_ConsecutiveUnderscoresInIdentifier_IsInvalid()
    {
        // common_errors__X → segments are ["common", "errors", "", "X"]; index 2 is empty.
        var result = KeyDecomposer.Decompose("common_errors__X");

        result.IsValid.Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // Invalid — identifier rules (ASCII, no leading digit, no whitespace)
    // ----------------------------------------------------------------------

    [Fact]
    public void Decompose_DomainStartsWithDigit_IsInvalid()
    {
        var result = KeyDecomposer.Decompose("1common_errors_X");

        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().Contain("1common").And.Contain("identifier");
    }

    [Fact]
    public void Decompose_CategoryStartsWithDigit_IsInvalid()
    {
        var result = KeyDecomposer.Decompose("common_1errors_X");

        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().Contain("identifier");
    }

    [Fact]
    public void Decompose_ConstantStartsWithDigit_IsInvalid()
    {
        // common_errors_404_NOT_FOUND → constant joins to "404_NOT_FOUND"; starts with digit.
        var result = KeyDecomposer.Decompose("common_errors_404_NOT_FOUND");

        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().Contain("404_NOT_FOUND");
    }

    [Fact]
    public void Decompose_WhitespaceInSegment_IsInvalid()
    {
        // Segment containing space → invalid C# identifier.
        var result = KeyDecomposer.Decompose("common errors NOT_FOUND");

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Decompose_UnicodeInIdentifier_IsInvalid()
    {
        // ASCII-only restriction keeps emitted code predictable across encoding tooling.
        var result = KeyDecomposer.Decompose("common_errors_naïve");

        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().Contain("identifier");
    }

    [Fact]
    public void Decompose_NonLetterPunctuationInSegment_IsInvalid()
    {
        var result = KeyDecomposer.Decompose("common_errors_NOT-FOUND");

        result.IsValid.Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // Reserved-word collision (defensive — uppercase identifiers shouldn't collide)
    // ----------------------------------------------------------------------

    [Fact]
    public void Decompose_ConstantNameCollidesWithReservedWord_IsInvalid()
    {
        // Hypothetical: common_errors_class → constant name "CLASS" lowercased to "class"
        // matches a reserved word. Defensive guard against future SrcGen changes that
        // might emit non-uppercased identifiers.
        var result = KeyDecomposer.Decompose("common_errors_class");

        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().Contain("reserved");
    }

    // ----------------------------------------------------------------------
    // Property test — every real v1 key must decompose successfully
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("common_errors_BAD_REQUEST")]
    [InlineData("common_errors_NOT_FOUND")]
    [InlineData("common_errors_UNAUTHORIZED")]
    [InlineData("common_errors_FORBIDDEN")]
    [InlineData("common_errors_CONFLICT")]
    [InlineData("common_errors_TOO_MANY_REQUESTS")]
    [InlineData("common_errors_REQUEST_FAILED")]
    [InlineData("common_errors_VALIDATION_FAILED")]
    [InlineData("common_errors_SERVICE_UNAVAILABLE")]
    [InlineData("common_errors_PAYLOAD_TOO_LARGE")]
    [InlineData("common_errors_CANCELED")]
    [InlineData("common_errors_SOME_FOUND")]
    [InlineData("common_errors_unknown")]
    [InlineData("common_validation_IP_INVALID")]
    [InlineData("common_validation_HASH_ID_INVALID")]
    [InlineData("common_validation_EMAIL_INVALID")]
    [InlineData("common_validation_PHONE_INVALID")]
    [InlineData("common_validation_NON_EMPTY_LIST")]
    [InlineData("geo_validation_ip_required")]
    [InlineData("geo_validation_latitude_range")]
    [InlineData("geo_validation_address_line1_required")]
    [InlineData("auth_errors_EMAIL_ALREADY_TAKEN")]
    [InlineData("auth_errors_EMULATION_CONSENT_ALREADY_EXISTS")]
    [InlineData("auth_errors_PASSWORD_NUMERIC_ONLY")]
    [InlineData("middleware_errors_INSUFFICIENT_ROLE")]
    [InlineData("files_errors_INVALID_UPLOAD_TARGET")]
    [InlineData("comms_errors_NO_DELIVERABLE_CHANNELS")]
    public void Decompose_RealCatalogKey_ProducesValidDecomposition(string realKey)
    {
        // Property: every key shipped in contracts/messages/en-US.json must
        // decompose cleanly. Failure here means SrcGen would have rejected
        // a real key — drift detector for the JSON catalog.
        var result = KeyDecomposer.Decompose(realKey);

        result.IsValid.Should().BeTrue(
            because: $"key '{realKey}' is from the live catalog and must decompose; " +
                $"got: {result.InvalidReason}");
    }
}
