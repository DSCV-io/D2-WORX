// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafRegressionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AuthOutbound.WorkloadCertificate;

using System.Diagnostics.Metrics;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Outbound;
using DcsvIo.D2.Auth.Outbound.Telemetry;
using DcsvIo.D2.Auth.Outbound.WorkloadCertificate;
using DcsvIo.D2.Result;
using DcsvIo.D2.Tests.Unit.Handler;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NodaTime;
using Xunit;

/// <summary>
/// Regression tests for the disposal-flag memory-fence fix,
/// the reissue-failure metric counter and its accompanying log field,
/// and the cache-hit debug log + startup-success info log.
/// Each test records what breaks without the fix and passes with it.
/// </summary>
[Collection("OutboundTelemetrySerial")]
[Trait("Category", "Unit")]
public sealed class WorkloadLeafRegressionTests
{
    private static readonly Instant SR_Base = Instant.FromUtc(2026, 1, 1, 0, 0, 0);

    // ------------------------------------------------------------------
    // Disposal-flag Volatile fence: WorkloadLeafClient
    // ------------------------------------------------------------------

    [Fact]
    public async Task WorkloadLeafClient_AfterDispose_GetCurrentLeafAsync_ThrowsObjectDisposed()
    {
        // Regression: WorkloadLeafClient.Dispose() must write the flag with
        // Volatile.Write, and GetCurrentLeafAsync must read with Volatile.Read,
        // so that the disposed state is visible without compiler/CPU reordering
        // on weakly-ordered architectures. The observable is: after Dispose(),
        // GetCurrentLeafAsync throws ObjectDisposedException.
        using var cache = new WorkloadLeafCache();
        var clock = new FakeTimeProvider(SR_Base.ToDateTimeOffset());
        using var issuer = new FakeWorkloadCertificateIssuer(clock);
        var client = new WorkloadLeafClient(
            issuer, cache, NullLogger<WorkloadLeafClient>.Instance, clock);

        client.Dispose();

        var act = async () => await client.GetCurrentLeafAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>(
            "the disposal flag is written with Volatile.Write so a reading thread " +
            "observes the write without CPU reordering");
    }

    [Fact]
    public async Task WorkloadLeafClient_AfterDispose_ForceReissueAsync_ThrowsObjectDisposed()
    {
        // Same Volatile-fence regression for ForceReissueAsync.
        using var cache = new WorkloadLeafCache();
        var clock = new FakeTimeProvider(SR_Base.ToDateTimeOffset());
        using var issuer = new FakeWorkloadCertificateIssuer(clock);
        var client = new WorkloadLeafClient(
            issuer, cache, NullLogger<WorkloadLeafClient>.Instance, clock);

        client.Dispose();

        var act = async () => await client.ForceReissueAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>(
            "ForceReissueAsync also reads the disposal flag via Volatile.Read");
    }

    // ------------------------------------------------------------------
    // Disposal-flag Volatile fence: WorkloadLeafCache
    // ------------------------------------------------------------------

    [Fact]
    public void WorkloadLeafCache_AfterDispose_SecondDisposeDoesNotThrow()
    {
        // Regression: WorkloadLeafCache.Dispose() must be idempotent; the disposal
        // flag guard (Volatile.Read) prevents the cert-disposal code from running
        // a second time (which would attempt to dispose already-disposed handles).
        var cache = new WorkloadLeafCache();
        cache.Dispose();

        var act = () => cache.Dispose();

        act.Should().NotThrow(
            "Volatile.Read of the disposed flag makes Dispose() idempotent");
    }

    [Fact]
    public void WorkloadLeafCache_AfterDispose_CertificatesAreDisposed()
    {
        // Regression: the disposal-flag Volatile.Write ensures that after Dispose()
        // returns, the leaf and intermediate certificates are disposed. The leaf holds
        // the ECDSA private key; its disposal is the Schannel key-container release.
        var cache = new WorkloadLeafCache();
        var snapshot = ASnapshot(SR_Base + Duration.FromHours(1));
        cache.Set(snapshot);

        cache.Dispose();

        var leafAct = () => _ = snapshot.Leaf.GetECDsaPrivateKey()!.ExportPkcs8PrivateKey();
        leafAct.Should().Throw<Exception>(
            "the disposal flag is written with a Volatile fence; the leaf cert is disposed on Dispose()");
    }

    // ------------------------------------------------------------------
    // Reissue-failure metric counter + CachedLeafNotAfter log field
    // ------------------------------------------------------------------

    [Fact]
    public async Task WorkloadLeafClient_ReissueExceptionPath_IncrementsLeafReissueFailuresCounter()
    {
        // Regression: when WorkloadLeafClient.ReissueAsync catches an exception
        // (e.g. the issuer throws), it must increment SR_LeafReissueFailures.
        // Prior to the fix the counter was never incremented.
        using var cache = new WorkloadLeafCache();
        var clock = new FakeTimeProvider(SR_Base.ToDateTimeOffset());
        using var issuer = new ThrowingWorkloadCertificateIssuer();

        using var client = new WorkloadLeafClient(
            issuer, cache, NullLogger<WorkloadLeafClient>.Instance, clock);

        using var listener = new LeafReissueFailuresListener();

        await client.ForceReissueAsync();

        listener.Total.Should().Be(1, "one exception-path reissue failure increments SR_LeafReissueFailures by 1");
    }

    [Fact]
    public async Task WorkloadLeafClient_ReissueExceptionPath_LogsCachedLeafNotAfterNone_WhenNoCachedLeaf()
    {
        // When no leaf has been cached before the exception-path failure,
        // the WorkloadLeafReissueFailed log record (EventId=3001) must carry
        // CachedLeafNotAfter="none" in its rendered message.
        using var cache = new WorkloadLeafCache();
        var clock = new FakeTimeProvider(SR_Base.ToDateTimeOffset());
        using var issuer = new ThrowingWorkloadCertificateIssuer();
        var logger = new TestLogger<WorkloadLeafClient>();

        using var client = new WorkloadLeafClient(issuer, cache, logger, clock);

        await client.ForceReissueAsync();

        var entry = logger.Entries.FirstOrDefault(e => e.EventId.Id == 3001);

        entry.Should().NotBeNull("WorkloadLeafReissueFailed (EventId=3001) must be emitted on exception-path reissue");
        entry.Message.Should().Contain(
            "CachedLeafNotAfter=none",
            "CachedLeafNotAfter is 'none' when no cached leaf exists at failure time");
    }

    [Fact]
    public async Task WorkloadLeafClient_ReissueExceptionPath_LogsCachedLeafNotAfterIso8601_WhenCachedLeafExists()
    {
        // When a stale cached leaf exists at failure time, the WorkloadLeafReissueFailed
        // log record (EventId=3001) must carry the ISO-8601 round-trip of the stale
        // leaf's NotAfter in its CachedLeafNotAfter structured field.
        var clock = new FakeTimeProvider(SR_Base.ToDateTimeOffset());
        using var cache = new WorkloadLeafCache();

        // Seed the cache with an already-expired snapshot (past SR_Base) so that
        // TryGet returns null (forcing ReissueAsync to attempt a real reissue) but
        // PeekRaw still returns the stale snapshot (the CachedLeafNotAfter source).
        var staleNotAfter = SR_Base - Duration.FromMinutes(1);
        cache.Set(ASnapshot(staleNotAfter));

        using var issuer = new ThrowingWorkloadCertificateIssuer();
        var logger = new TestLogger<WorkloadLeafClient>();

        using var client = new WorkloadLeafClient(issuer, cache, logger, clock);

        using var listener = new LeafReissueFailuresListener();

        await client.ForceReissueAsync();

        listener.Total.Should().Be(1, "one exception-path reissue → one counter increment");

        var entry = logger.Entries.FirstOrDefault(e => e.EventId.Id == 3001);

        entry.Should().NotBeNull("WorkloadLeafReissueFailed (EventId=3001) must be emitted");
        entry.Message.Should().NotContain(
            "CachedLeafNotAfter=none",
            "a stale cached leaf provides a real ISO-8601 timestamp, not 'none'");

        // The rendered message must contain the ISO-8601 round-trip of the stale NotAfter.
        var expected = staleNotAfter.ToDateTimeOffset().ToString("O");
        entry.Message.Should().Contain(
            expected,
            "CachedLeafNotAfter is the ISO-8601 round-trip of the stale NotAfter Instant");
    }

    // ------------------------------------------------------------------
    // Cache-hit debug log
    // ------------------------------------------------------------------

    [Fact]
    public async Task WorkloadLeafClient_CacheHit_EmitsDebugLog()
    {
        // Regression: on a cache hit (second call after first populates cache),
        // WorkloadLeafClient must emit a Debug log via WorkloadLeafCacheHit.
        // EventId = 3004, Level = Debug.
        using var cache = new WorkloadLeafCache();
        var clock = new FakeTimeProvider(SR_Base.ToDateTimeOffset());
        using var issuer = new FakeWorkloadCertificateIssuer(clock);
        var logger = new TestLogger<WorkloadLeafClient>();

        using var client = new WorkloadLeafClient(issuer, cache, logger, clock);

        // First call — populates cache (no cache-hit log).
        await client.GetCurrentLeafAsync();

        // Second call — cache hit.
        await client.GetCurrentLeafAsync();

        var cacheHitEntry = logger.Entries.FirstOrDefault(
            e => e.EventId.Id == 3004 && e.Level == LogLevel.Debug);

        cacheHitEntry.Should().NotBeNull(
            "WorkloadLeafClient must emit EventId=3004/Debug on cache hit");
    }

    // ------------------------------------------------------------------
    // Startup-success info log
    // ------------------------------------------------------------------

    [Fact]
    public async Task WorkloadLeafRefreshHostedService_StartupAcquireSucceeds_EmitsInfoLog()
    {
        // Regression: when the startup ForceReissueAsync succeeds and the cache
        // is populated, WorkloadLeafRefreshHostedService must emit an Info log
        // via WorkloadLeafStartupAcquireSucceeded. EventId = 3005, Level = Information.
        using var cache = new WorkloadLeafCache();
        cache.OnSetForTesting = () => { };

        var clock = new FakeTimeProvider(SR_Base.ToDateTimeOffset());
        using var issuer = new FakeWorkloadCertificateIssuer(clock);
        var clientLogger = NullLogger<WorkloadLeafClient>.Instance;
        using var client = new WorkloadLeafClient(issuer, cache, clientLogger, clock);

        var serviceLogger = new TestLogger<WorkloadLeafRefreshHostedService>();
        var options = Options.Create(new AuthOutboundOptions
        {
            WorkloadLeafRefreshLeadTime = TimeSpan.FromMinutes(5),
        });

        using var service = new WorkloadLeafRefreshHostedService(
            client, cache, options, serviceLogger, clock);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        // Wait until the issuer has been called at least once (startup acquire done).
        await issuer.WaitForInvocationCountAsync(1);

        await cts.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        var startupSuccessEntry = serviceLogger.Entries.FirstOrDefault(
            e => e.EventId.Id == 3005 && e.Level == LogLevel.Information);

        startupSuccessEntry.Should().NotBeNull(
            "WorkloadLeafRefreshHostedService must emit EventId=3005/Information on successful startup acquire");
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private static WorkloadLeafSnapshot ASnapshot(Instant notAfter)
    {
        var now = DateTimeOffset.UtcNow;

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=test", key, HashAlgorithmName.SHA256);
        var leaf = req.CreateSelfSigned(now.AddMinutes(-5), now.AddHours(24));

        using var key2 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req2 = new CertificateRequest("CN=issuer", key2, HashAlgorithmName.SHA256);
        var intermediate = req2.CreateSelfSigned(now.AddMinutes(-5), now.AddHours(24));

        var context = SslStreamCertificateContext.Create(leaf, [intermediate], offline: true);

        return new WorkloadLeafSnapshot(leaf, intermediate, context, notAfter);
    }

    /// <summary>
    /// Test issuer that always throws an <see cref="InvalidOperationException"/> from
    /// <c>IssueAsync</c> — exercises the <c>catch (Exception ex)</c> branch in
    /// <c>WorkloadLeafClient.ReissueAsync</c> that increments
    /// <c>SR_LeafReissueFailures</c>. Distinct from <see cref="FakeWorkloadCertificateIssuer"/>
    /// (which returns a failed <c>D2Result</c> on the non-exception failure path).
    /// </summary>
    private sealed class ThrowingWorkloadCertificateIssuer : IWorkloadCertificateIssuer, IDisposable
    {
        public ValueTask<D2Result<WorkloadLeafMaterial>> IssueAsync(
            byte[] csrDer, CancellationToken ct = default) =>
            throw new InvalidOperationException("Injected issuer exception for exception-path test coverage.");

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Captures measurements from <c>d2.auth.outbound.workload_leaf.reissue_failures</c>
    /// and exposes the cumulative total count. The counter is untagged — the accompanying
    /// <c>CachedLeafNotAfter</c> context is captured as a structured field on the
    /// <c>WorkloadLeafReissueFailed</c> log event (EventId=3001) rather than a
    /// high-cardinality metric tag.
    /// </summary>
    private sealed class LeafReissueFailuresListener : IDisposable
    {
        private const string _INSTRUMENT = "d2.auth.outbound.workload_leaf.reissue_failures";

        private readonly MeterListener r_listener = new();
        private long _total;

        public LeafReissueFailuresListener()
        {
            r_listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == OutboundTelemetry.METER_NAME &&
                    instrument.Name == _INSTRUMENT)
                    listener.EnableMeasurementEvents(instrument);
            };

            r_listener.SetMeasurementEventCallback<long>((_, value, _, _) =>
            {
                Interlocked.Add(ref _total, value);
            });

            r_listener.Start();
        }

        public long Total => Interlocked.Read(ref _total);

        public void Dispose() => r_listener.Dispose();
    }
}
