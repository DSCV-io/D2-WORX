// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafClient.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Outbound.WorkloadCertificate;

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using DcsvIo.D2.Auth.Outbound.Telemetry;
using DcsvIo.D2.Resilience.CircuitBreaker;
using DcsvIo.D2.Resilience.Singleflight;
using DcsvIo.D2.Result;
using DcsvIo.D2.Utilities.Diagnostics;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using NodaTime;

/// <summary>
/// Refresh-ahead implementation of <see cref="IWorkloadLeafSource"/>. Generates a
/// FRESH ECDSA P-256 keypair per reissue, builds a PKCS#10 certificate-signing
/// request, obtains a signed leaf through the host-supplied
/// <see cref="IWorkloadCertificateIssuer"/>, pairs the returned certificate with
/// the LOCAL private key, builds the leaf + issuing intermediate into a presentable
/// chain context, caches it in-memory until it nears expiry, and reissues via the
/// background <see cref="WorkloadLeafRefreshHostedService"/>. Concurrent callers
/// (on-demand + the refresh service) dedup to a single reissue via
/// <c>Singleflight</c>; a <c>CircuitBreaker</c> fast-fails after repeated
/// issuer-unreachable failures; a still-valid cached leaf is served stale while a
/// reissue is attempted.
/// </summary>
/// <remarks>
/// <para>
/// <b>The private key never crosses the issuer seam.</b> The keypair is generated
/// inside this client (fresh per reissue — per-rotation key freshness), the CSR
/// carries only public material, and the issuer returns only certificates. The
/// CSR subject is a fixed placeholder: the issuer structurally ignores it (the
/// leaf's subject-alternative-name is the issuer's authenticated view of this
/// workload), so the client deliberately carries NO identity configuration.
/// </para>
/// <para>
/// <b>Mismatch defense.</b> A returned leaf whose public key does not match the
/// local keypair can never be presented (there is no private key for it), so it
/// is rejected before any cache write — the still-valid cached leaf keeps serving
/// and the reissue counts as a transient failure.
/// </para>
/// <para>
/// The refresh-ahead loop keeps <see cref="WorkloadLeafCache"/> holding a current
/// chain. The gRPC presentation path (<c>AddD2WorkloadCertificate</c>) reads that
/// chain context at CHANNEL BUILD, not per-connection — so a consumer holding a
/// long-lived channel adopts a rotated leaf only by rebuilding the channel.
/// Rebuilding a long-lived channel on rotation is the consumer's responsibility.
/// </para>
/// </remarks>
[MustDisposeResource(false)]
internal sealed class WorkloadLeafClient : IWorkloadLeafSource, IDisposable
{
    // Singleflight key — there is exactly one "reissue this workload's leaf"
    // operation per process, so the key is a constant. Multiple concurrent callers
    // (on-demand + the refresh hosted service) all dedup to one reissue.
    private const string _SINGLEFLIGHT_KEY = "workload-leaf";

    // Fixed CSR subject placeholder. The issuer structurally ignores the CSR's
    // subject (the SAN authority is its authenticated peer view), so the client
    // cannot and need not name itself — no identity knob exists by design.
    private const string _CSR_SUBJECT = "CN=d2-workload";

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
                innerCt => r_circuitBreaker.ExecuteAsync(
                    breakerCt => ReissueAsync(force: false, breakerCt), ct: innerCt),
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
    /// Goes through the same singleflight as on-demand callers, but — unlike the
    /// opportunistic on-demand path — mints a FRESH leaf even when a still-valid
    /// leaf is cached: this is the refresh-ahead entry point, and it fires precisely
    /// while the current leaf is valid but inside the lead-time window, so it MUST
    /// reissue rather than short-circuit on the still-valid cache.
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
                innerCt => r_circuitBreaker.ExecuteAsync(
                    breakerCt => ReissueAsync(force: true, breakerCt), ct: innerCt),
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
    /// Builds a live, private-key-bearing leaf <see cref="X509Certificate2"/> by
    /// pairing the issuer-returned certificate with the LOCALLY-generated keypair.
    /// The key was never received and never transmitted — there is no received
    /// secret buffer to import or zero.
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
    /// The PKCS#12 buffer (which carries the private key) is zeroed.
    /// </para>
    /// </remarks>
    /// <param name="material">The issuer-returned certificate material (all public).</param>
    /// <param name="localKey">The locally-generated keypair the leaf certifies.</param>
    /// <returns>The live leaf certificate.</returns>
    private static X509Certificate2 BuildLiveLeaf(WorkloadLeafMaterial material, ECDsa localKey)
    {
        using var certOnly = X509CertificateLoader.LoadCertificate(material.CertificateDer);

        if (!OperatingSystem.IsWindows())
            return certOnly.CopyWithPrivateKey(localKey);

        // Windows-only: re-home the ephemeral key into a Schannel-usable perishable
        // key container via a PKCS#12 round-trip. The intermediate ephemeral cert is
        // disposed and the PKCS#12 buffer (which carries the private key) is zeroed.
        using var ephemeralLeaf = certOnly.CopyWithPrivateKey(localKey);
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
    /// Builds the cached snapshot: the live leaf (<see cref="BuildLiveLeaf"/> —
    /// paired with the local keypair), the public issuing intermediate, and — where
    /// the platform supports it — the pre-built
    /// <see cref="SslStreamCertificateContext"/> carrying the full
    /// <c>leaf → intermediate</c> chain. Presenting the chain (not the bare leaf)
    /// is what lets a strict peer's root-anchored chain rebuild complete without a
    /// machine-store-resident intermediate or a network (AIA) fetch.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The intermediate is public (no private key), so it carries no key-container
    /// concern; the leaf carries the locally-generated secret key. The context holds
    /// references to both certs — they MUST stay alive while the context is
    /// presentable, so the snapshot owns them and the cache disposes both only on
    /// swap/dispose.
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
    /// <param name="material">The issuer-returned certificate material (all public).</param>
    /// <param name="localKey">The locally-generated keypair the leaf certifies.</param>
    /// <returns>The snapshot to publish into the cache.</returns>
    private static WorkloadLeafSnapshot BuildSnapshot(
        WorkloadLeafMaterial material, ECDsa localKey)
    {
        var leaf = BuildLiveLeaf(material, localKey);

        X509Certificate2? intermediate = null;

        try
        {
            intermediate = X509CertificateLoader.LoadCertificate(material.IssuerCertificateDer);

            var chainContext = TryBuildChainContext(leaf, intermediate);

            return new WorkloadLeafSnapshot(leaf, intermediate, chainContext, material.NotAfter);
        }
        catch
        {
            // A malformed intermediate DER (LoadCertificate) — or a non-Windows chain-build
            // failure — throws AFTER the live leaf exists; the leaf carries the secret key,
            // so an un-guarded throw here leaks its Schannel key-container handle. Dispose
            // both owned handles on the error path, then rethrow so ReissueAsync's serve-
            // stale catch treats it as transient (the cached leaf keeps serving).
            leaf.Dispose();
            intermediate?.Dispose();

            throw;
        }
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

    /// <summary>
    /// Returns whether the returned leaf certifies the LOCAL keypair — its
    /// SubjectPublicKeyInfo must equal the local key's, byte for byte.
    /// </summary>
    /// <param name="certificateDer">The issuer-returned leaf certificate DER.</param>
    /// <param name="localKey">The locally-generated keypair.</param>
    /// <returns><see langword="true"/> when the leaf's public key matches the local key.</returns>
    private static bool LeafMatchesLocalKey(byte[] certificateDer, ECDsa localKey)
    {
        using var leaf = X509CertificateLoader.LoadCertificate(certificateDer);
        var leafSpki = leaf.PublicKey.ExportSubjectPublicKeyInfo();
        var localSpki = localKey.ExportSubjectPublicKeyInfo();

        return leafSpki.AsSpan().SequenceEqual(localSpki);
    }

    private async ValueTask<ReissueResult> ReissueAsync(bool force, CancellationToken ct)
    {
        // Opportunistic-path cache re-check (force == false ONLY). A sibling on-demand
        // caller may have populated the cache between the GetCurrentLeafAsync TryGet and
        // this Singleflight body; suppressing a redundant mint there is correct — an
        // on-demand caller only needs SOME non-expired leaf, and one now exists.
        //
        // The refresh-ahead FORCE path deliberately SKIPS this suppression. It fires
        // precisely when the cached leaf is still valid but inside the lead-time window,
        // so a "still-valid cached leaf" is exactly the state in which it MUST proceed to
        // mint a fresh leaf ahead of expiry; short-circuiting here would defeat
        // refresh-ahead entirely (the leaf would only ever reissue at/after expiry,
        // stalling on-demand callers on a synchronous mint on the hot path).
        //
        // Concurrency: both paths share one Singleflight key, so a force + on-demand race
        // dedups to a single mint (no double-mint; the cache's Interlocked swap makes the
        // publish torn-read-free). A valid-cache on-demand caller never enters this body
        // (it returns the cached leaf at the outer GetCurrentLeafAsync TryGet), so a force
        // follower can never be silently suppressed by a non-force Singleflight leader.
        if (!force)
        {
            var preReissueCache = r_cache.TryGet(Instant.FromDateTimeOffset(r_clock.GetUtcNow()));

            if (preReissueCache is not null)
                return ReissueResult.Successful();
        }

        try
        {
            // 1) Fresh keypair per reissue — the workload owns its key lifecycle;
            //    rotation freshness holds because a new key is minted every cycle.
            using var localKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

            // 2) Build the PKCS#10 CSR (public key + placeholder subject +
            //    self-signature — public material by construction).
            var csrRequest = new CertificateRequest(
                _CSR_SUBJECT, localKey, HashAlgorithmName.SHA256);

            var csrDer = csrRequest.CreateSigningRequest();

            // 3) Obtain the signed leaf. Only the CSR crosses the seam.
            var issuance = await r_issuer.IssueAsync(csrDer, ct);

            if (!issuance.Success || issuance.Data is null)
                return ReissueResult.TransientFailure();

            // 4) Mismatch defense: a leaf certifying a DIFFERENT key than the local
            //    one can never be presented (no private key exists for it) — reject
            //    BEFORE any cache write; the still-valid cached leaf keeps serving.
            if (!LeafMatchesLocalKey(issuance.Data.CertificateDer, localKey))
            {
                r_logger.WorkloadLeafIssuerKeyMismatch();
                OutboundTelemetry.SR_LeafReissueFailures.Add(1);
                return ReissueResult.TransientFailure();
            }

            // 5) Pair the returned certificate with the local key and publish. The
            //    local handle is disposed by the using once the live cert owns the
            //    key (CopyWithPrivateKey duplicates it into the certificate).
            r_cache.Set(BuildSnapshot(issuance.Data, localKey));

            return ReissueResult.Successful();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Serve-stale contract: ANY exception generating the keypair, building
            // the CSR, or building the snapshot — the live certificate
            // (CryptographicException, ArgumentException, InvalidOperationException
            // from CopyWithPrivateKey algorithm mismatch), decoding the intermediate
            // DER, or constructing the chain context (NotSupportedException if the
            // leaf lacks a private key) — is treated as transient: the cached leaf
            // keeps being served while the next reissue cycle may succeed.
            // OperationCanceledException propagates above.
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
