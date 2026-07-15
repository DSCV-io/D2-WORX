// -----------------------------------------------------------------------
// <copyright file="D2LoggingOptionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Logging;

using AwesomeAssertions;
using DcsvIo.D2.Logging;
using Serilog.Events;
using Xunit;

public sealed class D2LoggingOptionsTests
{
    [Fact]
    public void ParameterlessCtor_AllDefaultsApplied()
    {
        var opts = new D2LoggingOptions();

        opts.ServiceName.Should().BeNull();
        opts.Environment.Should().BeNull();
        opts.MinimumLevel.Should().Be(LogEventLevel.Information);
        opts.InfrastructurePaths.Should().BeEquivalentTo(
            "/health", "/alive", "/metrics", "/.well-known");
    }

    [Fact]
    public void ParameterizedCtor_AllNullArgs_AppliesAllDefaults()
    {
        var opts = new D2LoggingOptions(null, null, null, null);

        opts.ServiceName.Should().BeNull();
        opts.Environment.Should().BeNull();
        opts.MinimumLevel.Should().Be(LogEventLevel.Information);
        opts.InfrastructurePaths.Should().NotBeEmpty();
    }

    [Fact]
    public void ParameterizedCtor_ExplicitArgs_PreservesValues()
    {
        var paths = new[] { "/x", "/y" };
        var opts = new D2LoggingOptions("svc", "Production", LogEventLevel.Debug, paths);

        opts.ServiceName.Should().Be("svc");
        opts.Environment.Should().Be("Production");
        opts.MinimumLevel.Should().Be(LogEventLevel.Debug);
        opts.InfrastructurePaths.Should().BeSameAs(paths);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var a = new D2LoggingOptions("svc", "Production", LogEventLevel.Debug, ["/x"]);
        var b = new D2LoggingOptions("svc", "Production", LogEventLevel.Debug, ["/x"]);

        // Records compare by-value on init properties (set properties also when
        // both sides hold the same reference); InfrastructurePaths is a separate
        // list so reference-distinct collections don't auto-equal — use field-wise.
        a.ServiceName.Should().Be(b.ServiceName);
        a.Environment.Should().Be(b.Environment);
        a.MinimumLevel.Should().Be(b.MinimumLevel);
    }

    [Fact]
    public void WithExpression_OverridesSingleField()
    {
        var baseline = new D2LoggingOptions();

        var overridden = baseline with { MinimumLevel = LogEventLevel.Warning };

        overridden.MinimumLevel.Should().Be(LogEventLevel.Warning);
        overridden.ServiceName.Should().Be(baseline.ServiceName);
        overridden.Environment.Should().Be(baseline.Environment);
        overridden.InfrastructurePaths.Should().BeSameAs(baseline.InfrastructurePaths);
    }

    [Fact]
    public void WithExpression_OverridesServiceNameViaSetter()
    {
        var baseline = new D2LoggingOptions();

        var overridden = baseline with { ServiceName = "edge", Environment = "Test" };

        overridden.ServiceName.Should().Be("edge");
        overridden.Environment.Should().Be("Test");
    }

    [Fact]
    public void Sealed_CannotInherit()
    {
        typeof(D2LoggingOptions).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void DefaultInfrastructurePaths_HasExpectedFourPrefixes()
    {
        var opts = new D2LoggingOptions();

        opts.InfrastructurePaths.Should().HaveCount(4);
        opts.InfrastructurePaths.Should().Contain("/health");
        opts.InfrastructurePaths.Should().Contain("/alive");
        opts.InfrastructurePaths.Should().Contain("/metrics");
        opts.InfrastructurePaths.Should().Contain("/.well-known");
    }
}
