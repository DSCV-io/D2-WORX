// -----------------------------------------------------------------------
// <copyright file="LanguageCodeJsonWireTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.Abstractions;

using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using Xunit;

/// <summary>
/// JSON wire round-trip coverage for <see cref="LanguageCode"/>.
/// Pins that the <c>[JsonStringEnumMemberName]</c> attribute on each member
/// drives the wire form to the canonical lowercase ISO 639-1 code (e.g.
/// <c>"en"</c>, NOT the PascalCased C# member name <c>"En"</c>).
/// <c>JsonStringEnumConverter</c> honors <c>[JsonStringEnumMemberName]</c>
/// (the .NET 9+ attribute) — NOT <c>[EnumMember]</c> (DataContract), which
/// <c>System.Text.Json</c> ignores. These tests fail when the emitter
/// regresses to <c>[EnumMember]</c>.
/// </summary>
public sealed class LanguageCodeJsonWireTests
{
    // §1.2 category: Domain-specific — serialize: member name → ISO wire form.
    [Theory]
    [InlineData(LanguageCode.En, "\"en\"")]
    [InlineData(LanguageCode.Fr, "\"fr\"")]
    [InlineData(LanguageCode.Ar, "\"ar\"")]
    [InlineData(LanguageCode.Zh, "\"zh\"")]
    [InlineData(LanguageCode.Ja, "\"ja\"")]
    public void Serialize_LanguageCode_ProducesLowercaseIsoCode(
        LanguageCode code, string expectedJson)
    {
        var json = JsonSerializer.Serialize(code);

        json.Should().Be(expectedJson);
    }

    // §1.2 category: Domain-specific — deserialize: ISO wire form → member.
    [Theory]
    [InlineData("\"en\"", LanguageCode.En)]
    [InlineData("\"fr\"", LanguageCode.Fr)]
    [InlineData("\"ar\"", LanguageCode.Ar)]
    [InlineData("\"zh\"", LanguageCode.Zh)]
    [InlineData("\"ja\"", LanguageCode.Ja)]
    public void Deserialize_LowercaseIsoCode_ReturnsCorrectMember(
        string json, LanguageCode expected)
    {
        var result = JsonSerializer.Deserialize<LanguageCode>(json);

        result.Should().Be(expected);
    }

    // §1.2 category: Domain-specific — full round-trip: value → json → value.
    [Fact]
    public void RoundTrip_LanguageCodeEn_SurvivesJsonRoundTrip()
    {
        var original = LanguageCode.En;
        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<LanguageCode>(json);

        // Wire form must be the ISO code, not the PascalCased member name.
        json.Should().Be("\"en\"");
        restored.Should().Be(original);
    }

    // §1.2 category: Adversarial — PascalCase member name is NOT accepted.
    // If the emitter had regressed to no attribute (or [EnumMember] which
    // JsonStringEnumConverter ignores), JsonSerializer would have serialized
    // "En" and this deserialization would have succeeded — proving the bug
    // is present. Post-fix, JsonStringEnumConverter is strict: "En" (the raw
    // member name when [JsonStringEnumMemberName("en")] overrides it) is no
    // longer a valid wire value, and deserialization must throw.
    [Fact]
    public void Deserialize_PascalCaseMemberName_ThrowsWhenOverriddenByAttribute()
    {
        // "En" is the C# member name; the wire form is "en" (attribute override).
        // Strict JsonStringEnumConverter rejects "En" when [JsonStringEnumMemberName("en")]
        // is present — the member name is no longer recognized as a wire alias.
        var act = () => JsonSerializer.Deserialize<LanguageCode>("\"En\"");

        act.Should().Throw<JsonException>();
    }

    // §1.2 category: Adversarial — unknown wire value throws.
    [Fact]
    public void Deserialize_UnknownCode_ThrowsJsonException()
    {
        var act = () => JsonSerializer.Deserialize<LanguageCode>("\"xx\"");

        act.Should().Throw<JsonException>();
    }
}
