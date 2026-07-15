// -----------------------------------------------------------------------
// <copyright file="ImpersonationKindTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

public sealed class ImpersonationKindTests
{
    [Fact]
    public void Enum_HasExactlyTwoMembers()
    {
        // Adversarial: adding a new ImpersonationKind value affects scope
        // mint stripping (Force is admin-only); silent additions are dangerous.
        const int expected_count = 2;

        Enum.GetNames<ImpersonationKind>().Should().HaveCount(expected_count);
    }

    [Fact]
    public void Enum_NamesAreConsentAndForce()
    {
        Enum.GetNames<ImpersonationKind>()
            .Should().BeEquivalentTo("Consent", "Force");
    }

    [Theory]
    [InlineData(ImpersonationKind.Consent, 0)]
    [InlineData(ImpersonationKind.Force, 1)]
    public void Enum_UnderlyingIntValuesAreStable(ImpersonationKind kind, int expected)
    {
        ((int)kind).Should().Be(expected);
    }

    [Theory]
    [InlineData("Consent", ImpersonationKind.Consent)]
    [InlineData("consent", ImpersonationKind.Consent)]
    [InlineData("CONSENT", ImpersonationKind.Consent)]
    [InlineData("Force", ImpersonationKind.Force)]
    [InlineData("force", ImpersonationKind.Force)]
    [InlineData("FORCE", ImpersonationKind.Force)]
    public void Parse_CaseInsensitive_RoundTrips(string input, ImpersonationKind expected)
    {
        Enum.TryParse<ImpersonationKind>(input, ignoreCase: true, out var parsed)
            .Should().BeTrue();
        parsed.Should().Be(expected);
    }

    [Fact]
    public void Parse_GarbageString_ReturnsFalse()
    {
        Enum.TryParse<ImpersonationKind>("Forc", ignoreCase: true, out _).Should().BeFalse();
        Enum.TryParse<ImpersonationKind>("Consensus", ignoreCase: true, out _).Should().BeFalse();
        Enum.TryParse<ImpersonationKind>(string.Empty, ignoreCase: true, out _).Should().BeFalse();
    }
}
