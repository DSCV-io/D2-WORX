// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafClientTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.WorkloadCertificate;

using System.Diagnostics.Metrics;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using D2.Shared.Auth.Outbound.Telemetry;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using D2.Shared.Result;
using D2.Shared.Tests.Unit.Handler;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

/// <summary>
/// The refresh-ahead leaf-client matrix — first-call issuance, cache-hit reuse,
/// serve-stale on transient failure, hard-fail when expired-and-unreachable,
/// singleflight dedup — plus the CSR-flow custody pins: the issuer receives a
/// well-formed proof-of-possession-valid P-256 CSR, the private key never crosses
/// the seam (structural), the returned leaf pairs with the locally-generated key,
/// the CSR's placeholder subject is ignored, rotation mints a fresh keypair, and a
/// mismatched-key leaf is rejected before any cache write.
/// </summary>
/// <remarks>
/// In the OutboundTelemetrySerial collection because the mismatch-reject pins
/// assert the process-wide <c>SR_LeafReissueFailures</c> counter.
/// </remarks>
[Collection("OutboundTelemetrySerial")]
[Trait("Category", "Unit")]
public sealed class WorkloadLeafClientTests
{
    private static readonly DateTimeOffset SR_Base =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetCurrentLeafAsync_FirstCall_IssuesAndReturnsLeaf()
    {
        using var harness = new Harness();

        var result = await harness.Client.GetCurrentLeafAsync();

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        harness.Issuer.IssuanceCount.Should().Be(1);
    }

    [Fact]
    public async Task GetCurrentLeafAsync_SecondCall_ServesFromCache_NoReissue()
    {
        using var harness = new Harness();

        await harness.Client.GetCurrentLeafAsync();
        var second = await harness.Client.GetCurrentLeafAsync();

        second.Success.Should().BeTrue();
        harness.Issuer.IssuanceCount.Should().Be(1, "the cached leaf is served without reissue");
    }

    [Fact]
    public async Task GetCurrentLeafAsync_ServesStale_WhenReissueTransientlyFailsButLeafStillValid()
    {
        using var harness = new Harness(validity: TimeSpan.FromHours(24));

        // Populate the cache.
        await harness.Client.GetCurrentLeafAsync();

        // Expire the cache view by advancing within validity is not needed — the
        // cached leaf is still valid; force the client to attempt a reissue by
        // clearing then arming a transient failure. Easier: arm a failure and call
        // ForceReissueAsync — the still-valid cached leaf keeps serving.
        harness.Issuer.SetFail(true);

        // The cached leaf is still valid (24h), so GetCurrentLeafAsync returns it
        // straight from the cache without even attempting a reissue.
        var result = await harness.Client.GetCurrentLeafAsync();

        result.Success.Should().BeTrue("a still-valid cached leaf is served");
    }

    [Fact]
    public async Task GetCurrentLeafAsync_HardFails_WhenExpiredAndIssuerUnreachable()
    {
        using var harness = new Harness(validity: TimeSpan.FromMinutes(10));

        // Populate, then advance past the leaf's expiry so the cache no longer
        // serves it; arm the issuer to fail so reissue can't succeed.
        await harness.Client.GetCurrentLeafAsync();
        harness.Clock.Advance(TimeSpan.FromMinutes(20));
        harness.Issuer.SetFail(true);

        var result = await harness.Client.GetCurrentLeafAsync();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ForceReissueAsync_Succeeds_PopulatesCache()
    {
        using var harness = new Harness();

        var result = await harness.Client.ForceReissueAsync();

        result.Success.Should().BeTrue();
        harness.Cache.PeekRaw().Should().NotBeNull();
    }

    [Fact]
    public async Task ForceReissueAsync_IssuerFails_ReturnsServiceUnavailable()
    {
        using var harness = new Harness();
        harness.Issuer.SetFail(true);

        var result = await harness.Client.ForceReissueAsync();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task ForceReissueAsync_WhenValidLeafCachedInsideLeadWindow_MintsFreshLeaf()
    {
        // Regression (refresh-ahead lead window): WorkloadLeafRefreshHostedService calls
        // ForceReissueAsync precisely while the current leaf is STILL VALID but nearing
        // expiry (inside the lead window). ForceReissueAsync MUST mint a fresh leaf then,
        // NOT short-circuit on the still-valid cache. Before the force/opportunistic split
        // in ReissueAsync, the non-expired re-check early-returned Successful() without
        // minting, so the leaf only ever reissued at/after expiry — defeating refresh-ahead
        // and exposing on-demand callers to a synchronous mint stall at expiry.
        using var harness = new Harness(validity: TimeSpan.FromMinutes(10));

        // First force populates the cache with a leaf valid for 10 min.
        var first = await harness.Client.ForceReissueAsync();

        first.Success.Should().BeTrue();
        harness.Issuer.IssuanceCount.Should().Be(1);

        var firstNotAfter = harness.Cache.PeekRaw()!.NotAfter;
        var firstCsrSpki = harness.Issuer.LastCsrPublicKeySpki!.ToArray();

        // Advance INTO the lead window but BEFORE expiry: 6 min in, 4 min of validity left.
        // The cached leaf is still valid (TryGet returns it), so the pre-split re-check
        // would have suppressed the mint here.
        harness.Clock.Advance(TimeSpan.FromMinutes(6));

        var second = await harness.Client.ForceReissueAsync();

        second.Success.Should().BeTrue();
        harness.Issuer.IssuanceCount.Should().Be(
            2, "ForceReissueAsync mints a fresh leaf even while a still-valid leaf is cached");
        harness.Cache.PeekRaw()!.NotAfter.Should().BeGreaterThan(
            firstNotAfter, "the cached leaf rotated to a fresh, later NotAfter");
        harness.Issuer.LastCsrPublicKeySpki!.ToArray().Should().NotEqual(
            firstCsrSpki, "the forced reissue generated a fresh keypair");
    }

    [Fact]
    public async Task ForceReissueAsync_RacingConcurrentOnDemandGets_ShareOneMint_NoTornRead()
    {
        // ADVERSARIAL concurrency: a proactive ForceReissueAsync racing on-demand
        // GetCurrentLeafAsync callers on an empty cache. Both entry points share the one
        // singleflight key, so they dedup to a SINGLE mint (no double-mint), and every
        // caller observes the one coherent, private-key-bearing leaf (no torn read). The
        // issuer is GATED so the single in-flight reissue suspends inside IssueAsync until
        // every caller has attached to it — making the dedup deterministic rather than
        // scheduler-dependent (a synchronous issuer would let each flight complete before
        // the next caller attaches, defeating the dedup the test means to prove).
        var clock = new FakeTimeProvider(SR_Base);
        using var cache = new WorkloadLeafCache();
        using var issuer = new GatedWorkloadCertificateIssuer(clock);
        using var client = new WorkloadLeafClient(
            issuer, cache, NullLogger<WorkloadLeafClient>.Instance, clock);

        var forceTasks = new List<Task<D2Result>>();
        var getTasks = new List<Task<D2Result<X509Certificate2>>>();

        // Start every caller synchronously: each runs up to its Singleflight GetOrAdd
        // before suspending on the shared task, so once the loop returns all 32 have
        // attached to the one gated flight (its key stays present while it is suspended).
        for (var i = 0; i < 16; i++)
        {
            forceTasks.Add(client.ForceReissueAsync().AsTask());
            getTasks.Add(client.GetCurrentLeafAsync().AsTask());
        }

        // The single in-flight reissue has reached the gated issuer; release it so the one
        // mint completes and every attached caller converges on its result.
        await issuer.WaitForArrivalAsync();
        issuer.Release();

        await Task.WhenAll(getTasks.Cast<Task>().Concat(forceTasks));

        forceTasks.Should().AllSatisfy(
            t => t.Result.Success.Should().BeTrue("every forced reissue resolves to a valid leaf"));
        getTasks.Should().AllSatisfy(t =>
        {
            t.Result.Success.Should().BeTrue();
            t.Result.Data!.HasPrivateKey.Should().BeTrue(
                "an on-demand caller observes the one coherent, key-bearing leaf — never a torn snapshot");
        });
        issuer.IssuanceCount.Should().Be(
            1, "force + on-demand callers dedup to a single mint via the shared singleflight key");
    }

    [Fact]
    public async Task GetCurrentLeafAsync_ConcurrentFirstCallers_ShareOneIssuance()
    {
        using var harness = new Harness();

        var calls = Enumerable.Range(0, 16)
            .Select(_ => harness.Client.GetCurrentLeafAsync().AsTask())
            .ToArray();

        var results = await Task.WhenAll(calls);

        results.Should().AllSatisfy(r => r.Success.Should().BeTrue());
        harness.Issuer.IssuanceCount.Should().Be(
            1, "concurrent first-callers dedup to a single reissue via singleflight");
    }

    [Fact]
    public async Task GetCurrentLeafAsync_AfterDispose_Throws()
    {
        var harness = new Harness();
        harness.Client.Dispose();

        var act = async () => await harness.Client.GetCurrentLeafAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>();
        harness.Issuer.Dispose();
    }

    [Fact]
    public async Task GetCurrentLeafAsync_BuiltLeaf_HasPrivateKey()
    {
        // The reissue path builds a private-key-bearing cert (the PKCS#8 is imported
        // then zeroed); the live cert the channel presents must hold its key.
        using var harness = new Harness();

        var result = await harness.Client.GetCurrentLeafAsync();

        result.Data!.HasPrivateKey.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentLeafAsync_BuiltSnapshot_RetainsIntermediate()
    {
        // The reissue path retains the issuing intermediate (from the material's
        // IssuerCertificateDer) so the full chain can be presented. The intermediate is
        // the public issuer cert (no private key).
        using var harness = new Harness();

        await harness.Client.GetCurrentLeafAsync();

        var snapshot = harness.Cache.PeekRaw();
        snapshot.Should().NotBeNull();
        snapshot.Intermediate.Should().NotBeNull("the issuing intermediate is retained");
        snapshot.Intermediate.HasPrivateKey.Should().BeFalse(
            "the presented intermediate is the public issuer cert, not a key-bearing one");
    }

    [Fact]
    public async Task GetCurrentLeafAsync_BuiltSnapshot_PresentsChainOrFallsBackToLeaf()
    {
        // On Linux/OpenSSL (the deployment target) the snapshot carries a pre-built
        // leaf -> intermediate chain context (the full chain is presented). On Windows,
        // where Schannel will not build a chain context for a leaf whose internal-CA
        // root is not in the OS trust store, the context is tolerated-null and the
        // presentation falls back to the bare leaf — either way the leaf is cached and
        // GetCurrentLeafAsync succeeds (it never hard-fails on the chain-context build).
        using var harness = new Harness();

        var result = await harness.Client.GetCurrentLeafAsync();

        result.Success.Should().BeTrue(
            "the live leaf is cached regardless of whether the chain context could be built");

        var snapshot = harness.Cache.PeekRaw();
        snapshot.Should().NotBeNull();

        if (OperatingSystem.IsWindows())
        {
            // The bare-leaf fallback path is acceptable on Windows (dev), not the
            // deployment target; the leaf is still present for presentation.
            snapshot.Leaf.HasPrivateKey.Should().BeTrue(
                "the bare leaf is presented when no chain context can be built on Windows");
        }
        else
        {
            snapshot.ChainContext.Should().NotBeNull(
                "on the deployment target the channel presents the full leaf -> intermediate chain");
        }
    }

    [Fact]
    public async Task GetCurrentLeafAsync_IssuerReceivesWellFormedPopValidP256Csr()
    {
        // The seam carries a REAL PKCS#10 CSR: it loads with proof-of-possession
        // validation ON (a broken self-signature would throw here) and certifies an
        // ECDSA P-256 key — the leaf key policy the issuer enforces.
        using var harness = new Harness();

        await harness.Client.GetCurrentLeafAsync();

        var csrDer = harness.Issuer.LastReceivedCsrDer;
        csrDer.Should().NotBeNull("an issuance must have completed");

        var loaded = CertificateRequest.LoadSigningRequest(csrDer, HashAlgorithmName.SHA256);

        using var ecdsa = loaded.PublicKey.GetECDsaPublicKey();
        ecdsa.Should().NotBeNull("the CSR must certify an elliptic-curve key");
        ecdsa.KeySize.Should().Be(256, "the leaf key policy is ECDSA P-256");
    }

    [Fact]
    public void IssuerPort_IsStructurallyPrivateKeyFree()
    {
        // The strictly-stronger successor to the received-buffer zeroize pin: no
        // private key is ever received, because the port's only data parameter is
        // the CSR (public by construction) and the returned material carries no
        // private-key member — the custody guarantee is structural, not procedural.
        var issueAsync = typeof(IWorkloadCertificateIssuer).GetMethod("IssueAsync")!;
        var dataParams = issueAsync.GetParameters()
            .Where(p => p.ParameterType != typeof(CancellationToken))
            .ToArray();

        dataParams.Should().ContainSingle("the port carries exactly one data parameter");
        dataParams[0].ParameterType.Should().Be<byte[]>(
            "the sole data crossing the seam is the CSR DER");
        dataParams[0].Name.Should().Be("csrDer");

        typeof(WorkloadLeafMaterial).GetProperties()
            .Should().NotContain(
                p => p.Name.Contains("PrivateKey") || p.Name.Contains("Pkcs8"),
                "the returned material is all-public — no private-key member exists");
    }

    [Fact]
    public async Task GetCurrentLeafAsync_ReturnedLeafPairsWithLocalKey()
    {
        // The live leaf must hold the LOCALLY-generated private key, and its
        // certified public key must equal the CSR's — the pairing proof.
        using var harness = new Harness();

        var result = await harness.Client.GetCurrentLeafAsync();

        result.Data!.HasPrivateKey.Should().BeTrue(
            "the leaf pairs with the locally-generated key");
        result.Data.PublicKey.ExportSubjectPublicKeyInfo()
            .Should().Equal(
                harness.Issuer.LastCsrPublicKeySpki,
                "the leaf certifies exactly the key the CSR carried");
    }

    [Fact]
    public async Task GetCurrentLeafAsync_LeafIdentityFromIssuerPeerView_CsrSubjectIgnored()
    {
        // The consumer-seam mirror of the issuer's no-forgery invariant: the leaf's
        // SAN + subject come from the ISSUER's authenticated peer view (the fake's
        // configured serviceId), never from the CSR's placeholder subject.
        using var harness = new Harness();

        var result = await harness.Client.GetCurrentLeafAsync();

        // The CSR itself carried the fixed placeholder subject…
        var csr = CertificateRequest.LoadSigningRequest(
            harness.Issuer.LastReceivedCsrDer!, HashAlgorithmName.SHA256);
        csr.SubjectName.Name.Should().Be("CN=d2-workload");

        // …but the leaf's identity is the issuer's peer view.
        result.Data!.Subject.Should().Be(
            "CN=edge", "the leaf subject comes from the issuer's peer view");

        var sanUris = ReadUriSans(result.Data);
        sanUris.Should().ContainSingle()
            .Which.Should().Be(
                "spiffe://d2.internal/workload/edge",
                "the SAN is minted from the issuer's authenticated peer view");
    }

    [Fact]
    public async Task GetCurrentLeafAsync_RotationMintsFreshKeypair()
    {
        // Per-rotation key freshness: a second reissue submits a CSR certifying a
        // DIFFERENT public key than the first — no long-lived reused keypair.
        using var harness = new Harness(validity: TimeSpan.FromMinutes(10));

        await harness.Client.GetCurrentLeafAsync();
        var firstSpki = harness.Issuer.LastCsrPublicKeySpki!.ToArray();

        harness.Clock.Advance(TimeSpan.FromMinutes(20));
        await harness.Client.GetCurrentLeafAsync();
        var secondSpki = harness.Issuer.LastCsrPublicKeySpki!.ToArray();

        harness.Issuer.IssuanceCount.Should().Be(2, "expiry forces a second issuance");
        secondSpki.Should().NotEqual(
            firstSpki, "every reissue generates a fresh keypair");
    }

    [Fact]
    public async Task GetCurrentLeafAsync_MismatchedIssuerKey_RejectedWithTelemetry_NoCacheWrite()
    {
        // ADVERSARIAL: a returned leaf certifying a DIFFERENT key than the CSR's can
        // never be presented (no private key exists for it) — the client rejects it
        // BEFORE any cache write, fires the mismatch log + the reissue-failure
        // counter, and surfaces the transient 503.
        using var harness = new Harness();
        var logger = new TestLogger<WorkloadLeafClient>();
        using var client = new WorkloadLeafClient(
            harness.Issuer, harness.Cache, logger, harness.Clock);

        harness.Issuer.SetMintMismatchedKey(true);

        using var listener = new LeafReissueFailuresListener();

        var result = await client.GetCurrentLeafAsync();

        result.Success.Should().BeFalse("a mismatched-key leaf is rejected");
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        harness.Cache.PeekRaw().Should().BeNull("a rejected leaf never enters the cache");
        listener.Total.Should().Be(1, "the mismatch counts as a reissue failure");
        logger.Entries.Should().Contain(
            e => e.EventId.Id == 3006,
            "the issuer-key-mismatch warning names the rejection");
    }

    [Fact]
    public async Task GetCurrentLeafAsync_MismatchedReissue_KeepsStaleSnapshotUntouched()
    {
        // The serve-stale posture survives the mismatch reject: the previously-cached
        // snapshot is NOT overwritten by the rejected leaf.
        using var harness = new Harness(validity: TimeSpan.FromMinutes(10));

        await harness.Client.GetCurrentLeafAsync();
        var staleNotAfter = harness.Cache.PeekRaw()!.NotAfter;

        harness.Clock.Advance(TimeSpan.FromMinutes(20));
        harness.Issuer.SetMintMismatchedKey(true);

        var result = await harness.Client.GetCurrentLeafAsync();

        result.Success.Should().BeFalse("the expired cache cannot serve and the reissue was rejected");
        harness.Cache.PeekRaw()!.NotAfter.Should().Be(
            staleNotAfter, "the rejected leaf never overwrites the stale snapshot");
    }

    /// <summary>
    /// Reads every URI subject-alternative-name from a certificate (GeneralName
    /// CHOICE [6] IA5String — the same ASN.1 walk the shipped peer validator uses).
    /// </summary>
    /// <param name="certificate">The certificate whose SAN URIs to read.</param>
    /// <returns>The URI SAN values, in encounter order.</returns>
    private static List<string> ReadUriSans(X509Certificate2 certificate)
    {
        var uriSanTag = new System.Formats.Asn1.Asn1Tag(
            System.Formats.Asn1.TagClass.ContextSpecific, 6);

        var uris = new List<string>();

        var sanExtension = certificate.Extensions
            .OfType<X509SubjectAlternativeNameExtension>()
            .FirstOrDefault();

        if (sanExtension is null)
            return uris;

        var outer = new System.Formats.Asn1.AsnReader(
            sanExtension.RawData, System.Formats.Asn1.AsnEncodingRules.DER);

        var names = outer.ReadSequence();

        while (names.HasData)
        {
            if (names.PeekTag().HasSameClassAndValue(uriSanTag))
            {
                uris.Add(names.ReadCharacterString(
                    System.Formats.Asn1.UniversalTagNumber.IA5String, uriSanTag));
            }
            else
            {
                names.ReadEncodedValue();
            }
        }

        return uris;
    }

    /// <summary>
    /// Wraps <see cref="FakeWorkloadCertificateIssuer"/> and BLOCKS inside
    /// <c>IssueAsync</c> until <see cref="Release"/> is called — so a test can hold the
    /// single in-flight reissue open while concurrent callers attach to it, making
    /// singleflight-dedup assertions deterministic instead of scheduler-dependent.
    /// </summary>
    private sealed class GatedWorkloadCertificateIssuer : IWorkloadCertificateIssuer, IDisposable
    {
        private readonly FakeWorkloadCertificateIssuer r_inner;

        private readonly TaskCompletionSource r_gate =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly SemaphoreSlim r_arrived = new(0);

        public GatedWorkloadCertificateIssuer(TimeProvider clock, TimeSpan? validity = null)
            => r_inner = new FakeWorkloadCertificateIssuer(clock, validity: validity);

        public int IssuanceCount => r_inner.IssuanceCount;

        public async ValueTask<D2Result<WorkloadLeafMaterial>> IssueAsync(
            byte[] csrDer, CancellationToken ct = default)
        {
            r_arrived.Release();

            await r_gate.Task.WaitAsync(ct);

            return await r_inner.IssueAsync(csrDer, ct);
        }

        public Task WaitForArrivalAsync() => r_arrived.WaitAsync();

        public void Release() => r_gate.TrySetResult();

        public void Dispose()
        {
            r_arrived.Dispose();
            r_inner.Dispose();
        }
    }

    /// <summary>
    /// Captures measurements from <c>d2.auth.outbound.workload_leaf.reissue_failures</c>
    /// and exposes the cumulative total count (the same shape as the regression
    /// suite's listener — a private test helper, duplicated rather than shared to
    /// keep each suite self-contained).
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

    private sealed class Harness : IDisposable
    {
        public Harness(TimeSpan? validity = null)
        {
            Clock = new FakeTimeProvider(SR_Base);
            Issuer = new FakeWorkloadCertificateIssuer(Clock, validity: validity);
            Cache = new WorkloadLeafCache();
            Client = new WorkloadLeafClient(
                Issuer, Cache, NullLogger<WorkloadLeafClient>.Instance, Clock);
        }

        public FakeTimeProvider Clock { get; }

        public FakeWorkloadCertificateIssuer Issuer { get; }

        public WorkloadLeafCache Cache { get; }

        public WorkloadLeafClient Client { get; }

        public void Dispose()
        {
            Client.Dispose();
            Cache.Dispose();
            Issuer.Dispose();
        }
    }
}
