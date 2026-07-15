// -----------------------------------------------------------------------
// <copyright file="SseDispatcherTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecSse;

using System.Net;
using DcsvIo.D2.Private.Edge.Tests.TypeSpecSse.Generated;
using DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecSse.Fixtures;
using DcsvIo.D2.Result;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Behavioral tests for the TypeSpec-emitted server-push dispatchers
/// (<see cref="OrderShippedFixtureDispatcher"/> / <see cref="SessionExpiringFixtureDispatcher"/>)
/// against the faithful <see cref="FakeSseEmitSink"/>.
///
/// Each test exercises one observable behavior of a dispatcher:
///   - the User-channel dispatcher addresses the <c>User</c> channel class +
///     forwards the targetId, the op-name event-type, and the typed payload;
///   - the Session-channel dispatcher addresses the <c>Session</c> channel class
///     (the baked-in pushTarget arm is non-vacuous vs the User arm);
///   - a sink failure rides through verbatim (never swallowed to Ok, §9.20);
///   - an adversarial (empty / whitespace) targetId is forwarded as-is — the
///     dispatcher is a thin forwarder; the sink/gateway validate the recipient;
///   - the generated DI extension resolves each dispatcher AS its impl (§1.3 —
///     descriptor presence ≠ resolvability).
/// </summary>
public sealed class SseDispatcherTests
{
    // ── User-channel dispatch: class + id + eventType + payload (non-vacuous) ──

    [Fact]
    public async Task OrderShippedFixtureDispatcher_AddressesUserChannel_ForwardsAllFields()
    {
        var sink = new FakeSseEmitSink();
        var dispatcher = new OrderShippedFixtureDispatcher(sink);
        var payload = new OrderShippedFixtureOutput(
            "order-42",
            DateTimeOffset.UnixEpoch,
            [new OrderFixtureLine("sku-1", 3)]);

        var result = await dispatcher.DispatchAsync("user-7", payload);

        result.Success.Should().BeTrue();
        sink.CallCount.Should().Be(1);
        sink.LastTarget.Class.Should().Be(D2GeneratedSseChannelClass.User);
        sink.LastTarget.Id.Should().Be("user-7");
        sink.LastEventType.Should().Be("orderShippedFixture");
        sink.LastPayload.Should().BeSameAs(payload);
    }

    // ── Session-channel dispatch: the Session arm is non-vacuous vs User ──

    [Fact]
    public async Task SessionExpiringFixtureDispatcher_AddressesSessionChannel_ForwardsAllFields()
    {
        var sink = new FakeSseEmitSink();
        var dispatcher = new SessionExpiringFixtureDispatcher(sink);
        var payload = new SessionExpiringFixtureOutput("session-9", DateTimeOffset.UnixEpoch);

        var result = await dispatcher.DispatchAsync("session-9", payload);

        result.Success.Should().BeTrue();
        sink.CallCount.Should().Be(1);
        sink.LastTarget.Class.Should().Be(D2GeneratedSseChannelClass.Session);
        sink.LastTarget.Id.Should().Be("session-9");
        sink.LastEventType.Should().Be("sessionExpiringFixture");
        sink.LastPayload.Should().BeSameAs(payload);
    }

    // ── Sink failure propagates verbatim — never swallowed to Ok (§9.20) ──

    [Fact]
    public async Task OrderShippedFixtureDispatcher_SinkFailure_PropagatesServiceUnavailable()
    {
        var sink = new FakeSseEmitSink(D2Result.ServiceUnavailable());
        var dispatcher = new OrderShippedFixtureDispatcher(sink);
        var payload = new OrderShippedFixtureOutput("order-1", DateTimeOffset.UnixEpoch, []);

        var result = await dispatcher.DispatchAsync("user-1", payload);

        // The dispatcher returns the sink's result verbatim — it must NOT mask a
        // failure as Ok (the branching-call return discipline).
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        sink.CallCount.Should().Be(1);
    }

    // ── Adversarial targetId: empty / whitespace forwarded as-is ──

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OrderShippedFixtureDispatcher_AdversarialTargetId_ForwardedUnchanged(
        string targetId)
    {
        var sink = new FakeSseEmitSink();
        var dispatcher = new OrderShippedFixtureDispatcher(sink);
        var payload = new OrderShippedFixtureOutput("order-1", DateTimeOffset.UnixEpoch, []);

        var result = await dispatcher.DispatchAsync(targetId, payload);

        // The dispatcher is a thin forwarder — it does not validate the recipient
        // id; the sink/gateway own that. The id rides through verbatim.
        result.Success.Should().BeTrue();
        sink.LastTarget.Id.Should().Be(targetId);
        sink.LastTarget.Class.Should().Be(D2GeneratedSseChannelClass.User);
    }

    // ── §1.3 DI resolution — each dispatcher resolves AS its impl ──

    [Fact]
    public void AddD2PushFixturesSseDispatchers_ResolvesEachDispatcherToConcreteType()
    {
        var services = new ServiceCollection();
        services.AddSingleton<D2GeneratedSseEmitSink>(new FakeSseEmitSink());

        services.AddD2PushFixturesSseDispatchers();

        using var sp = services.BuildServiceProvider();

        // Descriptor presence ≠ resolvability — resolve EVERY registered seam.
        var userDispatcher = sp.GetRequiredService<IOrderShippedFixtureDispatcher>();
        userDispatcher.Should().BeOfType<OrderShippedFixtureDispatcher>();

        var sessionDispatcher = sp.GetRequiredService<ISessionExpiringFixtureDispatcher>();
        sessionDispatcher.Should().BeOfType<SessionExpiringFixtureDispatcher>();
    }

    // ── DI lifetime is Transient — two resolutions yield distinct instances ──

    [Fact]
    public void AddD2PushFixturesSseDispatchers_RegistersDispatchersTransient()
    {
        var services = new ServiceCollection();
        services.AddSingleton<D2GeneratedSseEmitSink>(new FakeSseEmitSink());

        services.AddD2PushFixturesSseDispatchers();

        using var sp = services.BuildServiceProvider();

        var first = sp.GetRequiredService<IOrderShippedFixtureDispatcher>();
        var second = sp.GetRequiredService<IOrderShippedFixtureDispatcher>();
        first.Should().NotBeSameAs(second);
    }
}
