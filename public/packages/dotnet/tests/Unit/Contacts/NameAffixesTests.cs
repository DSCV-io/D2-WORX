// -----------------------------------------------------------------------
// <copyright file="NameAffixesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Contacts;

using AwesomeAssertions;
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Validation.Abstractions;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="NameAffixes"/>: all-null rejection plus
/// the custom-required-iff-<c>Other</c> coherence rule on both the prefix and
/// suffix sides, including length caps and whitespace-only custom values.
/// </summary>
public sealed class NameAffixesTests
{
    // -----------------------------------------------------------------------
    // Happy paths
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_PrefixAndSuffixEnumsOnly_ReturnsOk()
    {
        var result = NameAffixes.Create(NamePrefix.Dr, suffix: NameSuffix.Jr);

        result.Success.Should().BeTrue();
        result.Data!.Prefix.Should().Be(NamePrefix.Dr);
        result.Data!.Suffix.Should().Be(NameSuffix.Jr);
        result.Data!.PrefixCustom.Should().BeNull();
        result.Data!.SuffixCustom.Should().BeNull();
    }

    [Fact]
    public void Create_PrefixOtherWithCustom_ReturnsOk_CustomCleaned()
    {
        var result = NameAffixes.Create(NamePrefix.Other, "  Captain  ");

        result.Success.Should().BeTrue();
        result.Data!.Prefix.Should().Be(NamePrefix.Other);
        result.Data!.PrefixCustom.Should().Be("Captain");
    }

    [Fact]
    public void Create_SuffixOtherWithCustom_ReturnsOk()
    {
        var result = NameAffixes.Create(suffix: NameSuffix.Other, suffixCustom: "Esq.");

        result.Success.Should().BeTrue();
        result.Data!.Suffix.Should().Be(NameSuffix.Other);
        result.Data!.SuffixCustom.Should().Be("Esq.");
    }

    // -----------------------------------------------------------------------
    // All-null degenerate record
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_AllNull_ReturnsAffixesEmptyRecord()
    {
        var result = NameAffixes.Create();

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.AFFIXES_EMPTY_RECORD.Key);
    }

    [Fact]
    public void Create_AllWhitespaceCustoms_ReturnsAffixesEmptyRecord()
    {
        var result = NameAffixes.Create(prefixCustom: "   ", suffixCustom: "  ");

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.AFFIXES_EMPTY_RECORD.Key);
    }

    // -----------------------------------------------------------------------
    // Prefix custom coherence
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_PrefixOtherWithoutCustom_ReturnsPrefixCustomRequired()
    {
        var result = NameAffixes.Create(NamePrefix.Other);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.PREFIX_CUSTOM_REQUIRED.Key);
    }

    [Fact]
    public void Create_PrefixOtherWithWhitespaceCustom_ReturnsPrefixCustomRequired()
    {
        var result = NameAffixes.Create(NamePrefix.Other, "   ");

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.PREFIX_CUSTOM_REQUIRED.Key);
    }

    [Fact]
    public void Create_NonOtherPrefixWithCustom_ReturnsPrefixCustomNotAllowed()
    {
        var result = NameAffixes.Create(NamePrefix.Mr, "Captain");

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.PREFIX_CUSTOM_NOT_ALLOWED.Key);
    }

    [Fact]
    public void Create_NullPrefixWithCustom_ReturnsPrefixCustomNotAllowed()
    {
        var result = NameAffixes.Create(prefixCustom: "Captain");

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.PREFIX_CUSTOM_NOT_ALLOWED.Key);
    }

    [Fact]
    public void Create_PrefixCustomExactlyMax_ReturnsOk()
    {
        var atMax = new string('x', FieldConstraints.AFFIX_CUSTOM_MAX);
        var result = NameAffixes.Create(NamePrefix.Other, atMax);

        result.Success.Should().BeTrue();
        result.Data!.PrefixCustom.Should().HaveLength(FieldConstraints.AFFIX_CUSTOM_MAX);
    }

    [Fact]
    public void Create_PrefixCustomOverMax_ReturnsPrefixCustomTooLong()
    {
        var overMax = new string('x', FieldConstraints.AFFIX_CUSTOM_MAX + 1);
        var result = NameAffixes.Create(NamePrefix.Other, overMax);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.PREFIX_CUSTOM_TOO_LONG.Key);
    }

    // -----------------------------------------------------------------------
    // Suffix custom coherence (symmetric)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_SuffixOtherWithoutCustom_ReturnsSuffixCustomRequired()
    {
        var result = NameAffixes.Create(suffix: NameSuffix.Other);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.SUFFIX_CUSTOM_REQUIRED.Key);
    }

    [Fact]
    public void Create_SuffixOtherWithWhitespaceCustom_ReturnsSuffixCustomRequired()
    {
        var result = NameAffixes.Create(suffix: NameSuffix.Other, suffixCustom: "   ");

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.SUFFIX_CUSTOM_REQUIRED.Key);
    }

    [Fact]
    public void Create_NonOtherSuffixWithCustom_ReturnsSuffixCustomNotAllowed()
    {
        var result = NameAffixes.Create(suffix: NameSuffix.Jr, suffixCustom: "the Great");

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.SUFFIX_CUSTOM_NOT_ALLOWED.Key);
    }

    [Fact]
    public void Create_NullSuffixWithCustom_ReturnsSuffixCustomNotAllowed()
    {
        var result = NameAffixes.Create(suffixCustom: "the Great");

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.SUFFIX_CUSTOM_NOT_ALLOWED.Key);
    }

    [Fact]
    public void Create_SuffixCustomExactlyMax_ReturnsOk()
    {
        var atMax = new string('y', FieldConstraints.AFFIX_CUSTOM_MAX);
        var result = NameAffixes.Create(suffix: NameSuffix.Other, suffixCustom: atMax);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Create_SuffixCustomOverMax_ReturnsSuffixCustomTooLong()
    {
        var overMax = new string('y', FieldConstraints.AFFIX_CUSTOM_MAX + 1);
        var result = NameAffixes.Create(suffix: NameSuffix.Other, suffixCustom: overMax);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.SUFFIX_CUSTOM_TOO_LONG.Key);
    }

    // -----------------------------------------------------------------------
    // Both sides custom-Other together
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_BothOtherWithCustoms_ReturnsOk()
    {
        var result = NameAffixes.Create(
            NamePrefix.Other, "Captain", NameSuffix.Other, "Ret.");

        result.Success.Should().BeTrue();
        result.Data!.PrefixCustom.Should().Be("Captain");
        result.Data!.SuffixCustom.Should().Be("Ret.");
    }
}
