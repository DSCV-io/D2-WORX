// -----------------------------------------------------------------------
// <copyright file="SystemRequestContextBootstrapTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Context.Establishment;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Time;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Xunit;

/// <summary>
/// Unit matrix for the System worker establishment bootstrap: it establishes
/// <see cref="RequestOrigin.System"/> + the host identity + a single System call-path
/// entry on the scope's <see cref="MutableRequestContext"/>, fails loudly when the scope
/// has not registered one, and guards its required args.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SystemRequestContextBootstrapTests
{
    private static readonly Instant sr_now = Instant.FromUtc(2026, 6, 30, 12, 0, 0);

    [Fact]
    public void EstablishSystemContext_OnScopeWithMutableContext_EstablishesSystemPlane()
    {
        using var provider = BuildScope();
        var clock = new TestClock(sr_now);

        provider.EstablishSystemContext("key-custodian", clock);

        var ctx = provider.GetRequiredService<MutableRequestContext>();
        ctx.Origin.Should().Be(RequestOrigin.System);
        ctx.ImmediateCaller.Should().Be("key-custodian");
        ctx.CallPath.Should().ContainSingle();
        ctx.CallPath[0].Id.Should().Be("key-custodian");
        ctx.CallPath[0].Kind.Should().Be(CallPathKind.System);
        ctx.CallPath[0].Timestamp.Should().Be(sr_now.ToDateTimeOffset());
    }

    [Fact]
    public void EstablishSystemContext_GrantsNoSigningAuthority_SystemIsNotCrossProcessOrModule()
    {
        // A System worker is least-privilege: its established origin is System, never a
        // plane the signing authority would grant against (CrossProcessHop / InProcessModule).
        using var provider = BuildScope();

        provider.EstablishSystemContext("key-custodian", new TestClock(sr_now));

        var ctx = provider.GetRequiredService<MutableRequestContext>();
        ctx.Origin.Should().NotBe(RequestOrigin.CrossProcessHop);
        ctx.Origin.Should().NotBe(RequestOrigin.InProcessModule);
    }

    [Fact]
    public void EstablishSystemContext_ScopeMissingMutableContext_Throws()
    {
        var act = () =>
        {
            using var provider = new ServiceCollection().BuildServiceProvider();
            provider.EstablishSystemContext("key-custodian", new TestClock(sr_now));
        };

        act.Should().Throw<InvalidOperationException>(
            "the worker scope must register a MutableRequestContext to populate");
    }

    [Fact]
    public void EstablishSystemContext_NullClock_Throws()
    {
        var act = () =>
        {
            using var provider = BuildScope();
            provider.EstablishSystemContext("key-custodian", null!);
        };

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EstablishSystemContext_BlankHostId_Throws(string? hostId)
    {
        var act = () =>
        {
            using var provider = BuildScope();
            provider.EstablishSystemContext(hostId!, new TestClock(sr_now));
        };

        act.Should().Throw<ArgumentException>();
    }

    private static ServiceProvider BuildScope()
    {
        var services = new ServiceCollection();
        services.AddScoped<MutableRequestContext>();

        return services.BuildServiceProvider();
    }
}
