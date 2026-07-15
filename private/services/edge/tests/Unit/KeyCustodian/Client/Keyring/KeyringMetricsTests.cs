// -----------------------------------------------------------------------
// <copyright file="KeyringMetricsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Client.Keyring;

using System.Diagnostics.Metrics;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.Client.Keyring;
using D2.Shared.Encryption;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Observability pins for <see cref="KeyringMetrics"/> — the rotation-hot-swap and
/// refresh-failure counters fire through the real wrapper path with the closed-set
/// named-constant tag keys (rules.md §21.11).
/// </summary>
public sealed class KeyringMetricsTests
{
    // A file-unique fixture domain isolates these counter pins from every other test
    // in the process: KeyringMetrics counters are static (one meter per process, like
    // the sibling meters), so concurrently-running tests that drive swaps/refreshes
    // for OTHER domains hit the same instruments. The capture below selects this
    // file's measurements by their domain tag, never by instrument alone.
    private const string _METRICS_DOMAIN = "fixture-keyring-metrics-domain";

    private static readonly TimeSpan sr_tiny = TimeSpan.FromMilliseconds(1);

    [Fact]
    public async Task RotationSwap_IncrementsHotSwapCounter()
    {
        using var capture = new MeterCapture("d2.keyring.rotation_hot_swaps", _METRICS_DOMAIN);
        var channel = NewChannel();
        var client = new FakeKeyringClient(i => D2Result<PayloadCryptoKeyring>.Ok(
            i == 1
                ? KeyringTestFixtures.SingleKidKeyring(_METRICS_DOMAIN)
                : KeyringTestFixtures.RotatedKeyring(_METRICS_DOMAIN)));
        await using var crypto = NewCrypto(client, channel, maxAttempts: 3);

        await channel.DispatchAsync(_METRICS_DOMAIN, CancellationToken.None);

        capture.Total.Should().Be(1);
        capture.LastTags.Should().ContainKey(KeyringMetrics.Tags.TAG_DOMAIN);
        capture.LastTags[KeyringMetrics.Tags.TAG_DOMAIN].Should().Be(_METRICS_DOMAIN);
    }

    [Fact]
    public async Task RefreshFailure_IncrementsFailureCounter_WithClosedSetTags()
    {
        using var capture = new MeterCapture("d2.keyring.refresh_failures", _METRICS_DOMAIN);
        var channel = NewChannel();
        var client = new FakeKeyringClient(i => i == 1
            ? D2Result<PayloadCryptoKeyring>.Ok(
                KeyringTestFixtures.SingleKidKeyring(_METRICS_DOMAIN))
            : D2Result<PayloadCryptoKeyring>.ServiceUnavailable());
        await using var crypto = NewCrypto(client, channel, maxAttempts: 2);

        await channel.DispatchAsync(_METRICS_DOMAIN, CancellationToken.None);

        capture.Total.Should().Be(1);
        capture.LastTags.Should().ContainKey(KeyringMetrics.Tags.TAG_DOMAIN);
        capture.LastTags.Should().ContainKey(KeyringMetrics.Tags.TAG_ERROR_CODE);
    }

    [Fact]
    public void MeterAndInstrumentNames_WireLiterals_ArePinned()
    {
        // Per-VALUE pins: a wire-name rename must fail a test, never pass silently.
        KeyringMetrics.METER_NAME.Should().Be("D2.Edge.KeyCustodian.Client");
        KeyringMetrics.SR_Meter.Name.Should().Be("D2.Edge.KeyCustodian.Client");

        KeyringMetrics.SR_KeyringFetches.Name.Should().Be("d2.keyring.fetches");
        KeyringMetrics.SR_RefreshFailures.Name.Should().Be("d2.keyring.refresh_failures");
        KeyringMetrics.SR_RotationHotSwaps.Name.Should().Be("d2.keyring.rotation_hot_swaps");
    }

    [Fact]
    public void TagKeysAndClosedSetValues_WireLiterals_ArePinned()
    {
        KeyringMetrics.Tags.TAG_DOMAIN.Should().Be("domain");
        KeyringMetrics.Tags.TAG_RESULT.Should().Be("result");
        KeyringMetrics.Tags.TAG_ERROR_CODE.Should().Be("errorCode");
        KeyringMetrics.Tags.SUCCESS.Should().Be("success");
        KeyringMetrics.Tags.FAILURE.Should().Be("failure");
        KeyringMetrics.Tags.NONE.Should().Be("<none>");
    }

    private static RabbitMqRotationEventChannel NewChannel()
        => new(NullLogger<RabbitMqRotationEventChannel>.Instance);

    private static KeyringBackedPayloadCrypto NewCrypto(
        FakeKeyringClient client, RabbitMqRotationEventChannel channel, int maxAttempts)
        => KeyringBackedPayloadCrypto.CreateForTesting(
            _METRICS_DOMAIN,
            client,
            channel,
            NullLogger<KeyringBackedPayloadCrypto>.Instance,
            sr_tiny,
            maxAttempts,
            sr_tiny);

    /// <summary>
    /// Captures long-counter measurements for a single instrument, counting only
    /// measurements tagged with the given domain (per-test isolation on the
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

                if (!Equals(snapshot.GetValueOrDefault(KeyringMetrics.Tags.TAG_DOMAIN), domain))
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
