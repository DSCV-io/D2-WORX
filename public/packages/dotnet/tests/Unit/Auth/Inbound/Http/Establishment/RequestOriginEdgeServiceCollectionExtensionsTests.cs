// -----------------------------------------------------------------------
// <copyright file="RequestOriginEdgeServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Http.Establishment;

using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Http;
using D2.Shared.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// DI-resolution matrix for <c>AddD2RequestOriginEdge()</c>: it binds the workload
/// identity options + the clock the Edge-inbound middleware depends on, and validates the
/// required self-id at resolution.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RequestOriginEdgeServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2RequestOriginEdge_NullServices_Throws()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2RequestOriginEdge();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2RequestOriginEdge_RegistersOptionsAndClock()
    {
        var services = new ServiceCollection();
        services.AddD2RequestOriginEdge(o => o.ServiceId = "edge");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<D2WorkloadIdentityOptions>>().Value.ServiceId
            .Should().Be("edge");
        provider.GetRequiredService<IClock>().Should().BeOfType<SystemClock>();
    }

    [Fact]
    public void AddD2RequestOriginEdge_DoesNotOverrideAnExistingClock()
    {
        var services = new ServiceCollection();
        var testClock = new TestClock(NodaTime.Instant.FromUtc(2026, 1, 1, 0, 0, 0));
        services.AddSingleton<IClock>(testClock);
        services.AddD2RequestOriginEdge(o => o.ServiceId = "edge");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IClock>().Should().BeSameAs(
            testClock, "the host's own clock binding is preserved (TryAdd)");
    }

    [Fact]
    public void AddD2RequestOriginEdge_BlankServiceId_FailsValidationOnResolve()
    {
        var services = new ServiceCollection();
        services.AddD2RequestOriginEdge();

        var act = () =>
        {
            using var provider = services.BuildServiceProvider();
            _ = provider.GetRequiredService<IOptions<D2WorkloadIdentityOptions>>().Value;
        };

        act.Should().Throw<OptionsValidationException>();
    }
}
