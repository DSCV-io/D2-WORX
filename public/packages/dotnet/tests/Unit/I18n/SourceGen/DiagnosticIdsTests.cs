// -----------------------------------------------------------------------
// <copyright file="DiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.I18n.SourceGen;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.I18n.SourceGen;
using Xunit;

/// <summary>
/// Pins every <see cref="DiagnosticIds"/> constant to its documented identifier
/// shape and confirms there are no duplicates. Diagnostic IDs ship as part of
/// the lib's contract — accidental renames break operator tooling that greps
/// build logs for these IDs.
/// </summary>
public sealed class DiagnosticIdsTests
{
    [Fact]
    public void IdConstants_FollowD2I18NPrefixWithThreeDigits()
    {
        var ids = CollectIds();

        ids.Should().NotBeEmpty();
        ids.Should().AllSatisfy(id =>
            id.Should().MatchRegex(
                @"^D2I18N\d{3}$",
                because: "diagnostic IDs follow the D2I18N### convention"));
    }

    [Fact]
    public void IdConstants_AreUnique()
    {
        // Adversarial: two constants pointing at the same string is a copy-paste bug
        // that would silently merge two distinct diagnostics into one.
        var ids = CollectIds();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("D2I18N001", nameof(DiagnosticIds.InvalidTranslationKey))]
    [InlineData("D2I18N002", nameof(DiagnosticIds.MissingKeyInLocale))]
    [InlineData("D2I18N003", nameof(DiagnosticIds.TranslationKeyCollision))]
    [InlineData("D2I18N004", nameof(DiagnosticIds.OrphanKeyInLocale))]
    [InlineData("D2I18N005", nameof(DiagnosticIds.MissingEnUsJson))]
    [InlineData("D2I18N006", nameof(DiagnosticIds.MalformedJsonCatalog))]
    public void IdConstants_HaveStableValues(string expectedId, string constantName)
    {
        // Stability gate: changing any of these IDs is a breaking change to the
        // build-warning surface. Force any future renumbering to update the test
        // inline data, surfacing the change to PR review.
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
