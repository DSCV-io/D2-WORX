// -----------------------------------------------------------------------
// <copyright file="ProblemDetailsDiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.ProblemDetails.SourceGen;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.ProblemDetails.SourceGen;
using Xunit;

/// <summary>
/// Pins every <see cref="DiagnosticIds"/> constant in the ProblemDetails
/// SrcGen to its documented identifier shape and confirms there are no
/// duplicates. Diagnostic IDs ship as part of the lib's contract.
/// </summary>
public sealed class ProblemDetailsDiagnosticIdsTests
{
    [Fact]
    public void IdConstants_FollowD2PRBPrefixWithThreeDigits()
    {
        var ids = CollectIds();

        ids.Should().NotBeEmpty();
        ids.Should().AllSatisfy(id =>
            id.Should().MatchRegex(
                @"^D2PRB\d{3}$",
                because: "diagnostic IDs follow the D2PRB### convention"));
    }

    [Fact]
    public void IdConstants_AreUnique()
    {
        var ids = CollectIds();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("D2PRB001", nameof(DiagnosticIds.MalformedSpec))]
    [InlineData("D2PRB002", nameof(DiagnosticIds.DuplicateExtensionKeyConstName))]
    [InlineData("D2PRB003", nameof(DiagnosticIds.DuplicateExtensionKeyValue))]
    [InlineData("D2PRB004", nameof(DiagnosticIds.DuplicateTitleConstName))]
    [InlineData("D2PRB005", nameof(DiagnosticIds.DuplicateTitleHttpStatus))]
    [InlineData("D2PRB006", nameof(DiagnosticIds.TypeUriPrefixMissingTrailingSlash))]
    public void IdConstants_HaveStableValues(string expectedId, string constantName)
    {
        var actual = typeof(DiagnosticIds)
            .GetField(
                constantName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            !.GetRawConstantValue();

        actual.Should().Be(expectedId);
    }

    private static List<string> CollectIds() =>
        typeof(DiagnosticIds)
            .GetFields(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(string) && f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
}
