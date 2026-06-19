// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.WorkloadCertificate;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using D2.Shared.Auth.Outbound.Telemetry;
using D2.Shared.Resilience.CircuitBreaker;
using D2.Shared.Resilience.Singleflight;
using D2.Shared.Result;
using D2.Shared.Utilities.Diagnostics;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

/// <summary>
/// Refresh-ahead implementation of <see cref="IWorkloadLeafSource"/>. Reissues this
/// workload's leaf through the host-supplied <see cref="IWorkloadCertificateIssuer"/>,
/// builds a live private-key-bearing <see cref="X509Certificate2"/>, caches it
/// in-memory until it nears expiry, and reissues via the background
/// <see cref="WorkloadLeafRefreshHostedService"/>. Mirrors
/// <c>HttpServiceIdentityClient</c>'s singleflight + circuit-breaker + serve-stale
/// shape, swapping the OAuth token-fetch for a certificate reissue.
/// </summary>
[MustDisposeResource(false)]
internal sealed class WorkloadLeafClient : IWorkloadLeafSource, IDisposable
{
    // Singleflight key — there is exactly one "reissue this workload's leaf"
    // operation per process, so the key is a constant. Multiple concurrent callers
    // (on-demand + the refresh hosted service) all dedup to one reissue.
    private const string _SINGLEFLIGHT_KEY = "workload-leaf";

    private readonly IWorkloadCertificateIssuer r_issuer;
    private readonly WorkloadLeafCache r_cache;
    private readonly ILogger<WorkloadLeafClient> r_logger;
    private readonly TimeProvider r_clock;
    private readonly Singleflight<string, ReissueResult> r_singleflight = new();
    private readonly CircuitBreaker<ReissueResult> r_circuitBreaker;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkloadLeafClient"/> class.
    /// </summary>
    /// <param name="issuer">The host-supplied leaf issuer (in-process / harness adapter).</param>
    /// <param name="cache">The shared per-process live-leaf cache.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="clock">The time provider (overridable for tests).</param>
    [MustDisposeResource(false)]
    public WorkloadLeafClient(
        IWorkloadCertificateIssuer issuer,
        WorkloadLeafCache cache,
        ILogger<WorkloadLeafClient> logger,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);

        r_issuer = issuer;
        r_cache = cache;
        r_logger = logger;
        r_clock = clock;

        // 5 consecutive transient-failure ReissueResults → 30 s open, matching the
        // service-identity client breaker config. Value-based predicate required
        // because ReissueAsync catches exceptions internally and returns
        // ReissueResult.TransientFailure().
        r_circuitBreaker = new CircuitBreaker<ReissueResult>(
            isFailure: static r => !r.Success,
            options: new CircuitBreakerOptions(
                failureThreshold: AuthOutboundResilienceDefaults.FAILURE_THRESHOLD,
                cooldownDuration: AuthOutboundResilienceDefaults.SR_CooldownDuration,
                nowFunc: () => clock.GetUtcNow().ToUnixTimeMilliseconds()));
    }

    /// <inheritdoc/>
    public async ValueTask<D2Result<X509Certificate2>> GetCurrentLeafAsync(
        CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var now = r_clock.GetUtcNow();
        var cached = r_cache.TryGet(now);

        if (cached is not null)
            return D2Result<X509Certificate2>.Ok(cached.Leaf);

        // Cache miss / expired → reissue via singleflight (outer) wrapping circuit
        // breaker (inner). Singleflight deduplicates concurrent callers; the breaker
        // fast-fails when the issuer has been repeatedly unreachable.
        try
        {
            var reissueResult = await r_singleflight.ExecuteAsync(
                _SINGLEFLIGHT_KEY,
                innerCt => r_circuitBreaker.ExecuteAsync(ReissueAsync, ct: innerCt),
                ct);

            if (!reissueResult.Success)
                return D2Result<X509Certificate2>.ServiceUnavailable();

            // A sibling may have published a fresher snapshot; read through the
            // cache so all callers converge on the one live handle.
            var current = r_cache.TryGet(r_clock.GetUtcNow());

            if (current is not null)
                return D2Result<X509Certificate2>.Ok(current.Leaf);

            return D2Result<X509Certificate2>.ServiceUnavailable();
        }
        catch (CircuitOpenException)
        {
            return D2Result<X509Certificate2>.ServiceUnavailable();
        }
    }

    /// <summary>
    /// Forces a reissue of the cached leaf. Called by
    /// <see cref="WorkloadLeafRefreshHostedService"/> on the proactive schedule.
    /// Goes through the same singleflight as on-demand callers.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="D2Result"/> describing the reissue outcome.</returns>
    public async ValueTask<D2Result> ForceReissueAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var reissueResult = await r_singleflight.ExecuteAsync(
                _SINGLEFLIGHT_KEY,
                innerCt => r_circuitBreaker.ExecuteAsync(ReissueAsync, ct: innerCt),
                ct);

            return reissueResult.Success
                ? D2Result.Ok()
                : D2Result.ServiceUnavailable();
        }
        catch (CircuitOpenException)
        {
            return D2Result.ServiceUnavailable();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
    }

    /// <summary>
    /// Builds a live, private-key-bearing <see cref="X509Certificate2"/> from raw
    /// issuance material and zeroes the PKCS#8 buffer once the cert owns the key.
    /// Mirrors the issuance handler's ephemeral-key import path.
    /// </summary>
    /// <param name="material">The raw issuance material.</param>
    /// <returns>The live leaf certificate.</returns>
    private static X509Certificate2 BuildLiveLeaf(WorkloadLeafMaterial material)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(material.PrivateKeyPkcs8, out _);

        // The leaf private key is SECRET — zero the PKCS#8 buffer once the live
        // certificate's key handle owns the material.
        CryptographicOperations.ZeroMemory(material.PrivateKeyPkcs8);

        using var certOnly = X509CertificateLoader.LoadCertificate(material.CertificateDer);

        return certOnly.CopyWithPrivateKey(ecdsa);
    }

    private async ValueTask<ReissueResult> ReissueAsync(CancellationToken ct)
    {
        // Cache re-check: a sibling caller may have populated the cache between the
        // GetCurrentLeafAsync TryGet and the Singleflight entry. Without this
        // re-check we'd issue a redundant reissue right after a peer just refreshed.
        var preReissueCache = r_cache.TryGet(r_clock.GetUtcNow());

        if (preReissueCache is not null)
            return ReissueResult.Successful();

        try
        {
            var issuance = await r_issuer.IssueAsync(ct);

            if (!issuance.Success || issuance.Data is null)
                return ReissueResult.TransientFailure();

            var leaf = BuildLiveLeaf(issuance.Data);
            r_cache.Set(new WorkloadLeafSnapshot(leaf, issuance.Data.NotAfter));

            return ReissueResult.Successful();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Serve-stale contract: ANY exception building the live certificate
            // (CryptographicException, ArgumentException, InvalidOperationException
            // from CopyWithPrivateKey algorithm mismatch, etc.) is treated as
            // transient — the cached leaf keeps being served while the next reissue
            // cycle may succeed. OperationCanceledException propagates above.
            // Sanitize: never log ex itself (a cert-parse exception could echo
            // subject / SAN content). Type FullName + first frame are safe.
            r_logger.WorkloadLeafReissueFailed(
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex));

            return ReissueResult.TransientFailure();
        }
    }

    /// <summary>
    /// Reissue outcome. <c>internal</c> (not <c>private</c>) so the assembly-level
    /// <see cref="CircuitBreaker{T}"/> generic can be instantiated with this type
    /// from within the same assembly.
    /// </summary>
    internal readonly record struct ReissueResult(bool Success)
    {
        /// <summary>Returns a successful reissue outcome.</summary>
        public static ReissueResult Successful() => new(true);

        /// <summary>Returns a transient-failure reissue outcome.</summary>
        public static ReissueResult TransientFailure() => new(false);
    }
}
