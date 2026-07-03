// -----------------------------------------------------------------------
// <copyright file="FakeWorkloadCertificateIssuer.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.WorkloadCertificate;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Channels;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using D2.Shared.Result;
using D2.Shared.Tests.Unit.Mtls;
using NodaTime;

/// <summary>
/// Test issuer that signs REAL CSR-flow leaf material from a self-contained
/// <see cref="TestCertificateAuthority"/>, or returns a transient failure when
/// armed to. Asserts the real seam contract: the CSR is loaded with
/// proof-of-possession validation ON (a malformed or PoP-broken CSR fails the test
/// at the seam), the CSR's subject is IGNORED (the SAN is minted from this fake's
/// configured serviceId — exactly what the real issuer does), and only
/// certificates are returned. Tracks the issuance count so tests can assert
/// reissue-before-expiry + singleflight dedup, and captures the received CSR + its
/// extracted public key for seam assertions.
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
    private bool _mintMismatchedKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeWorkloadCertificateIssuer"/> class.
    /// </summary>
    /// <param name="clock">
    /// The time provider — the returned material's <c>NotAfter</c> is computed
    /// relative to THIS clock so a <c>FakeTimeProvider</c> advance moves the leaf
    /// toward expiry (the cert's own X509 dates are real-time and irrelevant to the
    /// cache, which reads only <c>NotAfter</c>).
    /// </param>
    /// <param name="serviceId">The workload service id minted into each leaf's SAN (the issuer's peer view).</param>
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
    /// Gets the raw CSR DER received on the most-recent issuance. Tests assert it
    /// parses as a well-formed, PoP-valid PKCS#10 request (public material by
    /// construction — public key + metadata + self-signature, never a private key).
    /// </summary>
    public byte[]? LastReceivedCsrDer { get; private set; }

    /// <summary>
    /// Gets the SubjectPublicKeyInfo extracted from the most-recent received CSR —
    /// the key the returned leaf certifies. Tests compare it against the leaf's SPKI
    /// (and across rotations, assert freshness: two reissues carry different keys).
    /// </summary>
    public byte[]? LastCsrPublicKeySpki { get; private set; }

    /// <summary>Arms the issuer to fail (transiently) on the next issuance(s).</summary>
    /// <param name="fail">Whether subsequent issuances fail.</param>
    public void SetFail(bool fail) => _fail = fail;

    /// <summary>
    /// Arms the issuer to return a leaf minted over a DIFFERENT keypair than the
    /// CSR's — the adversarial shape driving the client's mismatch-reject defense.
    /// </summary>
    /// <param name="mismatch">Whether subsequent issuances return a mismatched-key leaf.</param>
    public void SetMintMismatchedKey(bool mismatch) => _mintMismatchedKey = mismatch;

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
    public ValueTask<D2Result<WorkloadLeafMaterial>> IssueAsync(
        byte[] csrDer, CancellationToken ct = default)
    {
        var count = Interlocked.Increment(ref _issuanceCount);

        // Signal synchronously — TryWrite on an unbounded channel never blocks or
        // fails (the channel is never closed during a test).
        _invocationChannel.Writer.TryWrite(count);

        if (_fail)
            return ValueTask.FromResult(D2Result<WorkloadLeafMaterial>.ServiceUnavailable());

        // Seam contract (a): the DEFAULT load options validate proof-of-possession
        // — a malformed or PoP-broken CSR THROWS here, failing the test at the seam.
        var csr = CertificateRequest.LoadSigningRequest(csrDer, HashAlgorithmName.SHA256);

        LastReceivedCsrDer = csrDer;
        LastCsrPublicKeySpki = csr.PublicKey.ExportSubjectPublicKeyInfo();

        // Seam contract (b)+(c): the CSR's subject is IGNORED — the SAN comes from
        // this fake's configured serviceId — and the CSR's public key is what the
        // leaf certifies (unless the mismatch knob is armed, which mints from a
        // fresh unrelated keypair to drive the client's mismatch-reject arm).
        byte[] certDer;
        byte[] issuerDer;

        if (_mintMismatchedKey)
        {
            (certDer, issuerDer) = r_ca.IssueLeafMaterial(r_serviceId, r_validity);
        }
        else
        {
            (certDer, issuerDer) = r_ca.SignLeafFromCsr(csrDer, r_serviceId, r_validity);
        }

        // The cache-relevant NotAfter tracks the injected (fake) clock so refresh-due
        // + expiry assertions are deterministic under FakeTimeProvider advances.
        var notAfter = Instant.FromDateTimeOffset(r_clock.GetUtcNow()) + Duration.FromTimeSpan(r_validity);

        return ValueTask.FromResult(D2Result<WorkloadLeafMaterial>.Ok(
            new WorkloadLeafMaterial(certDer, issuerDer, notAfter)));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _invocationChannel.Writer.TryComplete();
        r_ca.Dispose();
    }
}
