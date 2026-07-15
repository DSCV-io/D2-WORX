// -----------------------------------------------------------------------
// <copyright file="AudiencesDiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.SourceGen;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Audiences.SourceGen;
using Xunit;

/// <summary>
/// Pins every <c>DcsvIo.D2.Auth.Audiences.SourceGen.DiagnosticIds</c> constant
/// to its documented identifier shape and confirms there are no duplicates.
/// Diagnostic IDs ship as part of the lib's contract — accidental renames break
/// operator tooling that greps build logs for these IDs.
/// </summary>
public sealed class AudiencesDiagnosticIdsTests
{
    [Fact]
    public void IdConstants_FollowD2AUDPrefixWithThreeDigits()
    {
        var ids = CollectIds();

        ids.Should().NotBeEmpty();
        ids.Should().AllSatisfy(id =>
            id.Should().MatchRegex(
                @"^D2AUD\d{3}$",
                because: "diagnostic IDs follow the D2AUD### convention"));
    }

    [Fact]
    public void IdConstants_AreUnique()
    {
        // Adversarial: two constants pointing at the same string is a copy-paste
        // bug that would silently merge two distinct diagnostics into one.
        var ids = CollectIds();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("D2AUD001", nameof(DiagnosticIds.MalformedSpec))]
    [InlineData("D2AUD002", nameof(DiagnosticIds.InvalidAudienceName))]
    [InlineData("D2AUD003", nameof(DiagnosticIds.DuplicateAudienceName))]
    [InlineData("D2AUD004", nameof(DiagnosticIds.DuplicateAudienceUrl))]
    [InlineData("D2AUD005", nameof(DiagnosticIds.InvalidAudienceUrl))]
    [InlineData("D2AUD006", nameof(DiagnosticIds.MissingSpecFile))]
    public void IdConstants_HaveStableValues(string expectedId, string constantName)
    {
        // Stability gate: changing any of these IDs is a breaking change to the
        // build-warning surface. Force any future renumbering to update the
        // test inline data, surfacing the change to PR review.
        var actual = typeof(DiagnosticIds)
            .GetField(constantName, BindingFlags.Public | BindingFlags.Static)
            !.GetRawConstantValue();

        actual.Should().Be(expectedId);
    }

    private static List<string> CollectIds() =>
        typeof(DiagnosticIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
}
