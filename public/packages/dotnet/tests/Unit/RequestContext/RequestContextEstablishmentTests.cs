// -----------------------------------------------------------------------
// <copyright file="RequestContextEstablishmentTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.RequestContext;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// The request-context establishment surface: the three folded-in <see cref="IRequestContext"/>
/// fields (<c>Origin</c> / <c>ImmediateCaller</c> / <c>CallPath</c>), their
/// fail-closed mutable defaults, the structural anti-spoofing invariant (the
/// authority-grade non-propagated fields NEVER reach <see cref="PropagatedContext"/>),
/// and the hand-authored vocabulary trio (<see cref="RequestOrigin"/> /
/// <see cref="CallPathKind"/> / <see cref="CallPathEntry"/>).
/// </summary>
public sealed class RequestContextEstablishmentTests
{
    [Fact]
    public void MutableRequestContext_Defaults_AreFailClosed()
    {
        var ctx = new MutableRequestContext();

        ctx.Origin.Should().Be(RequestOrigin.Unestablished);
        ctx.ImmediateCaller.Should().BeNull();
        ctx.CallPath.Should().NotBeNull();
        ctx.CallPath.Should().BeEmpty();
    }

    [Fact]
    public void MutableRequestContext_ResolvedFromDi_HasFailClosedDefaults()
    {
        // DI-resolution proof: the scoped context resolves and its
        // establishment fields default to the fail-closed values — an
        // unestablished origin is the scoped default, so a capability authority
        // can never see an assumed plane.
        using var provider = new ServiceCollection()
            .AddScoped<MutableRequestContext>()
            .BuildServiceProvider();
        using var scope = provider.CreateScope();

        var ctx = scope.ServiceProvider.GetRequiredService<MutableRequestContext>();

        ctx.Origin.Should().Be(RequestOrigin.Unestablished);
        ctx.ImmediateCaller.Should().BeNull();
        ctx.CallPath.Should().BeEmpty();
    }

    [Fact]
    public void IRequestContext_ExposesEstablishmentFields_WithExpectedTypes()
    {
        var iface = typeof(IRequestContext);

        iface.GetProperty(nameof(IRequestContext.Origin))!
            .PropertyType.Should().Be<RequestOrigin>();
        iface.GetProperty(nameof(IRequestContext.ImmediateCaller))!
            .PropertyType.Should().Be<string>();
        iface.GetProperty(nameof(IRequestContext.CallPath))!
            .PropertyType.Should().Be<System.Collections.Generic.IReadOnlyList<CallPathEntry>>();
    }

    [Fact]
    public void PropagatedContext_ExcludesAuthorityGradeEstablishmentFields()
    {
        // Anti-spoofing invariant #1: Origin + ImmediateCaller are derived FRESH
        // by every boundary from unforgeable transport facts and are NEVER
        // propagated — so they MUST be structurally absent from the wire record.
        // CallPath (telemetry) IS propagated and therefore present.
        var record = typeof(PropagatedContext);

        record.GetProperty("Origin").Should().BeNull(
            "Origin is authority-grade + non-propagated; a wire Origin would be a spoofing surface");
        record.GetProperty("ImmediateCaller").Should().BeNull(
            "ImmediateCaller is derived from the mTLS peer cert each hop; it is never propagated");
        record.GetProperty("CallPath").Should().NotBeNull(
            "CallPath is propagated telemetry and must appear on the wire record");
    }

    [Fact]
    public void RequestOrigin_Unestablished_IsTheZeroDefault()
    {
        default(RequestOrigin).Should().Be(RequestOrigin.Unestablished);
        ((int)RequestOrigin.Unestablished).Should().Be(0);
        ((int)RequestOrigin.EdgeInbound).Should().Be(1);
        ((int)RequestOrigin.CrossProcessHop).Should().Be(2);
        ((int)RequestOrigin.InProcessModule).Should().Be(3);
        ((int)RequestOrigin.System).Should().Be(4);
    }

    [Fact]
    public void CallPathKind_Members_HaveExpectedOrdinals()
    {
        ((int)CallPathKind.Edge).Should().Be(0);
        ((int)CallPathKind.WorkloadHop).Should().Be(1);
        ((int)CallPathKind.ModuleHop).Should().Be(2);
        ((int)CallPathKind.System).Should().Be(3);
    }

    [Fact]
    public void CallPathEntry_Construction_ExposesComponents()
    {
        var ts = new DateTimeOffset(2026, 5, 27, 10, 0, 0, TimeSpan.Zero);

        var entry = new CallPathEntry("key-custodian", CallPathKind.WorkloadHop, ts);

        entry.Id.Should().Be("key-custodian");
        entry.Kind.Should().Be(CallPathKind.WorkloadHop);
        entry.Timestamp.Should().Be(ts);
    }

    [Fact]
    public void CallPathEntry_ValueEquality_ComparesByComponents()
    {
        var ts = new DateTimeOffset(2026, 5, 27, 10, 0, 0, TimeSpan.Zero);

        var a = new CallPathEntry("edge", CallPathKind.Edge, ts);
        var b = new CallPathEntry("edge", CallPathKind.Edge, ts);
        var different = new CallPathEntry("edge", CallPathKind.WorkloadHop, ts);

        a.Should().Be(b);
        a.Should().NotBe(different);
    }
}
