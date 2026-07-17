// -----------------------------------------------------------------------
// <copyright file="FieldConstraintsDiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation.SourceGen;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Validation.SourceGen;
using Xunit;

/// <summary>
/// Pins every <see cref="DiagnosticIds"/> constant in the field-constraints
/// SrcGen to its documented identifier shape and confirms there are no
/// duplicates. Diagnostic IDs ship as part of the lib's contract.
/// </summary>
public sealed class FieldConstraintsDiagnosticIdsTests
{
    [Fact]
    public void IdConstants_FollowD2FCPrefixWithThreeDigits()
    {
        var ids = CollectIds();

        ids.Should().NotBeEmpty();
        ids.Should().AllSatisfy(id =>
            id.Should().MatchRegex(
                @"^D2FC\d{3}$",
                because: "diagnostic IDs follow the D2FC### convention"));
    }

    [Fact]
    public void IdConstants_AreUnique()
    {
        CollectIds().Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("D2FC001", nameof(DiagnosticIds.MalformedSpec))]
    [InlineData("D2FC002", nameof(DiagnosticIds.DuplicateConstName))]
    [InlineData("D2FC003", nameof(DiagnosticIds.InvalidConstName))]
    [InlineData("D2FC004", nameof(DiagnosticIds.NonPositiveValue))]
    [InlineData("D2FC005", nameof(DiagnosticIds.DuplicateEnumName))]
    [InlineData("D2FC006", nameof(DiagnosticIds.InvalidEnumName))]
    [InlineData("D2FC007", nameof(DiagnosticIds.EmptyEnumMemberList))]
    [InlineData("D2FC008", nameof(DiagnosticIds.DuplicateEnumMember))]
    [InlineData("D2FC009", nameof(DiagnosticIds.InvalidEnumMemberName))]
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
