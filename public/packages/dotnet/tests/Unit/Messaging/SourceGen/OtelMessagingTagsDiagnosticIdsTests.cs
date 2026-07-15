// -----------------------------------------------------------------------
// <copyright file="OtelMessagingTagsDiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.SourceGen;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.OtelMessagingTags.SourceGen;
using Xunit;

/// <summary>
/// Pins every <see cref="DiagnosticIds"/> constant in the OtelMessagingTags
/// SrcGen to its documented identifier shape and confirms there are no
/// duplicates. Diagnostic IDs ship as part of the lib's contract.
/// </summary>
public sealed class OtelMessagingTagsDiagnosticIdsTests
{
    [Fact]
    public void IdConstants_FollowD2OMTPrefixWithThreeDigits()
    {
        var ids = CollectIds();

        ids.Should().NotBeEmpty();
        ids.Should().AllSatisfy(id =>
            id.Should().MatchRegex(
                @"^D2OMT\d{3}$",
                because: "diagnostic IDs follow the D2OMT### convention"));
    }

    [Fact]
    public void IdConstants_AreUnique()
    {
        var ids = CollectIds();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("D2OMT001", nameof(DiagnosticIds.MalformedSpec))]
    [InlineData("D2OMT002", nameof(DiagnosticIds.DuplicateConstName))]
    [InlineData("D2OMT003", nameof(DiagnosticIds.DuplicateValue))]
    [InlineData("D2OMT004", nameof(DiagnosticIds.InvalidConstName))]
    [InlineData("D2OMT005", nameof(DiagnosticIds.EmptyValue))]
    public void IdConstants_HaveStableValues(string expectedId, string constantName)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic;
        var actual = typeof(DiagnosticIds)
            .GetField(constantName, flags)
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
