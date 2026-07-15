// -----------------------------------------------------------------------
// <copyright file="SealKeyProvisioning.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Sealing;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Shared load-or-lazily-provision path for per-service ECDH sealing keys, consumed by both
/// seal ops (<c>getOrLazyProvisionSealPublicKey</c> / <c>getOrLazyProvisionOwnSealPrivateKey</c>) so provisioning is
/// identical on both planes. The first request for a service's <c>seal:&lt;serviceId&gt;</c>
/// domain generates, smoke-tests, and inline-activates one keypair on the spot; concurrent
/// first-requests safely converge on one winner.
/// </summary>
/// <remarks>
/// <para>
/// <b>Reused vs new.</b> The DOMAIN machinery is reused (<see cref="KeyGeneration"/>,
/// <see cref="SmokeTesting"/>, <see cref="PendingKey.Create"/> / <see cref="PendingKey.Activate"/>,
/// the one-Active EXCLUDE + one-Pending unique DB invariants). The APPLICATION path here —
/// back-dating <c>CreatedAt</c> to satisfy the soak guard for an inline activation, catching
/// the uniqueness conflict, and re-reading to serve the winner — is NEW (no existing handler
/// does generate-then-inline-activate on a hot serving path; <c>GenerateKey</c> returns the
/// 409 straight to the caller).
/// </para>
/// <para>
/// <b>Back-dating (the sanctioned precedent).</b> <see cref="PendingKey.Activate"/> returns
/// <c>SOAK_NOT_ELAPSED</c> when <c>elapsed &lt; policy.SmokeSoak</c>, so a fresh key can never
/// activate inline without back-dating <c>CreatedAt</c> by the soak window — exactly what
/// <c>SeedCertificateAuthorityHandler.SeedTier</c> does. The soak exists to keep an UNPROVEN
/// key from serving in operator flows where generation and activation are separate ops
/// separated by real time; lazy provisioning smoke-tests INLINE immediately before activating
/// in the same transaction, so the soak's purpose is satisfied by construction.
/// </para>
/// <para>
/// <b>Structural constraint.</b> This path can only ever create an
/// <see cref="KeyType.EcdhSealing"/> key under the passed <c>seal:</c> domain — the caller
/// resolves the domain via <c>KeyDomain.ForSeal</c> and the generation arm is hard-coded.
/// </para>
/// </remarks>
internal static class SealKeyProvisioning
{
    // Bounded visibility poll after a lost provisioning race. Attempt-budget (not wall-clock):
    // each miss awaits a short delay so under load the wait grows with the slowdown; a
    // permanently-stuck domain still terminates at the budget and returns retryable 503.
    // Budget sized for CI-stable visibility under concurrent commit latency (not a wall clock).
    private const int _CONVERGE_ATTEMPT_BUDGET = 64;

    private static readonly TimeSpan sr_convergeDelay = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Loads the seal domain's Active + Retiring keys, provisioning one lazily when the domain
    /// has no live key, and converging on a concurrent winner if a provisioning race is lost.
    /// </summary>
    /// <param name="db">The KeyCustodian DB context.</param>
    /// <param name="classifier">The DB-exception classifier (distinguishes the uniqueness conflict).</param>
    /// <param name="policyProvider">The rotation-policy provider (Default applies to seal domains).</param>
    /// <param name="rootCrypto">The root crypto that wraps the generated private key.</param>
    /// <param name="clock">The clock stamping generation / activation instants.</param>
    /// <param name="logger">The handler's logger (provisioning + unavailability forensic logs).</param>
    /// <param name="domain">The resolved <c>seal:&lt;serviceId&gt;</c> domain.</param>
    /// <param name="triggeredBy">The authenticated caller that triggered the request (forensic).</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>
    /// <c>Ok(<see cref="SealKeyServingSet"/>)</c> with an active key present;
    /// <c>KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE</c> (503, retryable) when a live Pending blocks
    /// provisioning or a lost race's winner is not yet visible; a bubbled generation /
    /// smoke-test failure; or a rethrown non-uniqueness DB failure (classified by the repo pipeline).
    /// </returns>
    public static async Task<D2Result<SealKeyServingSet>> LoadOrProvisionAsync(
        IKeyCustodianDbContext db,
        IDbExceptionClassifier classifier,
        IRotationPolicyProvider policyProvider,
        IPayloadCrypto rootCrypto,
        IClock clock,
        ILogger logger,
        KeyDomain domain,
        string triggeredBy,
        CancellationToken ct)
    {
        // 1) Load existing Active + Retiring seal keys.
        var loadResult = await LoadServingKeysAsync(db, domain, ct).ConfigureAwait(false);

        if (!loadResult.Success)
            return D2Result<SealKeyServingSet>.BubbleFail(loadResult);

        var loaded = loadResult.Data!;

        if (loaded.Active is { } existingActive)
        {
            return D2Result<SealKeyServingSet>.Ok(
                new SealKeyServingSet(existingActive, loaded.Retiring));
        }

        // 2) No active key. A live Pending successor (a soaking rotation successor, or an
        //    orphaned Pending from a crashed provisioner — the one-Pending index then blocks
        //    all new provisioning) means the domain is a retryable not-ready window, never a
        //    second provision.
        var hasPending = await db.Keys
            .ForDomain(domain.Value)
            .Pending()
            .AnyAsync(ct)
            .ConfigureAwait(false);

        if (hasPending)
            return Unavailable(logger, domain);

        // 3) Provision: generate → smoke → inline-activate (back-dated). Build the row +
        //    same-transaction audit, then commit.
        var provisionResult = ProvisionActiveKey(policyProvider, rootCrypto, clock, domain);

        if (!provisionResult.Success)
            return D2Result<SealKeyServingSet>.BubbleFail(provisionResult);

        var provisioned = provisionResult.Data!;

        db.Keys.Add(provisioned.ToNewRecord());

        db.Audit.Add(
            EncryptionKeyAudit.Record(
                provisioned.Kid, KeyAuditAction.Generated, KeyStatus.Pending, clock)
            .ToRecord());

        db.Audit.Add(
            EncryptionKeyAudit.Record(
                provisioned.Kid, KeyAuditAction.Activated, KeyStatus.Active, clock)
            .ToRecord());

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            KeyCustodianMetrics.SR_SealKeypairsProvisionedTotal.Add(1);

            KeyCustodianLog.SealKeypairProvisioned(
                logger, ServiceIdOf(domain), provisioned.Kid.Value, triggeredBy);

            return D2Result<SealKeyServingSet>.Ok(new SealKeyServingSet(provisioned, []));
        }
        catch (Exception ex) when (IsUniqueConflict(classifier, ex))
        {
            // Convergence: a racing first-request winner already inserted an Active (23P01
            // one-Active EXCLUDE) or a Pending (23505 one-Pending unique) for this domain.
            // Catch ANY exception the classifier maps to UniqueViolation — not only
            // DbUpdateException — so a differently-wrapped PG exclusion (or a future
            // EF/Npgsql wrap shape) cannot leak as D2Result.UniqueViolation (409) via
            // BaseRepoHandler. The loser re-reads and serves the winner's key.
            return await ConvergeAfterConflictAsync(db, logger, domain, ct).ConfigureAwait(false);
        }
    }

    // Shared with the catch filter — keep in lockstep with BaseRepoHandler's
    // UniqueViolation dispatch (PostgresDbExceptionClassifier maps 23505 + 23P01).
    private static bool IsUniqueConflict(IDbExceptionClassifier classifier, Exception ex) =>
        classifier.Classify(ex) == DbFailureKind.UniqueViolation;

    // Bounded convergence after a uniqueness / EXCLUDE collision. EF's change tracker is
    // poisoned by the failed SaveChanges (tracked inserts that never committed), so we
    // Clear() before re-reading. One re-read is not enough under concurrent commit latency —
    // poll with an attempt budget until the winner's Active row is visible, then 503.
    private static async Task<D2Result<SealKeyServingSet>> ConvergeAfterConflictAsync(
        IKeyCustodianDbContext db, ILogger logger, KeyDomain domain, CancellationToken ct)
    {
        ClearTrackedState(db);

        for (var attempt = 0; attempt < _CONVERGE_ATTEMPT_BUDGET; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(sr_convergeDelay, ct).ConfigureAwait(false);
                ClearTrackedState(db);
            }

            var reread = await LoadServingKeysAsync(db, domain, ct).ConfigureAwait(false);

            if (!reread.Success)
                return D2Result<SealKeyServingSet>.BubbleFail(reread);

            var loaded = reread.Data!;

            if (loaded.Active is { } winner)
                return D2Result<SealKeyServingSet>.Ok(new SealKeyServingSet(winner, loaded.Retiring));
        }

        return Unavailable(logger, domain);
    }

    // After DbUpdateException the context still tracks the rejected inserts; subsequent
    // queries (even AsNoTracking) can misbehave until the tracker is cleared. Port-only —
    // never cast past IKeyCustodianDbContext to EF's concrete ChangeTracker.
    private static void ClearTrackedState(IKeyCustodianDbContext db) =>
        db.ClearChangeTracker();

    // Loads the domain's Active + Retiring EcdhSealing keys, partitioned + sorted for serving.
    // A wrong-type row surviving the `.Sealing()` filter is trusted-store corruption → a
    // flagged 500 (unreachable via the real record-to-domain mapper; reachable only by mocking).
    private static async Task<D2Result<LoadedSealKeys>> LoadServingKeysAsync(
        IKeyCustodianDbContext db, KeyDomain domain, CancellationToken ct)
    {
        var records = await db.Keys
            .AsNoTracking()
            .ForDomain(domain.Value)
            .Sealing()
            .Where(k => k.Status == KeyStatus.Active || k.Status == KeyStatus.Retiring)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        ActiveKey? active = null;
        var retiring = new List<RetiringKey>(records.Count);

        foreach (var record in records)
        {
            switch (record.ToDomain())
            {
                case ActiveKey a when a.KeyType == KeyType.EcdhSealing:
                    active = a;
                    break;
                case RetiringKey r when r.KeyType == KeyType.EcdhSealing:
                    retiring.Add(r);
                    break;

                default:
                    return KeyCustodianFailures<LoadedSealKeys>.PreconditionViolated();
            }
        }

        retiring.Sort(static (x, y) => y.ActivatedAt.CompareTo(x.ActivatedAt));

        return D2Result<LoadedSealKeys>.Ok(new LoadedSealKeys(active, retiring));
    }

    // Generate → smoke → inline-activate a back-dated pending key so the soak guard is
    // satisfied without a hand-fabricated Active. Mirrors SeedCertificateAuthorityHandler.SeedTier.
    private static D2Result<ActiveKey> ProvisionActiveKey(
        IRotationPolicyProvider policyProvider,
        IPayloadCrypto rootCrypto,
        IClock clock,
        KeyDomain domain)
    {
        var policyResult = policyProvider.ForDomain(domain);

        if (!policyResult.Success)
            return D2Result<ActiveKey>.BubbleFail(policyResult);

        var policy = policyResult.Data!;

        // The EcdhSealing generation arm ignores the RSA/secret sizing tunables (P-256 is fixed).
        var genResult = KeyGeneration.Generate(
            KeyType.EcdhSealing, rsaModulusBits: 0, secretLengthBytes: 0);

        if (!genResult.Success)
            return D2Result<ActiveKey>.BubbleFail(genResult);

        var generated = genResult.Data!;
        ReadOnlyMemory<byte>? publicSpki = generated.PublicSpki is { } spki ? spki : null;

        // Smoke-test the freshly-generated material (a real seal→open round-trip) BEFORE
        // wrapping — the same bytes the managed key will hold. A failure rejects provisioning.
        var smokeResult = SmokeTesting.Verify(KeyType.EcdhSealing, generated.Plaintext, publicSpki);

        if (!smokeResult.Success)
        {
            generated.Zero();
            return D2Result<ActiveKey>.BubbleFail(smokeResult);
        }

        byte[] wrapped;

        try
        {
            wrapped = rootCrypto.Encrypt(generated.Plaintext);
        }
        finally
        {
            // Zero the raw plaintext as soon as it is wrapped — even on a wrap throw.
            generated.Zero();
        }

        var encryptedMaterial = KeyMaterialEncrypted.FromTrusted(wrapped);

        var publicMaterial = generated.PublicSpki is { } pub
            ? PublicKeyMaterial.FromTrusted(pub)
            : null;

        // Back-date CreatedAt by the soak window so Activate's elapsed >= soak guard is
        // satisfied for the inline activation — the key is smoke-tested in this same path.
        var now = clock.GetCurrentInstant();
        var createdAt = now - policy.SmokeSoak;

        var pendingResult = PendingKey.Create(
            Kid.FromTrusted(KidMinting.Mint()),
            domain,
            KeyType.EcdhSealing,
            encryptedMaterial,
            publicMaterial,
            caCertificateMaterial: null,
            createdAt);

        if (!pendingResult.Success)
            return D2Result<ActiveKey>.BubbleFail(pendingResult);

        var proofResult = SmokeProof.ForPassedSmokeTest(KeyType.EcdhSealing, clock);

        if (!proofResult.Success)
            return D2Result<ActiveKey>.BubbleFail(proofResult);

        return pendingResult.Data!.Activate(proofResult.Data!, policy, clock);
    }

    private static D2Result<SealKeyServingSet> Unavailable(ILogger logger, KeyDomain domain)
    {
        KeyCustodianMetrics.SR_SealKeyUnavailableTotal.Add(1);
        KeyCustodianLog.SealKeyUnavailable(logger, ServiceIdOf(domain));

        return KeyCustodianFailures<SealKeyServingSet>.SealKeyUnavailable();
    }

    private static string ServiceIdOf(KeyDomain domain) =>
        domain.Value[KeyDomain.SEAL_PREFIX.Length..];

    private sealed record LoadedSealKeys(ActiveKey? Active, IReadOnlyList<RetiringKey> Retiring);
}
