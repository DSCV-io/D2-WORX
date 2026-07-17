// -----------------------------------------------------------------------
// <copyright file="CaLeafSigningCapability.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Issuance;

/// <summary>
/// The issuance leaf-signing capability impl — the sole holder of the
/// issuance-path intermediate-CA unwrap. Loads the active
/// <c>mtls-ca-intermediate</c> managed key (none → the retryable 503), unwraps its
/// private key via root crypto, signs the supplied CSR public key into a leaf via
/// the pure <see cref="WorkloadCertificateIssuance"/> rule, and zeroes the
/// unwrapped material in a <c>finally</c>. Registered ONLY via
/// <see cref="CaLeafSigningCapabilityServiceCollectionExtensions.AddD2CaLeafSigningCapability"/>
/// — never by <c>AddD2KeyCustodianApp()</c>.
/// </summary>
internal sealed class CaLeafSigningCapability(
    IKeyCustodianDbContext db,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IClock clock)
    : ICaLeafSigningCapability
{
    /// <inheritdoc/>
    public async ValueTask<D2Result<CaSignedLeaf>> SignLeafAsync(
        PublicKey leafPublicKey,
        WorkloadIdentity workload,
        Duration validity,
        CancellationToken ct = default)
    {
        // Plain reference-type null-guards (§5.1a) — programmer error, not input
        // validation; the handler validated both before reaching this seam.
        ArgumentNullException.ThrowIfNull(leafPublicKey);
        ArgumentNullException.ThrowIfNull(workload);

        // 1) Load the active issuing intermediate CA. None active → 503 (the CA is
        //    a dependency that is not ready — retryable, not a client error).
        var intermediateRecord = await db.Keys
            .AsNoTracking()
            .ForDomain(KeyDomain.MTLS_CA_INTERMEDIATE)
            .Active()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (intermediateRecord is null)
            return KeyCustodianFailures<CaSignedLeaf>.NoActiveIssuingCa();

        // A row in the intermediate domain with Active status must be a CA key
        // carrying its certificate material — a malformed shape is corruption,
        // surfaced as the same retryable 503 (the tier is effectively absent).
        if (intermediateRecord.ToDomain() is not ActiveKey intermediate
            || intermediate.KeyType != KeyType.X509CaCertificate
            || intermediate.CaCertificateMaterial is null)
            return KeyCustodianFailures<CaSignedLeaf>.NoActiveIssuingCa();

        // 2) Unwrap the intermediate's private key, reconstruct the issuer cert +
        //    key, and sign the leaf. Zero the unwrapped private key in finally —
        //    the raw CA key never leaves this method.
        var issuerKeyPkcs8 = rootCrypto.Decrypt(intermediate.KeyMaterialEncrypted.Bytes.Span);
        D2Result<IssuedWorkloadCertificate> issuanceResult;

        try
        {
            using var issuerKey = ECDsa.Create();
            issuerKey.ImportPkcs8PrivateKey(issuerKeyPkcs8, out _);
            using var issuerCertificate = X509CertificateLoader.LoadCertificate(
                intermediate.CaCertificateMaterial.Bytes.Span);

            issuanceResult = WorkloadCertificateIssuance.IssueLeaf(
                workload,
                leafPublicKey,
                issuerCertificate,
                issuerKey,
                validity,
                clock);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(issuerKeyPkcs8);
        }

        if (issuanceResult.BubbleOnFailure<IssuedWorkloadCertificate, CaSignedLeaf>(
            out var issuanceBubble, out var issued))
            return issuanceBubble!;

        return D2Result<CaSignedLeaf>.Ok(new CaSignedLeaf(issued!, intermediate.Kid));
    }
}
