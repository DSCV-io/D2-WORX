// -----------------------------------------------------------------------
// <copyright file="InProcessModuleBoundaryTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Context.Establishment;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Tests.Unit.Handler;
using DcsvIo.D2.Time;
using NodaTime;
using Xunit;

/// <summary>
/// Unit matrix for the in-process-module establishment seam: it positively establishes
/// <see cref="RequestOrigin.InProcessModule"/> + the calling module id + a ModuleHop on
/// a mutable context, is a no-op on a read-only context, and guards its required args.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InProcessModuleBoundaryTests
{
    private static readonly Instant sr_now = Instant.FromUtc(2026, 6, 30, 12, 0, 0);

    [Fact]
    public void EstablishInProcessModule_OnMutableContext_SetsOriginCallerAndAppendsModuleHop()
    {
        var clock = new TestClock(sr_now);
        IRequestContext ctx = new MutableRequestContext
        {
            Origin = RequestOrigin.EdgeInbound,
            CallPath = [new CallPathEntry("edge", CallPathKind.Edge, sr_now.ToDateTimeOffset())],
        };

        ctx.EstablishInProcessModule("edge", "key-custodian", clock);

        ctx.Origin.Should().Be(RequestOrigin.InProcessModule);
        ctx.ImmediateCaller.Should().Be("edge");
        ctx.CallPath.Should().HaveCount(2);
        ctx.CallPath[^1].Id.Should().Be("key-custodian");
        ctx.CallPath[^1].Kind.Should().Be(CallPathKind.ModuleHop);
        ctx.CallPath[^1].Timestamp.Should().Be(sr_now.ToDateTimeOffset());
    }

    [Fact]
    public void EstablishInProcessModule_OnReadOnlyContext_IsNoOp()
    {
        var clock = new TestClock(sr_now);
        IRequestContext ctx = new TestRequestContext { Origin = RequestOrigin.Unestablished };

        var act = () => ctx.EstablishInProcessModule("edge", "key-custodian", clock);

        act.Should().NotThrow("a non-mutable context is left untouched, not mutated");
        ctx.Origin.Should().Be(RequestOrigin.Unestablished);
    }

    [Fact]
    public void EstablishInProcessModule_NullClock_Throws()
    {
        IRequestContext ctx = new MutableRequestContext();

        var act = () => ctx.EstablishInProcessModule("edge", "kc", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(null, "kc")]
    [InlineData("", "kc")]
    [InlineData("edge", null)]
    [InlineData("edge", "  ")]
    public void EstablishInProcessModule_BlankIds_Throw(string? calling, string? target)
    {
        var clock = new TestClock(sr_now);
        IRequestContext ctx = new MutableRequestContext();

        var act = () => ctx.EstablishInProcessModule(calling!, target!, clock);

        act.Should().Throw<ArgumentException>();
    }
}
