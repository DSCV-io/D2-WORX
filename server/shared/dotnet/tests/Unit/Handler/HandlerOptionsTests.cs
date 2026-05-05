// -----------------------------------------------------------------------
// <copyright file="HandlerOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Handler;

using System;
using System.Collections.Generic;
using AwesomeAssertions;
using D2.Shared.Handler.Abstractions;
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
    public void DefaultCtor_RequiredScopes_IsNull()
    {
        // Adversarial: a non-null empty set vs null mean different things —
        // null disables the check entirely; empty set runs the loop with
        // zero iterations (still no rejection but explicit "checked").
        var options = new HandlerOptions();

        options.RequiredScopes.Should().BeNull();
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
        overridden.RequiredScopes.Should().BeNull();
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
    // RequiredScopes — null vs empty distinction is semantic
    // ----------------------------------------------------------------------

    [Fact]
    public void With_RequiredScopesNull_DefaultRemains()
    {
        var defaults = new HandlerOptions();

        var overridden = defaults with { RequiredScopes = null };

        overridden.RequiredScopes.Should().BeNull();
    }

    [Fact]
    public void With_RequiredScopesEmptySet_AcceptedAsDistinctFromNull()
    {
        // Adversarial: empty set is semantically different from null —
        // null disables the foreach entirely, empty runs zero iterations.
        // Both result in "no rejection" today, but the handler pipeline
        // checks `is { Count: > 0 }` so it MUST treat empty as "skip" too.
        // Verify the option-record itself preserves the distinction.
        IReadOnlySet<string> empty_set = new HashSet<string>();

        var overridden = new HandlerOptions { RequiredScopes = empty_set };

        overridden.RequiredScopes.Should().NotBeNull();
        overridden.RequiredScopes.Should().BeEmpty();
        overridden.RequiredScopes.Should().BeSameAs(empty_set);
    }

    [Fact]
    public void With_RequiredScopesPopulated_ExposesEntries()
    {
        IReadOnlySet<string> scopes = new HashSet<string>(["a", "b", "c"]);

        var options = new HandlerOptions { RequiredScopes = scopes };

        options.RequiredScopes.Should().NotBeNull();
        options.RequiredScopes.Should().BeEquivalentTo(["a", "b", "c"]);
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
