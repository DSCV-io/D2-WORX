// -----------------------------------------------------------------------
// <copyright file="GrpcTrailersDiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Grpc.SourceGen;

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using D2.Shared.Grpc.Trailers.SourceGen;
using Xunit;

/// <summary>
/// Pins every <see cref="DiagnosticIds"/> constant in the GrpcTrailers
/// SrcGen to its documented identifier shape and confirms there are no
/// duplicates. Diagnostic IDs ship as part of the lib's contract.
/// </summary>
public sealed class GrpcTrailersDiagnosticIdsTests
{
    [Fact]
    public void IdConstants_FollowD2GTPrefixWithThreeDigits()
    {
        var ids = CollectIds();

        ids.Should().NotBeEmpty();
        ids.Should().AllSatisfy(id =>
            id.Should().MatchRegex(
                @"^D2GT\d{3}$",
                because: "diagnostic IDs follow the D2GT### convention"));
    }

    [Fact]
    public void IdConstants_AreUnique()
    {
        var ids = CollectIds();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("D2GT001", nameof(DiagnosticIds.MalformedSpec))]
    [InlineData("D2GT002", nameof(DiagnosticIds.DuplicateConstName))]
    [InlineData("D2GT003", nameof(DiagnosticIds.DuplicateValue))]
    [InlineData("D2GT004", nameof(DiagnosticIds.InvalidConstName))]
    [InlineData("D2GT005", nameof(DiagnosticIds.EmptyValue))]
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
