// -----------------------------------------------------------------------
// <copyright file="ScopeRequirementTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using System.Collections.Generic;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Handler.Abstractions;
using Xunit;

public sealed class ScopeRequirementTests
{
    // ----------------------------------------------------------------------
    // 1. HandlerScopeMatch — enum member names + values are load-bearing for
    //    any serialization / runtime switch that branches on them. Pin via
    //    reflection so a rename trips an obvious test failure.
    // ----------------------------------------------------------------------

    [Fact]
    public void HandlerScopeMatch_HasAnyMember_WithExpectedName()
    {
        var member = typeof(HandlerScopeMatch).GetField(nameof(HandlerScopeMatch.Any));

        member.Should().NotBeNull();
        member.Name.Should().Be("Any");
    }

    [Fact]
    public void HandlerScopeMatch_HasAllMember_WithExpectedName()
    {
        var member = typeof(HandlerScopeMatch).GetField(nameof(HandlerScopeMatch.All));

        member!.Name.Should().Be("All");
    }

    [Fact]
    public void HandlerScopeMatch_EnumHasExactlyTwoMembers()
    {
        // Adversarial: any future member addition to HandlerScopeMatch
        // requires updating the BaseHandler branch logic. Pin the count so
        // the addition trips this test and forces a conscious review.
        var members = typeof(HandlerScopeMatch).GetFields(BindingFlags.Public | BindingFlags.Static);

        members.Should().HaveCount(2);
    }

    // ----------------------------------------------------------------------
    // 2. ScopeRequirement — record construction, property access, equality
    // ----------------------------------------------------------------------

    [Fact]
    public void ScopeRequirement_Ctor_AnyMatch_ExposesProperties()
    {
        IReadOnlySet<string> scopes = new HashSet<string>(["a", "b"]);

        var req = new ScopeRequirement(HandlerScopeMatch.Any, scopes);

        req.Match.Should().Be(HandlerScopeMatch.Any);
        req.Scopes.Should().BeSameAs(scopes);
    }

    [Fact]
    public void ScopeRequirement_Ctor_AllMatch_ExposesProperties()
    {
        IReadOnlySet<string> scopes = new HashSet<string>(["read", "write"]);

        var req = new ScopeRequirement(HandlerScopeMatch.All, scopes);

        req.Match.Should().Be(HandlerScopeMatch.All);
        req.Scopes.Should().BeSameAs(scopes);
    }

    [Fact]
    public void ScopeRequirement_EmptyScopes_Throws()
    {
        // Constructor guard: empty Scopes set is unconstructible — use null
        // ScopeRequirement instead. Regression-pins F5 fix: empty set no
        // longer silently passes (previously "no check" via pipeline guard).
        IReadOnlySet<string> empty_set = new HashSet<string>();

        var act = () => new ScopeRequirement(HandlerScopeMatch.Any, empty_set);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ScopeRequirement.Scopes must contain at least one entry*");
    }

    [Fact]
    public void ScopeRequirement_NullScopes_Throws()
    {
        // Constructor guard: null Scopes set throws ArgumentNullException.
        var act = () => new ScopeRequirement(HandlerScopeMatch.Any, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ScopeRequirement_Equality_SameValues_Equal()
    {
        // Record value-semantics: two instances with identical Match +
        // reference-equal Scopes should be equal.
        IReadOnlySet<string> scopes = new HashSet<string>(["x"]);
        var left = new ScopeRequirement(HandlerScopeMatch.Any, scopes);
        var right = new ScopeRequirement(HandlerScopeMatch.Any, scopes);

        left.Should().Be(right);
    }

    [Fact]
    public void ScopeRequirement_Equality_DifferentMatch_NotEqual()
    {
        IReadOnlySet<string> scopes = new HashSet<string>(["x"]);
        var any = new ScopeRequirement(HandlerScopeMatch.Any, scopes);
        var all = new ScopeRequirement(HandlerScopeMatch.All, scopes);

        any.Should().NotBe(all);
    }

    [Fact]
    public void ScopeRequirement_Equality_DifferentScopes_NotEqual()
    {
        // Two distinct HashSet instances with different contents are not
        // reference-equal → the record's auto-generated Equals sees them
        // as different (IReadOnlySet has no structural equality via
        // System.Object.Equals by default).
        var a = new ScopeRequirement(HandlerScopeMatch.Any, new HashSet<string>(["a"]));
        var b = new ScopeRequirement(HandlerScopeMatch.Any, new HashSet<string>(["b"]));

        a.Should().NotBe(b);
    }

    [Fact]
    public void ScopeRequirement_Equality_SameContentDifferentReferenceScopes_NotEqual()
    {
        // The record's auto-generated Equals delegates to the declared types of
        // each property. IReadOnlySet<string> is typed as an interface — the
        // runtime sees two distinct HashSet<string> references and calls
        // object.Equals on them, which is REFERENCE equality for HashSet<T>
        // (it does not override Equals). Structurally-equal-but-distinct sets
        // are therefore NOT record-equal. This pins that behavior so that any
        // future change to ScopeRequirement (e.g., switching to FrozenSet, adding
        // a custom IEqualityComparer on the record, or using a struct set type
        // that DOES have value-Equals) will trip this test and force a conscious
        // review of the downstream impact on handler-options equality checks.
        IReadOnlySet<string> left_scopes = new HashSet<string>(["read", "write"]);
        IReadOnlySet<string> right_scopes = new HashSet<string>(["read", "write"]); // same content, different reference

        var left = new ScopeRequirement(HandlerScopeMatch.Any, left_scopes);
        var right = new ScopeRequirement(HandlerScopeMatch.Any, right_scopes);

        left.Should().NotBe(right);
    }

    // ----------------------------------------------------------------------
    // 3. Reflection name-pins for ScopeRequirement.Match / Scopes — these
    //    are the property names the BaseHandler pattern-matches against;
    //    renaming them would require updating the pipeline guard.
    // ----------------------------------------------------------------------

    [Fact]
    public void ScopeRequirement_MatchPropertyName_IsPinned()
    {
        var prop = typeof(ScopeRequirement).GetProperty(nameof(ScopeRequirement.Match));

        prop.Should().NotBeNull();
        prop.Name.Should().Be("Match");
    }

    [Fact]
    public void ScopeRequirement_ScopesPropertyName_IsPinned()
    {
        var prop = typeof(ScopeRequirement).GetProperty(nameof(ScopeRequirement.Scopes));

        prop.Should().NotBeNull();
        prop.Name.Should().Be("Scopes");
    }
}
