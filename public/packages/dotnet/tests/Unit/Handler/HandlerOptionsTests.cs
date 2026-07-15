// -----------------------------------------------------------------------
// <copyright file="HandlerOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using System;
using System.Collections.Generic;
using AwesomeAssertions;
using DcsvIo.D2.Handler.Abstractions;
using Xunit;

public sealed class HandlerOptionsTests
{
    // ----------------------------------------------------------------------
    // Defaults — every default is load-bearing for the platform-wide
    // observability contract; document each in a dedicated test so a future
    // accidental change to a default trips a single named failure.
    // ----------------------------------------------------------------------

    [Fact]
    public void DefaultCtor_LogInput_IsTrue()
    {
        var options = new HandlerOptions();

        options.LogInput.Should().BeTrue();
    }

    [Fact]
    public void DefaultCtor_LogOutput_IsTrue()
    {
        var options = new HandlerOptions();

        options.LogOutput.Should().BeTrue();
    }

    [Fact]
    public void DefaultCtor_SlowThreshold_Is100Milliseconds()
    {
        var options = new HandlerOptions();

        options.SlowThreshold.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void DefaultCtor_CriticalThreshold_Is500Milliseconds()
    {
        var options = new HandlerOptions();

        options.CriticalThreshold.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void DefaultCtor_ScopeRequirement_IsNull()
    {
        // Adversarial: null disables the per-handler scope pre-check entirely
        // (the pipeline guard is `is { Scopes.Count: > 0 }`); a non-null
        // ScopeRequirement with an empty Scopes set is treated equivalently
        // but null is the idiomatic "no check" declaration.
        var options = new HandlerOptions();

        options.ScopeRequirement.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // record `with`-expression overrides — verifies record-semantics work
    // through every property without surprise (e.g. an init-only setter
    // accidentally turned into a get-only would silently break overrides).
    // ----------------------------------------------------------------------

    [Fact]
    public void With_LogInputFalse_OverridesOnlyLogInput()
    {
        var defaults = new HandlerOptions();

        var overridden = defaults with { LogInput = false };

        overridden.LogInput.Should().BeFalse();
        overridden.LogOutput.Should().BeTrue();
        overridden.SlowThreshold.Should().Be(defaults.SlowThreshold);
        overridden.CriticalThreshold.Should().Be(defaults.CriticalThreshold);
        overridden.ScopeRequirement.Should().BeNull();
    }

    [Fact]
    public void With_LogOutputFalse_OverridesOnlyLogOutput()
    {
        var defaults = new HandlerOptions();

        var overridden = defaults with { LogOutput = false };

        overridden.LogOutput.Should().BeFalse();
        overridden.LogInput.Should().BeTrue();
    }

    [Fact]
    public void With_SlowThresholdNull_DisablesSlowCheck()
    {
        var defaults = new HandlerOptions();

        var overridden = defaults with { SlowThreshold = null };

        overridden.SlowThreshold.Should().BeNull();

        // Critical untouched.
        overridden.CriticalThreshold.Should().Be(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public void With_CriticalThresholdNull_DisablesCriticalCheck()
    {
        var defaults = new HandlerOptions();

        var overridden = defaults with { CriticalThreshold = null };

        overridden.CriticalThreshold.Should().BeNull();

        // Slow untouched.
        overridden.SlowThreshold.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void With_BothThresholdsNull_DisablesBoth()
    {
        var defaults = new HandlerOptions();

        var overridden = defaults with { SlowThreshold = null, CriticalThreshold = null };

        overridden.SlowThreshold.Should().BeNull();
        overridden.CriticalThreshold.Should().BeNull();
    }

    [Fact]
    public void With_HigherSlowThreshold_OverridesDefault()
    {
        var defaults = new HandlerOptions();

        var overridden = defaults with { SlowThreshold = TimeSpan.FromSeconds(2) };

        overridden.SlowThreshold.Should().Be(TimeSpan.FromSeconds(2));
    }

    // ----------------------------------------------------------------------
    // ScopeRequirement — null disables check; populated exposes Match + Scopes
    // ----------------------------------------------------------------------

    [Fact]
    public void With_ScopeRequirementNull_DisablesCheck()
    {
        var defaults = new HandlerOptions();

        var overridden = defaults with { ScopeRequirement = null };

        overridden.ScopeRequirement.Should().BeNull();
    }

    [Fact]
    public void With_ScopeRequirementAnyMatch_ExposesMatchAndScopes()
    {
        IReadOnlySet<string> scopes = new HashSet<string>(["a", "b"]);
        var req = new ScopeRequirement(HandlerScopeMatch.Any, scopes);

        var options = new HandlerOptions { ScopeRequirement = req };

        options.ScopeRequirement.Should().NotBeNull();
        options.ScopeRequirement!.Match.Should().Be(HandlerScopeMatch.Any);
        options.ScopeRequirement.Scopes.Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public void With_ScopeRequirementAllMatch_ExposesMatchAndScopes()
    {
        IReadOnlySet<string> scopes = new HashSet<string>(["read", "write"]);
        var req = new ScopeRequirement(HandlerScopeMatch.All, scopes);

        var options = new HandlerOptions { ScopeRequirement = req };

        options.ScopeRequirement!.Match.Should().Be(HandlerScopeMatch.All);
        options.ScopeRequirement.Scopes.Should().BeEquivalentTo(["read", "write"]);
    }

    [Fact]
    public void With_ScopeRequirementNullDisablesCheck()
    {
        // A null ScopeRequirement is the documented way to disable the per-
        // handler check. The HandlerOptions record accepts null; the pipeline
        // guard short-circuits when ScopeRequirement is null.
        var options = new HandlerOptions { ScopeRequirement = null };

        options.ScopeRequirement.Should().BeNull();
    }

    [Fact]
    public void With_ScopeRequirementEmptyScopes_ThrowsAtConstruction()
    {
        // Adversarial: empty Scopes is now illegal at construction (F5 guard).
        // The pipeline guard `is { Scopes.Count: > 0 }` remains as defense-in-depth,
        // but the constructor guard surfaces the misconfiguration at compose time.
        IReadOnlySet<string> empty_set = new HashSet<string>();

        var act = () => new ScopeRequirement(HandlerScopeMatch.Any, empty_set);

        act.Should().Throw<ArgumentException>();
    }

    // ----------------------------------------------------------------------
    // Reflection name-pins for HandlerOptions.ScopeRequirement — the property
    // name is load-bearing: BaseHandler pattern-matches against it and
    // test/doc code references it symbolically. Pin via reflection so a rename
    // trips an obvious test failure. Also pin that the OLD name (RequiredScopes)
    // is absent — guards against accidental reintroduction.
    // ----------------------------------------------------------------------

    [Fact]
    public void HandlerOptions_ScopeRequirementPropertyName_IsPinned()
    {
        var prop = typeof(HandlerOptions).GetProperty(nameof(HandlerOptions.ScopeRequirement));

        prop.Should().NotBeNull();
        prop.Name.Should().Be("ScopeRequirement");
    }

    [Fact]
    public void HandlerOptions_RequiredScopes_DoesNotExist()
    {
        // The old property name was "RequiredScopes"; it was replaced by
        // "ScopeRequirement" when explicit match-mode support was added. Pin
        // its absence so it cannot be silently reintroduced.
        var prop = typeof(HandlerOptions).GetProperty("RequiredScopes");

        prop.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Equality — record value-semantics. Two HandlerOptions with the same
    // property values must be equal; one differing property breaks equality.
    // ----------------------------------------------------------------------

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        var left = new HandlerOptions();
        var right = new HandlerOptions();

        left.Should().Be(right);
    }

    [Fact]
    public void Equals_DifferentLogInput_ReturnsFalse()
    {
        var left = new HandlerOptions { LogInput = true };
        var right = new HandlerOptions { LogInput = false };

        left.Should().NotBe(right);
    }
}
