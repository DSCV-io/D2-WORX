// -----------------------------------------------------------------------
// <copyright file="MutableForwardedJwtAccessorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Forwarding;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

/// <summary>
/// Unit tests for <see cref="MutableForwardedJwtAccessor"/> — the request-scoped
/// holder for the inbound forwarded JWT.
/// </summary>
/// <remarks>
/// The holder is POPULATED by the inbound auth surface. These tests prove the
/// holder STANDALONE via a populate-then-read-back cycle: the test acts as the
/// consumer, calling <see cref="IForwardedJwtAccessor.Current"/> +
/// <see cref="ForwardedJwt.RevealForForwarding"/> directly.
/// </remarks>
public sealed class MutableForwardedJwtAccessorTests
{
    private const string _JWT_A = "header.payloadA.signatureA";
    private const string _JWT_B = "header.payloadB.signatureB";

    [Fact]
    public void Current_FreshHolder_IsNull()
    {
        var holder = new MutableForwardedJwtAccessor();

        holder.Current.Should().BeNull();
    }

    [Fact]
    public void Capture_ThenCurrent_HoldsTheToken_RoundTripVerbatim()
    {
        var holder = new MutableForwardedJwtAccessor();

        holder.Capture(_JWT_A);

        holder.Current.Should().NotBeNull();
        holder.Current!.Value.HasValue.Should().BeTrue();
        holder.Current!.Value.RevealForForwarding().Should().Be(_JWT_A);
    }

    [Fact]
    public void Capture_Twice_LastWriteWins()
    {
        // A request is validated once, so a second capture is structurally
        // impossible in the real pipeline; the documented semantic is
        // last-write-wins. This test locks that choice.
        var holder = new MutableForwardedJwtAccessor();

        holder.Capture(_JWT_A);
        holder.Capture(_JWT_B);

        holder.Current!.Value.RevealForForwarding().Should().Be(_JWT_B);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Capture_BlankInput_NoOps_HolderStaysNull(string? blank)
    {
        // A blank "credential" is never valid to hold — Create() validates it
        // away and the holder never stores a failed wrapper.
        var holder = new MutableForwardedJwtAccessor();

        holder.Capture(blank!);

        holder.Current.Should().BeNull();
    }

    [Fact]
    public void Capture_BlankAfterValidCapture_DoesNotClobberWithFailure()
    {
        // A subsequent blank capture must not wipe an already-held valid token
        // (Create() fails on blank → the no-op leaves the prior value intact).
        var holder = new MutableForwardedJwtAccessor();

        holder.Capture(_JWT_A);
        holder.Capture("   ");

        holder.Current!.Value.RevealForForwarding().Should().Be(_JWT_A);
    }

    [Fact]
    public void TwoInstances_AreIndependent_NoSharedState()
    {
        // Distinct holder instances (what distinct request scopes yield) share
        // no state — capturing in one leaves the other empty. Defends the
        // no-static-state property at the type level (the DI-lifetime proof
        // lives in the resolution tests).
        var first = new MutableForwardedJwtAccessor();
        var second = new MutableForwardedJwtAccessor();

        first.Capture(_JWT_A);

        first.Current.Should().NotBeNull();
        second.Current.Should().BeNull();
    }
}
