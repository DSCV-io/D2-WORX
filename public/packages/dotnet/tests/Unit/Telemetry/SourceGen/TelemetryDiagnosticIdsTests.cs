// -----------------------------------------------------------------------
// <copyright file="TelemetryDiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry.SourceGen;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Telemetry.Tags.SourceGen;
using Xunit;

/// <summary>
/// Pins every <see cref="DiagnosticIds"/> constant in the telemetry-tags
/// SrcGen to its documented identifier shape and confirms there are no
/// duplicates.
/// </summary>
public sealed class TelemetryDiagnosticIdsTests
{
    [Fact]
    public void IdConstants_FollowD2TELPrefixWithThreeDigits()
    {
        var ids = CollectIds();

        ids.Should().NotBeEmpty();
        ids.Should().AllSatisfy(id =>
            id.Should().MatchRegex(
                @"^D2TEL\d{3}$",
                because: "diagnostic IDs follow the D2TEL### convention"));
    }

    [Fact]
    public void IdConstants_AreUnique()
    {
        var ids = CollectIds();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("D2TEL001", nameof(DiagnosticIds.MalformedSpec))]
    [InlineData("D2TEL002", nameof(DiagnosticIds.DuplicateMeter))]
    [InlineData("D2TEL003", nameof(DiagnosticIds.DuplicateInstrument))]
    [InlineData("D2TEL004", nameof(DiagnosticIds.UnknownInstrumentKind))]
    [InlineData("D2TEL005", nameof(DiagnosticIds.DuplicateTagValue))]
    [InlineData("D2TEL006", nameof(DiagnosticIds.CrossSpecInconsistency))]
    public void IdConstants_HaveStableValues(string expectedId, string constantName)
    {
        var actual = typeof(DiagnosticIds)
            .GetField(
                constantName, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            !.GetRawConstantValue();

        actual.Should().Be(expectedId);
    }

    private static List<string> CollectIds() =>
        typeof(DiagnosticIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(string) && f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
}
