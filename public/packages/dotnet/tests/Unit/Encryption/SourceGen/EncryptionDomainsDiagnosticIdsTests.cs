// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsDiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.SourceGen;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.EncryptionDomains.SourceGen;
using Xunit;

/// <summary>
/// Pins every <see cref="DiagnosticIds"/> constant in the EncryptionDomains
/// SrcGen to its documented identifier shape and confirms there are no
/// duplicates. Diagnostic IDs ship as part of the lib's contract.
/// </summary>
public sealed class EncryptionDomainsDiagnosticIdsTests
{
    [Fact]
    public void IdConstants_FollowD2EDPrefixWithThreeDigits()
    {
        var ids = CollectIds();

        ids.Should().NotBeEmpty();
        ids.Should().AllSatisfy(id =>
            id.Should().MatchRegex(
                @"^D2ED\d{3}$",
                because: "diagnostic IDs follow the D2ED### convention"));
    }

    [Fact]
    public void IdConstants_AreUnique()
    {
        var ids = CollectIds();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("D2ED001", nameof(DiagnosticIds.MalformedSpec))]
    [InlineData("D2ED002", nameof(DiagnosticIds.DuplicateConstName))]
    [InlineData("D2ED003", nameof(DiagnosticIds.DuplicateValue))]
    [InlineData("D2ED004", nameof(DiagnosticIds.InvalidConstName))]
    [InlineData("D2ED005", nameof(DiagnosticIds.EmptyValue))]
    [InlineData("D2ED006", nameof(DiagnosticIds.InvalidMode))]
    [InlineData("D2ED007", nameof(DiagnosticIds.MissingConsumerService))]
    [InlineData("D2ED008", nameof(DiagnosticIds.UnexpectedConsumerService))]
    [InlineData("D2ED009", nameof(DiagnosticIds.InvalidConsumerService))]
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
