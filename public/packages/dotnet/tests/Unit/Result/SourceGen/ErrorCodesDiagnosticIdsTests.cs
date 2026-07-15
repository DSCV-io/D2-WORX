// -----------------------------------------------------------------------
// <copyright file="ErrorCodesDiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias ResultErrorCodesSourceGen;

namespace DcsvIo.D2.Tests.Unit.Result.SourceGen;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Xunit;
using DiagnosticIds = ResultErrorCodesSourceGen::DcsvIo.D2.ResultErrorCodes.SourceGen.DiagnosticIds;

/// <summary>
/// Pins every <see cref="DiagnosticIds"/> constant in the generic
/// ErrorCodes SrcGen to its documented identifier shape and confirms there
/// are no duplicates. Diagnostic IDs ship as part of the lib's contract.
/// </summary>
public sealed class ErrorCodesDiagnosticIdsTests
{
    [Fact]
    public void IdConstants_FollowD2ECPrefixWithThreeDigits()
    {
        var ids = CollectIds();

        ids.Should().NotBeEmpty();
        ids.Should().AllSatisfy(id =>
            id.Should().MatchRegex(
                @"^D2EC\d{3}$",
                because: "diagnostic IDs follow the D2EC### convention"));
    }

    [Fact]
    public void IdConstants_AreUnique()
    {
        var ids = CollectIds();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("D2EC001", nameof(DiagnosticIds.MalformedSpec))]
    [InlineData("D2EC002", nameof(DiagnosticIds.DuplicateCode))]
    [InlineData("D2EC003", nameof(DiagnosticIds.InvalidHttpStatus))]
    [InlineData("D2EC004", nameof(DiagnosticIds.InvalidCode))]
    [InlineData("D2EC005", nameof(DiagnosticIds.MissingDoc))]
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
