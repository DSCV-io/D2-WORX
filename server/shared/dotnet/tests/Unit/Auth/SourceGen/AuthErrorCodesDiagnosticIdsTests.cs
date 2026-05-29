// -----------------------------------------------------------------------
// <copyright file="AuthErrorCodesDiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.SourceGen;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using D2.Shared.Auth.ErrorCodes.SourceGen;
using Xunit;

/// <summary>
/// Pins every <see cref="DiagnosticIds"/> constant in the AuthErrorCodes
/// SrcGen to its documented identifier shape and confirms there are no
/// duplicates. Diagnostic IDs ship as part of the lib's contract.
/// </summary>
public sealed class AuthErrorCodesDiagnosticIdsTests
{
    [Fact]
    public void IdConstants_FollowD2AECPrefixWithThreeDigits()
    {
        var ids = CollectIds();

        ids.Should().NotBeEmpty();
        ids.Should().AllSatisfy(id =>
            id.Should().MatchRegex(
                @"^D2AEC\d{3}$",
                because: "diagnostic IDs follow the D2AEC### convention"));
    }

    [Fact]
    public void IdConstants_AreUnique()
    {
        var ids = CollectIds();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("D2AEC001", nameof(DiagnosticIds.MalformedSpec))]
    [InlineData("D2AEC002", nameof(DiagnosticIds.UnknownCategoryEnum))]
    [InlineData("D2AEC003", nameof(DiagnosticIds.DuplicateCode))]
    [InlineData("D2AEC004", nameof(DiagnosticIds.DuplicateFactoryName))]
    [InlineData("D2AEC005", nameof(DiagnosticIds.InvalidHttpStatus))]
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
