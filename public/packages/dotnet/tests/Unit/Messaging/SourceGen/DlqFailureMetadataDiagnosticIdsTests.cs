// -----------------------------------------------------------------------
// <copyright file="DlqFailureMetadataDiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.SourceGen;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Messaging.DlqMetadata.SourceGen;
using Xunit;

/// <summary>
/// Pins every <see cref="DiagnosticIds"/> constant in the DlqFailureMetadata
/// SrcGen to its documented identifier shape and confirms there are no
/// duplicates. Diagnostic IDs ship as part of the lib's contract.
/// </summary>
public sealed class DlqFailureMetadataDiagnosticIdsTests
{
    [Fact]
    public void IdConstants_FollowD2DLQPrefixWithThreeDigits()
    {
        var ids = CollectIds();

        ids.Should().NotBeEmpty();
        ids.Should().AllSatisfy(id =>
            id.Should().MatchRegex(
                @"^D2DLQ\d{3}$",
                because: "diagnostic IDs follow the D2DLQ### convention"));
    }

    [Fact]
    public void IdConstants_AreUnique()
    {
        var ids = CollectIds();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("D2DLQ001", nameof(DiagnosticIds.MalformedSpec))]
    [InlineData("D2DLQ002", nameof(DiagnosticIds.DuplicateFieldConstName))]
    [InlineData("D2DLQ003", nameof(DiagnosticIds.DuplicateFieldValue))]
    [InlineData("D2DLQ004", nameof(DiagnosticIds.DuplicateCause))]
    [InlineData("D2DLQ005", nameof(DiagnosticIds.InvalidConstName))]
    [InlineData("D2DLQ006", nameof(DiagnosticIds.EmptyValue))]
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
