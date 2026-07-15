// -----------------------------------------------------------------------
// <copyright file="ActionSensitivityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

public sealed class ActionSensitivityTests
{
    [Fact]
    public void Enum_HasExactlyThreeMembers()
    {
        // Adversarial: ActionSensitivity drives risk-score thresholds and audit
        // verbosity. Adding a tier requires updating Edge risk-score logic +
        // every Scopes.GetActionSensitivity caller.
        const int expected_count = 3;

        Enum.GetNames<ActionSensitivity>().Should().HaveCount(expected_count);
    }

    [Fact]
    public void Enum_NamesAreCanonicalSet()
    {
        Enum.GetNames<ActionSensitivity>()
            .Should().BeEquivalentTo("Routine", "Sensitive", "Critical");
    }

    [Theory]
    [InlineData(ActionSensitivity.Routine, 0)]
    [InlineData(ActionSensitivity.Sensitive, 1)]
    [InlineData(ActionSensitivity.Critical, 2)]
    public void Enum_UnderlyingIntValuesAreOrdered(ActionSensitivity sensitivity, int expected)
    {
        // Adversarial: unlike Role, sensitivity int values DO imply an
        // ordering (Routine < Sensitive < Critical) — codebase logic that
        // does `>= Sensitive` checks depends on this. Reordering would
        // silently invert security posture.
        ((int)sensitivity).Should().Be(expected);
    }

    [Theory]
    [InlineData("Routine", ActionSensitivity.Routine)]
    [InlineData("routine", ActionSensitivity.Routine)]
    [InlineData("ROUTINE", ActionSensitivity.Routine)]
    [InlineData("Sensitive", ActionSensitivity.Sensitive)]
    [InlineData("Critical", ActionSensitivity.Critical)]
    [InlineData("critical", ActionSensitivity.Critical)]
    public void Parse_CaseInsensitive_RoundTrips(string input, ActionSensitivity expected)
    {
        Enum.TryParse<ActionSensitivity>(input, ignoreCase: true, out var parsed)
            .Should().BeTrue();
        parsed.Should().Be(expected);
    }

    [Fact]
    public void Parse_GarbageString_ReturnsFalse()
    {
        Enum.TryParse<ActionSensitivity>("Severe", ignoreCase: true, out _).Should().BeFalse();
        Enum.TryParse<ActionSensitivity>("Low", ignoreCase: true, out _).Should().BeFalse();
        Enum.TryParse<ActionSensitivity>(string.Empty, ignoreCase: true, out _).Should().BeFalse();
    }
}
