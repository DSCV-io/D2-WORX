// -----------------------------------------------------------------------
// <copyright file="FakeWorkloadCertificateIssuer.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.WorkloadCertificate;

using System.Threading.Channels;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using D2.Shared.Result;
using D2.Shared.Tests.Unit.Mtls;
using NodaTime;

/// <summary>
/// Test issuer that mints real leaf material from a self-contained
/// <see cref="TestCertificateAuthority"/>, or returns a transient failure when
/// armed to. Tracks the issuance count so tests can assert reissue-before-expiry +
/// singleflight dedup.
/// </summary>
internal sealed class FakeWorkloadCertificateIssuer : IWorkloadCertificateIssuer, IDisposable
{
    // Unbounded channel: each IssueAsync call writes the post-call count into it.
    // Tests read from this channel to get a deterministic "issuer was invoked N times"
    // signal — no polling, no wall-clock deadline.
    private readonly Channel<int> _invocationChannel =
        Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    private readonly TestCertificateAuthority r_ca = new();
    private readonly string r_serviceId;
    private readonly TimeSpan r_validity;
    private readonly TimeProvider r_clock;
    private int _issuanceCount;
    private bool _fail;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeWorkloadCertificateIssuer"/> class.
    /// </summary>
    /// <param name="clock">
    /// The time provider — the returned material's <c>NotAfter</c> is computed
    /// relative to THIS clock so a <c>FakeTimeProvider</c> advance moves the leaf
    /// toward expiry (the cert's own X509 dates are real-time and irrelevant to the
    /// cache, which reads only <c>NotAfter</c>).
    /// </param>
    /// <param name="serviceId">The workload service id minted into each leaf.</param>
    /// <param name="validity">The validity window each issued leaf carries.</param>
    public FakeWorkloadCertificateIssuer(
        TimeProvider clock, string serviceId = "edge", TimeSpan? validity = null)
    {
        r_clock = clock;
        r_serviceId = serviceId;
        r_validity = validity ?? TimeSpan.FromHours(24);
    }

    /// <summary>Gets the number of successful + attempted issuances.</summary>
    public int IssuanceCount => Volatile.Read(ref _issuanceCount);

    /// <summary>
    /// Gets the raw PKCS#8 byte-array reference from the most-recent successful
    /// issuance — the SAME array instance passed to <see cref="WorkloadLeafMaterial"/>
    /// and subsequently zeroed by <c>BuildLiveLeaf</c>. Tests assert
    /// <c>Array.TrueForAll(LastIssuedPkcs8!, b => b == 0)</c> after the leaf is built
    /// to verify the private-key-zeroize contract mechanically.
    /// </summary>
    public byte[]? LastIssuedPkcs8 { get; private set; }

    /// <summary>Arms the issuer to fail (transiently) on the next issuance(s).</summary>
    /// <param name="fail">Whether subsequent issuances fail.</param>
    public void SetFail(bool fail) => _fail = fail;

    /// <summary>
    /// Awaits until <see cref="IssueAsync"/> has been called at least
    /// <paramref name="targetCount"/> times in total (across the lifetime of this
    /// instance). Returns immediately if the count is already reached.
    /// Times out only on a genuine hang (the SUT never invokes <c>IssueAsync</c>
    /// — a true defect). No polling — driven by the production code's invocation.
    /// </summary>
    /// <param name="targetCount">The minimum cumulative call count to wait for.</param>
    /// <param name="timeout">
    /// Safety timeout — should be generous (default 30 s) so a slow CI runner never
    /// trips it on normal operation; it only fires when the SUT is genuinely stuck.
    /// </param>
    /// <param name="ct">Optional cancellation token.</param>
    public async Task WaitForInvocationCountAsync(
        int targetCount,
        TimeSpan? timeout = null,
        CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(30));

        // Fast-path: already there (e.g. the test advanced the clock before calling).
        if (Volatile.Read(ref _issuanceCount) >= targetCount)
            return;

        await foreach (var count in _invocationChannel.Reader.ReadAllAsync(cts.Token))
        {
            if (count >= targetCount)
                return;
        }
    }

    /// <inheritdoc/>
    public ValueTask<D2Result<WorkloadLeafMaterial>> IssueAsync(CancellationToken ct = default)
    {
        var count = Interlocked.Increment(ref _issuanceCount);

        // Signal synchronously — TryWrite on an unbounded channel never blocks or
        // fails (the channel is never closed during a test).
        _invocationChannel.Writer.TryWrite(count);

        if (_fail)
            return ValueTask.FromResult(D2Result<WorkloadLeafMaterial>.ServiceUnavailable());

        var (certDer, pkcs8, issuerDer) = r_ca.IssueLeafMaterial(r_serviceId, r_validity);

        // Retain the array REFERENCE (not a copy) so the zeroize-assertion test can
        // inspect the same buffer after BuildLiveLeaf zeroes it in-place.
        LastIssuedPkcs8 = pkcs8;

        // The cache-relevant NotAfter tracks the injected (fake) clock so refresh-due
        // + expiry assertions are deterministic under FakeTimeProvider advances.
        var notAfter = Instant.FromDateTimeOffset(r_clock.GetUtcNow()) + Duration.FromTimeSpan(r_validity);

        return ValueTask.FromResult(D2Result<WorkloadLeafMaterial>.Ok(
            new WorkloadLeafMaterial(certDer, pkcs8, issuerDer, notAfter)));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _invocationChannel.Writer.TryComplete();
        r_ca.Dispose();
    }
}
