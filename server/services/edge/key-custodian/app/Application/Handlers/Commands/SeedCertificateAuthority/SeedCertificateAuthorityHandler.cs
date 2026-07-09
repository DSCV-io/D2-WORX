// -----------------------------------------------------------------------
// <copyright file="SeedCertificateAuthorityHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.SeedCertificateAuthority;

using H = D2.Edge.KeyCustodian.App.Application.Handlers.Commands.SeedCertificateAuthority.ISeedCertificateAuthorityHandler;
using I = SeedCertificateAuthorityInput;
using O = SeedCertificateAuthorityOutput;

/// <summary>
/// Seeds the certificate-authority hierarchy on startup from the
/// <see cref="ICaProvider"/>: persists the root + issuing intermediate as active
/// managed <c>X509CaCertificate</c> keys so the rest of the system (issuance,
/// rotation, compromise-replacement) reads the CA from the database like any other
/// managed key.
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotent.</b> If both CA domains already hold an active key the command is
/// a no-op (returns <c>Seeded = false</c>). The active-CA gate makes a re-run on a
/// seeded store safe — the seeder owns the CA domains, which the auto-bootstrap map
/// deliberately excludes.
/// </para>
/// <para>
/// <b>Genuine activate path.</b> Each tier is constructed as a
/// <see cref="PendingKey"/> with a back-dated <c>CreatedAt</c> (so the smoke-soak
/// window is already elapsed), smoke-tested (the CA arm), then activated via
/// <see cref="PendingKey.Activate"/> — never a hand-fabricated <c>ActiveKey</c>, so
/// the illegal-state-unrepresentable invariant holds. The smoke test on seed is a
/// genuine integrity check on the loaded material.
/// </para>
/// <para>
/// Both tiers + their <c>Generated</c> and <c>Activated</c> audit entries land in
/// one <see cref="IKeyCustodianDbContext.SaveChangesAsync"/>.
/// </para>
/// </remarks>
public sealed class SeedCertificateAuthorityHandler(
    HandlerContext<SeedCertificateAuthorityHandler> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    ICaProvider caProvider,
    IRotationPolicyProvider policyProvider,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IClock clock)
    : BaseRepoHandler<
        SeedCertificateAuthorityHandler,
        I,
        O>(ctx, classifier),
      H
{
    /// <inheritdoc/>
    /// <remarks>
    /// Loading + chain-validating the CA, two root-wrap encrypts, and two smoke
    /// tests routinely exceed the platform default slow-handler thresholds
    /// (100ms warn / 500ms error).
    /// </remarks>
    protected override HandlerOptions DefaultOptions => new()
    {
        SlowThreshold = TimeSpan.FromSeconds(2),
        CriticalThreshold = TimeSpan.FromSeconds(10),
    };

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input, CancellationToken ct)
    {
        // 0) Authority precedes work: lifecycle mutations are System-plane-only,
        //    fail-closed (the CA seeder establishes the System context on its scope).
        var authorityResult =
            KeyLifecycleAuthority.AuthorizeLifecycleMutation(Context.Request.Origin);

        if (authorityResult.Failed)
        {
            return LifecycleAuthorityTelemetry.Deny<O>(
                Context.Logger,
                authorityResult,
                Context.Request.ImmediateCaller,
                "seed-certificate-authority");
        }

        // 1) Per-tier idempotency gate — check each tier INDEPENDENTLY so a partial
        //    seed (crash after root was persisted but before intermediate) re-runs
        //    only the missing tier on the next boot, not both. If both are already
        //    active, the whole command is a no-op.
        var rootActive = await db.Keys
            .ForDomain(KeyDomain.MTLS_CA_ROOT)
            .Active()
            .AnyAsync(ct)
            .ConfigureAwait(false);

        var intermediateActive = await db.Keys
            .ForDomain(KeyDomain.MTLS_CA_INTERMEDIATE)
            .Active()
            .AnyAsync(ct)
            .ConfigureAwait(false);

        if (rootActive && intermediateActive)
        {
            KeyCustodianLog.CaSeedSkippedAlreadyActive(Context.Logger);

            return D2Result<O?>.Ok(
                new O(Seeded: false, RootKid: null, IntermediateKid: null));
        }

        // 2) Load + chain-validate the dev CA hierarchy (typed failure on any load error).
        var loadResult = caProvider.GetSeedCaMaterial();

        if (loadResult.BubbleOnFailure<LoadedCaMaterial, O>(
            out var loadBubble, out var loaded))
            return loadBubble;

        try
        {
            // 3) Seed only the missing tiers through the genuine smoke → activate path.
            //    Each tier is seeded independently — an already-active tier is skipped
            //    so a partial-seed restart inserts only the genuinely missing tier.
            var loadedCa = loaded!;
            ActiveKey? rootActiveKey = null;
            ActiveKey? intermediateActiveKey = null;

            if (!rootActive)
            {
                var rootSeedResult = SeedTier(
                    KeyDomain.MtlsCaRoot,
                    loadedCa.RootPrivateKeyPkcs8,
                    loadedCa.RootCertificateDer);

                if (rootSeedResult.BubbleOnFailure<ActiveKey, O>(
                    out var rootBubble, out var rootNullable))
                    return rootBubble;

                rootActiveKey = rootNullable!;
            }

            if (!intermediateActive)
            {
                var intermediateSeedResult = SeedTier(
                    KeyDomain.MtlsCaIntermediate,
                    loadedCa.IntermediatePrivateKeyPkcs8,
                    loadedCa.IntermediateCertificateDer);

                if (intermediateSeedResult.BubbleOnFailure<ActiveKey, O>(
                    out var intermediateBubble, out var intermediateNullable))
                    return intermediateBubble;

                intermediateActiveKey = intermediateNullable!;
            }

            // 4) Persist only the newly seeded tiers + their audit entries in one save.
            var seededCount = 0;

            if (rootActiveKey is not null)
            {
                db.Keys.Add(rootActiveKey.ToNewRecord());
                AppendSeedAudit(rootActiveKey.Kid);
                seededCount++;
            }

            if (intermediateActiveKey is not null)
            {
                db.Keys.Add(intermediateActiveKey.ToNewRecord());
                AppendSeedAudit(intermediateActiveKey.Kid);
                seededCount++;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            KeyCustodianMetrics.SR_KeyGenerationsTotal.Add(seededCount);

            KeyCustodianLog.CaSeeded(
                Context.Logger,
                rootActiveKey?.Kid.Value ?? "(already active)",
                intermediateActiveKey?.Kid.Value ?? "(already active)");

            return D2Result<O?>.Ok(
                new O(
                    Seeded: true,
                    RootKid: rootActiveKey?.Kid.Value,
                    IntermediateKid: intermediateActiveKey?.Kid.Value));
        }
        finally
        {
            // Zero both loaded private keys once they have been wrapped + seeded.
            loaded!.Zero();
        }
    }

    /// <summary>
    /// Builds an active managed CA key for one tier from loaded material: root-wraps
    /// the private key, smoke-tests it, and activates a back-dated pending key so the
    /// smoke-soak is satisfied without a hand-fabricated active state.
    /// </summary>
    private D2Result<ActiveKey> SeedTier(
        KeyDomain domain, byte[] privateKeyPkcs8, byte[] certificateDer)
    {
        var policyResult = policyProvider.ForDomain(domain);

        if (!policyResult.Success)
            return D2Result<ActiveKey>.BubbleFail(policyResult);

        var policy = policyResult.Data!;

        // Smoke-test the loaded private key BEFORE wrapping (the same bytes the
        // managed key will hold). A failure rejects the seed loudly.
        var smokeResult = SmokeTesting.Verify(
            KeyType.X509CaCertificate, privateKeyPkcs8, publicSpki: null);

        if (!smokeResult.Success)
            return D2Result<ActiveKey>.BubbleFail(smokeResult);

        // Inner zero mirrors GenerateKeyHandler / IssueWorkloadCertificateHandler: zero the
        // plaintext PKCS#8 immediately after wrapping, even if Encrypt throws, so the
        // sensitive bytes are never left un-zeroed if the outer try-scope exception occurs.
        byte[] wrapped;

        try
        {
            wrapped = rootCrypto.Encrypt(privateKeyPkcs8);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyPkcs8);
        }

        var encryptedMaterial = KeyMaterialEncrypted.FromTrusted(wrapped);
        var caCertMaterial = CaCertificateMaterial.FromTrusted(certificateDer);

        // Back-date CreatedAt by the soak window so Activate's elapsed >= soak guard
        // is satisfied — the loaded material is a pre-validated trust anchor that
        // does not need to wait out a fresh soak.
        var now = clock.GetCurrentInstant();
        var createdAt = now - policy.SmokeSoak;

        var pendingResult = PendingKey.Create(
            Kid.FromTrusted(KidMinting.Mint()),
            domain,
            KeyType.X509CaCertificate,
            encryptedMaterial,
            publicMaterial: null,
            caCertificateMaterial: caCertMaterial,
            createdAt);

        if (!pendingResult.Success)
            return D2Result<ActiveKey>.BubbleFail(pendingResult);

        var proofResult = SmokeProof.ForPassedSmokeTest(KeyType.X509CaCertificate, clock);

        if (!proofResult.Success)
            return D2Result<ActiveKey>.BubbleFail(proofResult);

        return pendingResult.Data!.Activate(proofResult.Data!, policy, clock);
    }

    private void AppendSeedAudit(Kid kid)
    {
        db.Audit.Add(
            EncryptionKeyAudit.Record(kid, KeyAuditAction.Generated, KeyStatus.Pending, clock)
            .ToRecord());

        db.Audit.Add(
            EncryptionKeyAudit.Record(kid, KeyAuditAction.Activated, KeyStatus.Active, clock)
            .ToRecord());
    }
}
