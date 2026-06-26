// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafClient.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.WorkloadCertificate;

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using D2.Shared.Auth.Outbound.Telemetry;
using D2.Shared.Resilience.CircuitBreaker;
using D2.Shared.Resilience.Singleflight;
using D2.Shared.Result;
using D2.Shared.Utilities.Diagnostics;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using NodaTime;

/// <summary>
/// Refresh-ahead implementation of <see cref="IWorkloadLeafSource"/>. Reissues this
/// workload's leaf through the host-supplied <see cref="IWorkloadCertificateIssuer"/>,
/// builds a live private-key-bearing leaf + its issuing intermediate into a
/// presentable chain context, caches it in-memory until it nears expiry, and reissues
/// via the background <see cref="WorkloadLeafRefreshHostedService"/>. Concurrent callers
/// (on-demand + the refresh service) dedup to a single reissue via <c>Singleflight</c>;
/// a <c>CircuitBreaker</c> fast-fails after repeated issuer-unreachable failures; a
/// still-valid cached leaf is served stale while a reissue is attempted.
/// </summary>
/// <remarks>
/// The refresh-ahead loop keeps <see cref="WorkloadLeafCache"/> holding a current
/// chain. The gRPC presentation path (<c>AddD2WorkloadCertificate</c>) reads that
/// chain context at CHANNEL BUILD, not per-connection — so a consumer holding a
/// long-lived channel adopts a rotated leaf only by rebuilding the channel.
/// Rebuilding a long-lived channel on rotation is the consumer's responsibility.
/// </remarks>
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

        // 5 consecutive transient-failure ReissueResults → 30 s open (the shared
        // AuthOutboundResilienceDefaults thresholds). Value-based predicate required
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
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed), this);

        var now = Instant.FromDateTimeOffset(r_clock.GetUtcNow());
        var cached = r_cache.TryGet(now);

        if (cached is not null)
        {
            r_logger.WorkloadLeafCacheHit(cached.NotAfter.ToDateTimeOffset());
            return D2Result<X509Certificate2>.Ok(cached.Leaf);
        }

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
            var current = r_cache.TryGet(Instant.FromDateTimeOffset(r_clock.GetUtcNow()));

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
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed), this);

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
        Volatile.Write(ref _disposed, true);
    }

    /// <summary>
    /// Builds a live, private-key-bearing leaf <see cref="X509Certificate2"/> from raw
    /// issuance material and zeroes the PKCS#8 buffer once the cert owns the key.
    /// Mirrors the issuance handler's ephemeral-key import path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>CopyWithPrivateKey</c> over an in-memory <see cref="ECDsa"/> yields an
    /// EPHEMERAL-key certificate. Linux/OpenSSL — the deployment target — presents an
    /// ephemeral-key leaf for TLS client authentication without issue, so that path is
    /// left exactly as-is.
    /// </para>
    /// <para>
    /// Windows Schannel, however, refuses an ephemeral-key certificate for client
    /// authentication (the handshake fails with <c>0x8009030E</c> — "No credentials
    /// are available in the security package"). The Windows-only branch therefore
    /// round-trips the leaf through PKCS#12 and re-imports WITHOUT
    /// <see cref="X509KeyStorageFlags.EphemeralKeySet"/> (the very flag Schannel
    /// rejects) and WITHOUT <see cref="X509KeyStorageFlags.PersistKeySet"/>, so the
    /// key lands in a Schannel-usable perishable key container that is deleted when
    /// the returned <see cref="X509Certificate2"/> is disposed — the cache disposes
    /// the superseded / current leaf, so no key container leaks across a refresh.
    /// </para>
    /// </remarks>
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

        if (!OperatingSystem.IsWindows())
            return certOnly.CopyWithPrivateKey(ecdsa);

        // Windows-only: re-home the ephemeral key into a Schannel-usable perishable
        // key container via a PKCS#12 round-trip. The intermediate ephemeral cert is
        // disposed and the PKCS#12 buffer (which carries the private key) is zeroed.
        using var ephemeralLeaf = certOnly.CopyWithPrivateKey(ecdsa);
        var pfx = ephemeralLeaf.Export(X509ContentType.Pkcs12);

        try
        {
            // Exportable == no EphemeralKeySet (Schannel-usable) AND no PersistKeySet
            // (the temporary key container is removed when the cert is disposed).
            return X509CertificateLoader.LoadPkcs12(
                pfx,
                password: null,
                keyStorageFlags: X509KeyStorageFlags.Exportable);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }
    }

    /// <summary>
    /// Builds the cached snapshot from raw issuance material: the live leaf
    /// (<see cref="BuildLiveLeaf"/>), the public issuing intermediate, and — where the
    /// platform supports it — the pre-built <see cref="SslStreamCertificateContext"/>
    /// carrying the full <c>leaf → intermediate</c> chain. Presenting the chain (not
    /// the bare leaf) is what lets a strict peer's root-anchored chain rebuild complete
    /// without a machine-store-resident intermediate or a network (AIA) fetch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The intermediate is public (no private key), so it carries no key-container
    /// concern; the leaf carries the secret key. The context holds references to both
    /// certs — they MUST stay alive while the context is presentable, so the snapshot
    /// owns them and the cache disposes both only on swap/dispose.
    /// </para>
    /// <para>
    /// <c>offline: true</c> keeps the chain build store-only — the client never reaches
    /// out to the network for missing issuer certificates. On Linux/OpenSSL (the
    /// deployment target) the context is built from the provided leaf + intermediate
    /// and the full chain is presented on the handshake. On Windows the chain is built
    /// by Schannel outside the process, which refuses to construct a chain context for
    /// a leaf whose internal-CA root is not installed in the OS trust store; that build
    /// throws a <see cref="System.Security.Cryptography.CryptographicException"/>, which
    /// is tolerated here (a null context → bare-leaf presentation) because Windows
    /// cannot transmit an application-supplied intermediate regardless. A Windows host
    /// that needs the chain transmitted installs the CA into the OS store (operator
    /// action), after which this same build succeeds and the chain is presented.
    /// </para>
    /// </remarks>
    /// <param name="material">The raw issuance material (its PKCS#8 buffer is zeroed inside <see cref="BuildLiveLeaf"/>).</param>
    /// <returns>The snapshot to publish into the cache.</returns>
    private static WorkloadLeafSnapshot BuildSnapshot(WorkloadLeafMaterial material)
    {
        var leaf = BuildLiveLeaf(material);

        var intermediate = X509CertificateLoader.LoadCertificate(material.IssuerCertificateDer);

        var chainContext = TryBuildChainContext(leaf, intermediate);

        return new WorkloadLeafSnapshot(leaf, intermediate, chainContext, material.NotAfter);
    }

    /// <summary>
    /// Builds the presentable <c>leaf → intermediate</c> chain context, tolerating the
    /// Windows-Schannel case where the context cannot be constructed for an
    /// uninstalled-internal-CA-root leaf.
    /// </summary>
    /// <remarks>
    /// On Linux/OpenSSL this always returns a context (the deployment path). On Windows
    /// it returns null when Schannel rejects the chain build (root not in the OS trust
    /// store) — the only Windows-functional fallback is the bare leaf, since Schannel
    /// will not transmit an application-supplied intermediate without store residency.
    /// </remarks>
    /// <param name="leaf">The live private-key-bearing leaf.</param>
    /// <param name="intermediate">The public issuing intermediate.</param>
    /// <returns>The chain context, or null on the Windows store-dependent path.</returns>
    private static SslStreamCertificateContext? TryBuildChainContext(
        X509Certificate2 leaf, X509Certificate2 intermediate)
    {
        try
        {
            return SslStreamCertificateContext.Create(leaf, [intermediate], offline: true);
        }
        catch (CryptographicException) when (OperatingSystem.IsWindows())
        {
            // Windows-only: Schannel cannot build a chain context for a leaf whose
            // internal-CA root is not in the OS trust store, and cannot transmit an
            // application-supplied intermediate regardless. Fall back to presenting the
            // bare leaf (the prior Windows-functional behavior); Linux is unaffected.
            return null;
        }
    }

    private async ValueTask<ReissueResult> ReissueAsync(CancellationToken ct)
    {
        // Cache re-check: a sibling caller may have populated the cache between the
        // GetCurrentLeafAsync TryGet and the Singleflight entry. Without this
        // re-check we'd issue a redundant reissue right after a peer just refreshed.
        var preReissueCache = r_cache.TryGet(Instant.FromDateTimeOffset(r_clock.GetUtcNow()));

        if (preReissueCache is not null)
            return ReissueResult.Successful();

        try
        {
            var issuance = await r_issuer.IssueAsync(ct);

            if (!issuance.Success || issuance.Data is null)
                return ReissueResult.TransientFailure();

            r_cache.Set(BuildSnapshot(issuance.Data));

            return ReissueResult.Successful();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Serve-stale contract: ANY exception building the snapshot — the live
            // certificate (CryptographicException, ArgumentException,
            // InvalidOperationException from CopyWithPrivateKey algorithm mismatch),
            // decoding the intermediate DER, or constructing the chain context
            // (NotSupportedException if the leaf lacks a private key) — is treated as
            // transient: the cached leaf keeps being served while the next reissue
            // cycle may succeed. OperationCanceledException propagates above.
            // Sanitize: never log ex itself (a cert-parse exception could echo
            // subject / SAN content). Type FullName + first frame are safe.
            // The cached leaf's not-after is captured as a structured log field
            // (ISO-8601 UTC) so operators can correlate failures with expiry proximity.
            // It cannot be a metric tag (timestamps are high-cardinality, not enumerable
            // dimensions); the log record is the correct OTel home for this value.
            var staleCached = r_cache.PeekRaw();
            var cachedLeafNotAfter = staleCached is not null
                ? staleCached.NotAfter.ToDateTimeOffset().ToString("O")
                : "none";

            r_logger.WorkloadLeafReissueFailed(
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex),
                cachedLeafNotAfter);

            OutboundTelemetry.SR_LeafReissueFailures.Add(1);

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
