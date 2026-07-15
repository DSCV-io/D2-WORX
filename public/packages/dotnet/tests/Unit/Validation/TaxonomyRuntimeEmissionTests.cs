// -----------------------------------------------------------------------
// <copyright file="TaxonomyRuntimeEmissionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation;

using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Validation.Abstractions;
using Xunit;

/// <summary>
/// Runtime-emission pin tests for the codegen-emitted taxonomy enums
/// (<see cref="NamePrefix"/> / <see cref="NameSuffix"/> /
/// <see cref="BiologicalSex"/>). Asserts each member JSON-serializes to its
/// member-name wire string via the embedded
/// <c>JsonStringEnumConverter</c> (round-trip serialize / deserialize) and
/// that an unknown wire code throws — mirroring the strict-deserialization
/// policy used by the geo enums. Drift here would break cross-language wire
/// parity with the TS-side <c>@dcsv-io/d2-validation-abstractions</c> catalog.
/// </summary>
public sealed class TaxonomyRuntimeEmissionTests
{
    [Theory]
    [InlineData(NamePrefix.Mr, "Mr")]
    [InlineData(NamePrefix.Ms, "Ms")]
    [InlineData(NamePrefix.Miss, "Miss")]
    [InlineData(NamePrefix.Mrs, "Mrs")]
    [InlineData(NamePrefix.Mx, "Mx")]
    [InlineData(NamePrefix.Dr, "Dr")]
    [InlineData(NamePrefix.Prof, "Prof")]
    [InlineData(NamePrefix.Sir, "Sir")]
    [InlineData(NamePrefix.Lady, "Lady")]
    [InlineData(NamePrefix.Lord, "Lord")]
    [InlineData(NamePrefix.RtHon, "RtHon")]
    [InlineData(NamePrefix.Rev, "Rev")]
    [InlineData(NamePrefix.Fr, "Fr")]
    [InlineData(NamePrefix.Pr, "Pr")]
    [InlineData(NamePrefix.Sr, "Sr")]
    [InlineData(NamePrefix.Elder, "Elder")]
    [InlineData(NamePrefix.Other, "Other")]
    public void NamePrefix_Member_SerializesToMemberNameAndRoundTrips(
        NamePrefix value, string wire)
    {
        SerializesTo(value, wire);
        RoundTrips(value);
    }

    [Theory]
    [InlineData(NameSuffix.Jr, "Jr")]
    [InlineData(NameSuffix.Sr, "Sr")]
    [InlineData(NameSuffix.I, "I")]
    [InlineData(NameSuffix.II, "II")]
    [InlineData(NameSuffix.III, "III")]
    [InlineData(NameSuffix.IV, "IV")]
    [InlineData(NameSuffix.V, "V")]
    [InlineData(NameSuffix.VI, "VI")]
    [InlineData(NameSuffix.VII, "VII")]
    [InlineData(NameSuffix.VIII, "VIII")]
    [InlineData(NameSuffix.IX, "IX")]
    [InlineData(NameSuffix.X, "X")]
    [InlineData(NameSuffix.Other, "Other")]
    public void NameSuffix_Member_SerializesToMemberNameAndRoundTrips(
        NameSuffix value, string wire)
    {
        SerializesTo(value, wire);
        RoundTrips(value);
    }

    [Theory]
    [InlineData(BiologicalSex.Male, "Male")]
    [InlineData(BiologicalSex.Female, "Female")]
    [InlineData(BiologicalSex.Intersex, "Intersex")]
    [InlineData(BiologicalSex.Unspecified, "Unspecified")]
    public void BiologicalSex_Member_SerializesToMemberNameAndRoundTrips(
        BiologicalSex value, string wire)
    {
        SerializesTo(value, wire);
        RoundTrips(value);
    }

    [Fact]
    public void NamePrefix_UnknownWireCode_ThrowsJsonException()
    {
        var act = () => JsonSerializer.Deserialize<NamePrefix>("\"NotAPrefix\"");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void NameSuffix_UnknownWireCode_ThrowsJsonException()
    {
        var act = () => JsonSerializer.Deserialize<NameSuffix>("\"NotASuffix\"");

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void BiologicalSex_UnknownWireCode_ThrowsJsonException()
    {
        // v1 used "Unknown"; the catalog locked to "Unspecified" — the old wire
        // code must NOT silently deserialize.
        var act = () => JsonSerializer.Deserialize<BiologicalSex>("\"Unknown\"");

        act.Should().Throw<JsonException>();
    }

    private static void SerializesTo<TEnum>(TEnum value, string wire)
        where TEnum : struct, System.Enum
    {
        var json = JsonSerializer.Serialize(value);
        json.Should().Be($"\"{wire}\"");
    }

    private static void RoundTrips<TEnum>(TEnum value)
        where TEnum : struct, System.Enum
    {
        var json = JsonSerializer.Serialize(value);
        var back = JsonSerializer.Deserialize<TEnum>(json);
        back.Should().Be(value);
    }
}
