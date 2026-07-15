// -----------------------------------------------------------------------
// <copyright file="CaRootSigningCapability.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.CertificateAuthority;

using Microsoft.Extensions.Logging;

/// <summary>
/// The dedicated CA-root-signing capability impl — the sole holder of every stored
/// <c>mtls-ca-root</c> private-key plaintext materialization (rules §9.44). Loads the
/// active root and signs a successor intermediate; and unwraps a pending / successor
/// root to smoke-test it. Every unwrap is zeroed in a <c>finally</c>, and every use is
/// the single §9.44 chokepoint that fires <c>SR_CaRootKeyUsesTotal</c> + the CA-root-key
/// log delegates. Registered ONLY via
/// <see cref="CaRootSigningCapabilityServiceCollectionExtensions.AddD2CaRootSigningCapability"/>
/// — never by <c>AddD2KeyCustodianApp()</c>.
/// </summary>
/// <remarks>
/// The keyed <c>rootCrypto</c> here is the at-rest KEK (the root SERVICE key that wraps
/// stored material), the SAME singleton the successor-wrap in
/// <see cref="CaSuccessorFactory"/> injects — referenced twice, never moved. §9.44's
/// isolated secret is the CA-root SIGNING key this capability unwraps, not that KEK.
/// </remarks>
internal sealed class CaRootSigningCapability(
    IKeyCustodianDbContext db,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IOptions<KeyCustodianOptions> options,
    IClock clock,
    ILogger<CaRootSigningCapability> logger)
    : ICaRootSigningCapability
{
    /// <inheritdoc/>
    public async ValueTask<D2Result<GeneratedCaMaterial>> SignSuccessorIntermediateAsync(
        Kid successorKid, string operation, CancellationToken ct = default)
    {
        // Plain reference-type null-guards (§5.1a) — programmer error, not input
        // validation; the caller (CaSuccessorFactory) supplies both.
        ArgumentNullException.ThrowIfNull(successorKid);
        ArgumentNullException.ThrowIfNull(operation);

        // Load the active root — the dependency that signs the intermediate. None
        // active → 503 (the root is a dependency that is not ready, retryable). This is
        // the verbatim body extracted from CaSuccessorFactory.GenerateIntermediateAsync;
        // behavior is unchanged, the plaintext just materializes inside this seam now.
        var rootRecord = await db.Keys
            .ForDomain(KeyDomain.MTLS_CA_ROOT)
            .Active()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (rootRecord is null)
            return KeyCustodianFailures<GeneratedCaMaterial>.NoActiveIssuingCa();

        if (rootRecord.ToDomain() is not ActiveKey root
            || root.KeyType != KeyType.X509CaCertificate
            || root.CaCertificateMaterial is null)
            return KeyCustodianFailures<GeneratedCaMaterial>.NoActiveIssuingCa();

        var rootKeyPkcs8 = rootCrypto.Decrypt(root.KeyMaterialEncrypted.Bytes.Span);

        try
        {
            // §9.44 chokepoint: the root SIGNING key plaintext was materialized for a
            // sign. Instrument the use (kids + operation only — never key material).
            RecordRootKeySigningUse(operation, root.Kid.Value, successorKid.Value);

            using var rootKey = ECDsa.Create();
            rootKey.ImportPkcs8PrivateKey(rootKeyPkcs8, out _);
            using var rootCert = X509CertificateLoader.LoadCertificate(
                root.CaCertificateMaterial.Bytes.Span);

            return CaCertificateGeneration.GenerateIntermediateCa(
                CaCertificateGeneration.INTERMEDIATE_CA_SUBJECT,
                rootCert,
                rootKey,
                Duration.FromTimeSpan(options.Value.IntermediateCaValidity),
                clock);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(rootKeyPkcs8);
        }
    }

    /// <inheritdoc/>
    public ValueTask<D2Result> SmokeTestRootKeyMaterialAsync(
        PendingKey pendingRoot, string operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pendingRoot);
        ArgumentNullException.ThrowIfNull(operation);

        // The decrypt throw on an undecryptable wrapped blob propagates exactly as the
        // inline generic smoke path did (no new swallow). The verify probe is
        // synchronous + CPU-bound, so ct is not observed here — it is kept for interface
        // symmetry. _ = ct silences the unused-parameter analyzer without a suppression.
        _ = ct;

        var unwrapped = rootCrypto.Decrypt(pendingRoot.KeyMaterialEncrypted.Bytes.Span);

        try
        {
            // §9.44 chokepoint: the root key plaintext was materialized for a smoke
            // test. Recorded regardless of the verify outcome — the use already happened.
            RecordRootKeySmokeUse(operation, pendingRoot.Kid.Value);

            return ValueTask.FromResult(
                SmokeTesting.Verify(
                    pendingRoot.KeyType, unwrapped, pendingRoot.PublicKeyMaterial?.Bytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(unwrapped);
        }
    }

    private void RecordRootKeySigningUse(string operation, string rootKid, string successorKid)
    {
        KeyCustodianLog.CaRootKeySigningUsed(logger, operation, rootKid, successorKid);
        KeyCustodianMetrics.SR_CaRootKeyUsesTotal.Add(
            1,
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.CaRootKeyUses.TAG_OPERATION, operation));
    }

    private void RecordRootKeySmokeUse(string operation, string kid)
    {
        KeyCustodianLog.CaRootKeySmokeTested(logger, operation, kid);
        KeyCustodianMetrics.SR_CaRootKeyUsesTotal.Add(
            1,
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.CaRootKeyUses.TAG_OPERATION, operation));
    }
}
