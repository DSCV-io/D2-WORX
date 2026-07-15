// -----------------------------------------------------------------------
// <copyright file="SealingMetricsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Client.Sealing;

using System.Diagnostics.Metrics;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.Client.Keyring;
using D2.Edge.KeyCustodian.Client.Sealing;
using D2.Shared.Encryption;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Observability pins for <see cref="SealingMetrics"/> — the sealed sibling of
/// <c>KeyringMetricsTests</c>. The fetch, rotation-hot-swap, and refresh-failure counters
/// fire through the real sealer / opener path with the closed-set named-constant tag keys
/// (rules.md §21.11); the meter + instrument wire names are pinned per-value so a rename
/// fails a test rather than silently breaking a dashboard.
/// </summary>
public sealed class SealingMetricsTests
{
    // A file-unique fixture seal domain isolates these counter pins from every other test
    // in the process: the sealing counters are static (one meter per process), so a test
    // driving swaps/refreshes/fetches for the SHARED fixture seal domain must not pollute
    // this capture, which selects measurements by this file's own seal:<serviceId> tag.
    private const string _METRICS_SERVICE_ID = "seal-fixture-metrics-svc";
    private const string _METRICS_DOMAIN = "seal:seal-fixture-metrics-svc";

    private static readonly TimeSpan sr_tiny = TimeSpan.FromMilliseconds(1);

    [Fact]
    public async Task LazyFetchFailure_IncrementsFetchCounter_WithClosedSetTags()
    {
        using var capture = new MeterCapture("d2.sealing.fetches", _METRICS_DOMAIN);
        var client = FakeSealingClient.PublicAlwaysFails(
            D2Result<RecipientPublicKeyring>.ServiceUnavailable());
        await using var sealer = NewSealer(client, NewChannel());

        // ReSharper disable once AccessToDisposedClosure -- the throwing seal runs
        // synchronously via Should().Throw() before the await using disposes the sealer.
        var act = () => sealer.Seal("x"u8);
        act.Should().Throw<InvalidOperationException>();

        capture.Total.Should().Be(1);
        capture.LastTags[SealingMetrics.Tags.TAG_DOMAIN].Should().Be(_METRICS_DOMAIN);
        capture.LastTags[SealingMetrics.Tags.TAG_RESULT].Should().Be(SealingMetrics.Tags.FAILURE);
    }

    [Fact]
    public async Task RotationSwap_IncrementsHotSwapCounter()
    {
        using var capture = new MeterCapture("d2.sealing.rotation_hot_swaps", _METRICS_DOMAIN);
        var channel = NewChannel();
        var client = new FakeSealingClient(privateResponder:
            i => D2Result<RecipientPrivateKeyring>.Ok(
                i == 1
                    ? SealingTestFixtures.SingleKidPrivateKeyring(_METRICS_SERVICE_ID)
                    : SealingTestFixtures.RotatedPrivateKeyring(_METRICS_SERVICE_ID)));
        await using var opener = NewOpener(client, channel, maxAttempts: 3);

        await channel.DispatchAsync(_METRICS_DOMAIN, CancellationToken.None);

        capture.Total.Should().Be(1);
        capture.LastTags[SealingMetrics.Tags.TAG_DOMAIN].Should().Be(_METRICS_DOMAIN);
    }

    [Fact]
    public async Task RefreshFailure_IncrementsFailureCounter_WithClosedSetTags()
    {
        using var capture = new MeterCapture("d2.sealing.refresh_failures", _METRICS_DOMAIN);
        var channel = NewChannel();
        var client = new FakeSealingClient(privateResponder: i => i == 1
            ? D2Result<RecipientPrivateKeyring>.Ok(
                SealingTestFixtures.SingleKidPrivateKeyring(_METRICS_SERVICE_ID))
            : D2Result<RecipientPrivateKeyring>.ServiceUnavailable());
        await using var opener = NewOpener(client, channel, maxAttempts: 2);

        await channel.DispatchAsync(_METRICS_DOMAIN, CancellationToken.None);

        capture.Total.Should().Be(1);
        capture.LastTags.Should().ContainKey(SealingMetrics.Tags.TAG_DOMAIN);
        capture.LastTags.Should().ContainKey(SealingMetrics.Tags.TAG_ERROR_CODE);
    }

    [Fact]
    public void MeterAndInstrumentNames_WireLiterals_ArePinned()
    {
        // Per-VALUE pins: a wire-name rename must fail a test, never pass silently. The
        // sealing counters share the keyring meter (D2.Edge.KeyCustodian.Client).
        SealingMetrics.SR_SealKeyringFetches.Meter.Name
            .Should().Be("D2.Edge.KeyCustodian.Client");
        SealingMetrics.SR_SealKeyringFetches.Meter.Name.Should().Be(KeyringMetrics.METER_NAME);

        SealingMetrics.SR_SealKeyringFetches.Name.Should().Be("d2.sealing.fetches");
        SealingMetrics.SR_RefreshFailures.Name.Should().Be("d2.sealing.refresh_failures");
        SealingMetrics.SR_RotationHotSwaps.Name.Should().Be("d2.sealing.rotation_hot_swaps");
    }

    [Fact]
    public void TagKeysAndClosedSetValues_WireLiterals_ArePinned()
    {
        SealingMetrics.Tags.TAG_DOMAIN.Should().Be("domain");
        SealingMetrics.Tags.TAG_RESULT.Should().Be("result");
        SealingMetrics.Tags.TAG_ERROR_CODE.Should().Be("errorCode");
        SealingMetrics.Tags.SUCCESS.Should().Be("success");
        SealingMetrics.Tags.FAILURE.Should().Be("failure");
        SealingMetrics.Tags.NONE.Should().Be("<none>");
    }

    private static RabbitMqRotationEventChannel NewChannel()
        => new(NullLogger<RabbitMqRotationEventChannel>.Instance);

    private static KeyringBackedPayloadOpener NewOpener(
        FakeSealingClient client, RabbitMqRotationEventChannel channel, int maxAttempts)
        => KeyringBackedPayloadOpener.CreateForTesting(
            _METRICS_SERVICE_ID,
            client,
            channel,
            NullLogger<KeyringBackedPayloadOpener>.Instance,
            sr_tiny,
            maxAttempts,
            sr_tiny);

    private static KeyringBackedPayloadSealer NewSealer(
        FakeSealingClient client, RabbitMqRotationEventChannel channel)
        => KeyringBackedPayloadSealer.CreateForTesting(
            _METRICS_SERVICE_ID,
            client,
            channel,
            NullLogger<KeyringBackedPayloadSealer>.Instance,
            maxRefreshAttempts: 1,
            sr_tiny,
            sr_tiny);

    /// <summary>
    /// Captures long-counter measurements for a single instrument, counting only
    /// measurements tagged with the given seal domain (per-test isolation on the
    /// process-global meter).
    /// </summary>
    private sealed class MeterCapture : IDisposable
    {
        private readonly MeterListener r_listener = new();
        private long _total;

        public MeterCapture(string instrumentName, string domain)
        {
            r_listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == KeyringMetrics.METER_NAME
                    && instrument.Name == instrumentName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            r_listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
            {
                var snapshot = new Dictionary<string, object?>(StringComparer.Ordinal);

                foreach (var tag in tags)
                    snapshot[tag.Key] = tag.Value;

                if (!Equals(snapshot.GetValueOrDefault(SealingMetrics.Tags.TAG_DOMAIN), domain))
                    return;

                Interlocked.Add(ref _total, measurement);
                LastTags = snapshot;
            });
            r_listener.Start();
        }

        public long Total => Interlocked.Read(ref _total);

        public IReadOnlyDictionary<string, object?> LastTags { get; private set; } =
            new Dictionary<string, object?>(StringComparer.Ordinal);

        public void Dispose() => r_listener.Dispose();
    }
}
