// -----------------------------------------------------------------------
// <copyright file="InProcessJwksProvider.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Jwks;

using System.Diagnostics;
using D2.Shared.Auth.Abstractions.Jwks;
using D2.Shared.Resilience.Singleflight;
using D2.Shared.Result;
using D2.Shared.Utilities.Diagnostics;
using D2.Shared.Utilities.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Issuer-host <see cref="IJwksProvider"/> that loads Active + Retiring
/// <c>jwks-signing</c> RSA public keys from the KeyCustodian store — the same
/// domain filters and SPKI projection as <c>GetJwksHandler</c> — without an
/// HTTP self-fetch to the Issuer's well-known endpoints.
/// </summary>
/// <remarks>
/// <para>
/// Registered only on the Edge (issuer) composition root via
/// <see cref="InProcessJwksProviderServiceCollectionExtensions.AddD2InProcessJwksProvider"/>,
/// which replaces the <see cref="IJwksProvider"/> interface registration after
/// <c>AddD2Auth</c>. Remote consumers (Audit, etc.) keep
/// <c>HttpJwksProvider</c> + private-CA trust
/// (<c>JwksProviderOptions.TrustedRootCertificatePath</c>). Well-known HTTP
/// routes remain for those consumers.
/// </para>
/// <para>
/// Process-local snapshot cache; <see cref="RefreshAsync"/> reloads from the
/// DB under Singleflight + cooldown so
/// <c>JwksBackplaneSubscriber</c> rotation events and reactive unknown-kid
/// paths stay stampede-safe. Empty signing-key store → fail-secure
/// <c>ServiceUnavailable</c> (same posture as <c>GetJwksHandler</c>).
/// </para>
/// </remarks>
internal sealed class InProcessJwksProvider : IJwksProvider
{
    private const string _SINGLEFLIGHT_KEY = "force-refresh";

    /// <summary>
    /// Matches <c>JwksProviderOptions</c> default refresh cooldown so issuer-host
    /// reactive refresh does not stampede the DB under sustained unknown-kid load.
    /// </summary>
    private static readonly TimeSpan sr_RefreshCooldown = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory r_scopeFactory;
    private readonly IOptions<KeyCustodianOptions> r_options;
    private readonly ILogger<InProcessJwksProvider> r_logger;
    private readonly TimeProvider r_clock;
    private readonly Singleflight<string, D2Result> r_singleflight = new();

    // Volatile so concurrent GetKeysAsync sees the latest snapshot without a lock.
    // Writes happen under Singleflight (at most one refresh at a time).
    private JwksKeySetSnapshot? _snapshot;
    private long _lastRefreshTicks;

    /// <summary>
    /// Initializes a new instance of the <see cref="InProcessJwksProvider"/> class.
    /// </summary>
    /// <param name="scopeFactory">Creates scopes for scoped <see cref="IKeyCustodianDbContext"/>.</param>
    /// <param name="options">KeyCustodian options (IssuerBaseUrl for telemetry SourceUri).</param>
    /// <param name="logger">The logger.</param>
    /// <param name="clock">The time provider (overridable for tests).</param>
    public InProcessJwksProvider(
        IServiceScopeFactory scopeFactory,
        IOptions<KeyCustodianOptions> options,
        ILogger<InProcessJwksProvider> logger,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);

        r_scopeFactory = scopeFactory;
        r_options = options;
        r_logger = logger;
        r_clock = clock;
    }

    /// <inheritdoc/>
    public async ValueTask<D2Result<JwksKeySetSnapshot>> GetKeysAsync(
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var cached = Volatile.Read(ref _snapshot);

        if (cached is not null && cached.Keys.Count > 0)
            return D2Result<JwksKeySetSnapshot>.Ok(cached);

        var refresh = await RefreshAsync(ct).ConfigureAwait(false);

        if (refresh.Failed)
            return D2Result<JwksKeySetSnapshot>.ServiceUnavailable();

        cached = Volatile.Read(ref _snapshot);

        if (cached is null || cached.Keys.Count == 0)
            return D2Result<JwksKeySetSnapshot>.ServiceUnavailable();

        return D2Result<JwksKeySetSnapshot>.Ok(cached);
    }

    /// <inheritdoc/>
    public async ValueTask<D2Result> RefreshAsync(CancellationToken ct = default)
    {
        return await r_singleflight.ExecuteAsync(_SINGLEFLIGHT_KEY, RunRefreshAsync, ct)
            .ConfigureAwait(false);
    }

    private async ValueTask<D2Result> RunRefreshAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var nowTicks = r_clock.GetUtcNow().Ticks;
        var lastTicks = Interlocked.Read(ref _lastRefreshTicks);

        if (lastTicks > 0)
        {
            var elapsed = TimeSpan.FromTicks(nowTicks - lastTicks);

            if (elapsed < sr_RefreshCooldown)
                return D2Result.Ok();
        }

        var sw = Stopwatch.StartNew();

        try
        {
            var snapshot = await LoadSnapshotAsync(ct).ConfigureAwait(false);

            if (snapshot is null || snapshot.Keys.Count == 0)
            {
                Volatile.Write(ref _snapshot, null);
                InProcessJwksLog.EmptySigningKeyStore(r_logger);

                return D2Result.ServiceUnavailable();
            }

            Volatile.Write(ref _snapshot, snapshot);
            Interlocked.Exchange(ref _lastRefreshTicks, nowTicks);
            InProcessJwksLog.RefreshSucceeded(
                r_logger,
                snapshot.Keys.Count,
                snapshot.SourceUri.ToString(),
                (long)sw.Elapsed.TotalMilliseconds);

            return D2Result.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            InProcessJwksLog.RefreshFailed(
                r_logger,
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex));

            return D2Result.ServiceUnavailable();
        }
    }

    private async Task<JwksKeySetSnapshot?> LoadSnapshotAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // DbContext is scoped — open a short-lived scope for the read.
        await using var scope = r_scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IKeyCustodianDbContext>();

        // Same domain filters / ordering as GetJwksHandler (Active first, then Retiring).
        var rows = await db.Keys
            .AsNoTracking()
            .ForDomain(KeyDomain.JWKS_SIGNING)
            .Signing()
            .Where(k => k.Status == KeyStatus.Active || k.Status == KeyStatus.Retiring)
            .OrderBy(k => k.Status == KeyStatus.Active ? 0 : 1)
            .ThenByDescending(k => k.ActivatedAt)
            .Select(k => new { k.Kid, k.PublicKeyMaterial })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var keys = new Dictionary<string, SecurityKey>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            // A signing key always carries SPKI public material (domain invariant);
            // a null here is a corrupt row — skip rather than emit a broken key.
            if (row.PublicKeyMaterial is not { } spki)
                continue;

            var securityKey = ToVerifyKey(row.Kid, spki);

            if (securityKey is not null)
                keys[row.Kid] = securityKey;
        }

        if (keys.Falsey())
            return null;

        return new JwksKeySetSnapshot
        {
            Keys = keys,
            FetchedAt = r_clock.GetUtcNow(),
            SourceUri = BuildSourceUri(r_options.Value.IssuerBaseUrl),
        };

        static SecurityKey? ToVerifyKey(string kid, byte[] publicSpki)
        {
            if (kid.Falsey() || publicSpki.Length == 0)
                return null;

            try
            {
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(publicSpki, out _);
                var parameters = rsa.ExportParameters(includePrivateParameters: false);

                return new RsaSecurityKey(parameters) { KeyId = kid };
            }
            catch (CryptographicException)
            {
                return null;
            }
        }

        static Uri BuildSourceUri(string issuerBaseUrl)
        {
            if (issuerBaseUrl.Falsey())
                return new Uri("https://issuer.invalid/.well-known/jwks.json");

            var baseUri = new Uri(
                issuerBaseUrl.TrimEnd('/') + "/",
                UriKind.Absolute);

            return new Uri(baseUri, ".well-known/jwks.json");
        }
    }
}
