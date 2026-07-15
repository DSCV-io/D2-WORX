// -----------------------------------------------------------------------
// <copyright file="ScopeMatchTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Abstractions;

using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

/// <summary>
/// Reflection-based name + count pins for <see cref="ScopeMatch"/>.
/// Mirrors the <c>HandlerScopeMatch</c> pins in
/// <c>DcsvIo.D2.Tests.Unit.Handler.ScopeRequirementTests</c>. Member names
/// and the count are load-bearing: the auth middleware, fluent-extension
/// callers, and generated analyzer output branch on them. A rename must
/// trip an obvious test failure.
/// </summary>
/// <remarks>
/// We deliberately do NOT use <c>nameof()</c> in the expected-value
/// arguments: <c>nameof</c> is compile-time–resolved and would silently
/// follow any rename, defeating the pin. The literal strings here are the
/// contract.
/// </remarks>
public sealed class ScopeMatchTests
{
    [Fact]
    public void ScopeMatch_HasAnyMember_WithExpectedName()
    {
        var member = typeof(ScopeMatch).GetField("Any");

        member.Should().NotBeNull();
        member.Name.Should().Be("Any");
    }

    [Fact]
    public void ScopeMatch_HasAllMember_WithExpectedName()
    {
        var member = typeof(ScopeMatch).GetField("All");

        member.Should().NotBeNull();
        member.Name.Should().Be("All");
    }

    [Fact]
    public void ScopeMatch_EnumHasExactlyTwoMembers()
    {
        // Adversarial: any future member addition to ScopeMatch requires
        // updating the auth middleware branch logic + all call sites that
        // switch on ScopeMatch. Pin the count so an addition trips this
        // test and forces a conscious review.
        var members = typeof(ScopeMatch).GetFields(BindingFlags.Public | BindingFlags.Static);

        members.Should().HaveCount(2);
    }
}
