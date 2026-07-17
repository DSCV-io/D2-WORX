// -----------------------------------------------------------------------
// <copyright file="AdvisoryLocksDiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AdvisoryLocks.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.AdvisoryLocks.SourceGen;
using Xunit;

/// <summary>
/// Pins the <c>D2LCK</c> diagnostic-ID string values and the prefix convention.
/// If a constant is renamed or its value drifted, compiler errors (or this test)
/// surface the regression before it ships.
/// </summary>
public sealed class AdvisoryLocksDiagnosticIdsTests
{
    [Theory]
    [InlineData(DiagnosticIds.MalformedSpec, "D2LCK001")]
    [InlineData(DiagnosticIds.DuplicateConstNameInDatabase, "D2LCK002")]
    [InlineData(DiagnosticIds.DuplicateKeyInDatabase, "D2LCK003")]
    [InlineData(DiagnosticIds.InvalidConstName, "D2LCK004")]
    [InlineData(DiagnosticIds.KeyOutOfRange, "D2LCK005")]
    public void DiagnosticId_HasExpectedValue(string actual, string expected)
    {
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(DiagnosticIds.MalformedSpec)]
    [InlineData(DiagnosticIds.DuplicateConstNameInDatabase)]
    [InlineData(DiagnosticIds.DuplicateKeyInDatabase)]
    [InlineData(DiagnosticIds.InvalidConstName)]
    [InlineData(DiagnosticIds.KeyOutOfRange)]
    public void DiagnosticId_StartsWithD2LCKPrefix(string id)
    {
        id.Should().StartWith("D2LCK", "all advisory-locks diagnostic IDs use the D2LCK prefix");
    }

    [Theory]
    [InlineData(DiagnosticIds.MalformedSpec)]
    [InlineData(DiagnosticIds.DuplicateConstNameInDatabase)]
    [InlineData(DiagnosticIds.DuplicateKeyInDatabase)]
    [InlineData(DiagnosticIds.InvalidConstName)]
    [InlineData(DiagnosticIds.KeyOutOfRange)]
    public void DiagnosticId_HasThreeDigitNumericSuffix(string id)
    {
        var suffix = id[5..]; // after "D2LCK"
        suffix.Should().HaveLength(3);
        int.TryParse(suffix, out _).Should().BeTrue(
            "ID suffix must be a three-digit numeric string");
    }
}
