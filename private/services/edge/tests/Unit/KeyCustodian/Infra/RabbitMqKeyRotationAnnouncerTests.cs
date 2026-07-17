// -----------------------------------------------------------------------
// <copyright file="RabbitMqKeyRotationAnnouncerTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.Infra;

using DcsvIo.D2.Auth.Events;
using DcsvIo.D2.Messaging;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Messaging.RabbitMq;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Tests for <see cref="RabbitMqKeyRotationAnnouncer"/>: the four announce
/// permutations map to the right wire payload, a publish failure returns a failure
/// result (never throws — fire-and-log), a thrown bus exception is caught and
/// downgraded, cancellation propagates, and the payload carries public identifiers
/// only (no key material, no compromise reason).
/// </summary>
public sealed class RabbitMqKeyRotationAnnouncerTests
{
    public static TheoryData<bool, KeyStatus> AnnouncePermutations() => new()
    {
        { false, KeyStatus.Active },
        { true, KeyStatus.Active },
        { false, KeyStatus.Retiring },
        { true, KeyStatus.Compromised },
    };

    [Theory]
    [MemberData(nameof(AnnouncePermutations))]
    public async Task AnnounceAsync_PublishesKeyRotatedEvent_WithMappedFields(
        bool urgent, KeyStatus status)
    {
        var bus = new RecordingMessageBus();
        var announcer = BuildAnnouncer(bus);
        var domain = KeyDomain.JwksSigning;
        var kid = Kid.FromTrusted("kid-123");

        var result = await announcer.AnnounceAsync(domain, kid, status, urgent);

        result.Success.Should().BeTrue();
        bus.Published.Should().HaveCount(1);
        var ev = bus.Published[0].Should().BeOfType<KeyRotatedEvent>().Subject;
        ev.Domain.Should().Be(domain.Value);
        ev.Kid.Should().Be("kid-123");
        ev.NewStatus.Should().Be(status.ToString());
        ev.Urgent.Should().Be(urgent);
    }

    [Fact]
    public async Task AnnounceAsync_PublishFailure_ReturnsFailure_DoesNotThrow()
    {
        var bus = new RecordingMessageBus(D2Result.ServiceUnavailable());
        var announcer = BuildAnnouncer(bus);

        var result = await announcer.AnnounceAsync(
            KeyDomain.Cookie, Kid.FromTrusted("k"), KeyStatus.Active, urgent: false);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task AnnounceAsync_BusThrows_CaughtAndDowngradedToFailure()
    {
        var bus = new ThrowingMessageBus();
        var announcer = BuildAnnouncer(bus);

        // Fire-and-log: a broker fault must NOT bubble out of the post-commit announce.
        var result = await announcer.AnnounceAsync(
            KeyDomain.ClientSecret, Kid.FromTrusted("k"), KeyStatus.Active, urgent: true);

        result.Success.Should().BeFalse();
        result.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task AnnounceAsync_Canceled_PropagatesCancellation()
    {
        var bus = new RecordingMessageBus();
        var announcer = BuildAnnouncer(bus);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var token = cts.Token;

        var act = async () => await announcer.AnnounceAsync(
            KeyDomain.JwksSigning, Kid.FromTrusted("k"), KeyStatus.Active, false, token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task AnnounceAsync_Payload_CarriesOnlyPublicIdentifiers()
    {
        // §3.4 evidence: the wire event exposes exactly four public fields —
        // no key material, no compromise reason.
        var bus = new RecordingMessageBus();
        var announcer = BuildAnnouncer(bus);

        await announcer.AnnounceAsync(
            KeyDomain.JwksSigning, Kid.FromTrusted("k"), KeyStatus.Compromised, urgent: true);

        var props = typeof(KeyRotatedEvent).GetProperties().Select(p => p.Name).ToList();
        props.Should().BeEquivalentTo(["Domain", "Kid", "NewStatus", "Urgent"]);
    }

    private static RabbitMqKeyRotationAnnouncer BuildAnnouncer(IMessageBus bus) =>
        new(bus, NullLogger<RabbitMqKeyRotationAnnouncer>.Instance);

    private sealed class RecordingMessageBus(D2Result? result = null) : IMessageBus
    {
        private readonly D2Result r_result = result ?? D2Result.Ok();

        public List<object> Published { get; } = [];

        public ValueTask<D2Result> PublishAsync<TMessage>(
            TMessage message, PublisherOptions? options = null, CancellationToken ct = default)
            where TMessage : class
        {
            ct.ThrowIfCancellationRequested();
            Published.Add(message);
            return ValueTask.FromResult(r_result);
        }

        public Task WaitForReadyAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ThrowingMessageBus : IMessageBus
    {
        public ValueTask<D2Result> PublishAsync<TMessage>(
            TMessage message, PublisherOptions? options = null, CancellationToken ct = default)
            where TMessage : class =>
            throw new InvalidOperationException("broker down");

        public Task WaitForReadyAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
