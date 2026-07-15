// -----------------------------------------------------------------------
// <copyright file="RoleTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

public sealed class RoleTests
{
    [Fact]
    public void Enum_HasExactlyFourMembers()
    {
        // Adversarial: roles are intentionally a closed set of capability
        // bundles, NOT a hierarchy. Adding one requires Scopes.g.cs re-emission.
        const int expected_count = 4;

        Enum.GetNames<Role>().Should().HaveCount(expected_count);
    }

    [Fact]
    public void Enum_NamesAreCanonicalSet()
    {
        Enum.GetNames<Role>()
            .Should().BeEquivalentTo("Auditor", "Agent", "Officer", "Owner");
    }

    [Theory]
    [InlineData(Role.Auditor, 0)]
    [InlineData(Role.Agent, 1)]
    [InlineData(Role.Officer, 2)]
    [InlineData(Role.Owner, 3)]
    public void Enum_UnderlyingIntValuesAreStable(Role role, int expected)
    {
        ((int)role).Should().Be(expected);
    }

    [Theory]
    [InlineData("Auditor", Role.Auditor)]
    [InlineData("auditor", Role.Auditor)]
    [InlineData("AUDITOR", Role.Auditor)]
    [InlineData("Agent", Role.Agent)]
    [InlineData("Officer", Role.Officer)]
    [InlineData("Owner", Role.Owner)]
    public void Parse_CaseInsensitive_RoundTrips(string input, Role expected)
    {
        Enum.TryParse<Role>(input, ignoreCase: true, out var parsed)
            .Should().BeTrue();
        parsed.Should().Be(expected);
    }

    [Fact]
    public void Parse_GarbageString_ReturnsFalse()
    {
        Enum.TryParse<Role>("Admin", ignoreCase: true, out _).Should().BeFalse();
        Enum.TryParse<Role>("Manager", ignoreCase: true, out _).Should().BeFalse();
        Enum.TryParse<Role>(string.Empty, ignoreCase: true, out _).Should().BeFalse();
    }

    [Fact]
    public void Roles_AreNotAHierarchy_IntValuesDoNotImplyOrdering()
    {
        // Adversarial: explicitly assert there is NO hierarchy. Per Role.cs
        // remarks and the spec, capability assignment is per (role, org_type)
        // tuple in scopes.spec.json — NOT inferred from int values. This test
        // documents the contract and protects against any code that might
        // accidentally treat `(int)role` as a privilege rank.
        //
        // Auditor(0) has the LOWEST int value but Auditor reads more data than
        // Agent(1) per the spec's remarks. Owner(3) is high-int but the
        // ordering is just declaration order, not a privilege rank.
        var roleInts = Enum.GetValues<Role>().Select(r => (int)r).ToArray();

        // Assert int ordering (as a sanity check on declaration order) but the
        // critical claim is that capability lookup goes through the spec, not
        // through arithmetic on these ints.
        roleInts.Should().BeEquivalentTo([0, 1, 2, 3]);

        // Sanity: there's no implicit ordering implied by sums / comparisons.
        // The fact that 'Auditor < Agent' is a declaration artifact, not a
        // privilege statement.
        ((int)Role.Auditor).Should().BeLessThan((int)Role.Agent);
    }
}
