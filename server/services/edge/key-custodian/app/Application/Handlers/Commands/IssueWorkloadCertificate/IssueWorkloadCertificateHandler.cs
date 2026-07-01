// -----------------------------------------------------------------------
// <copyright file="IssueWorkloadCertificateHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;

using System.Security.Cryptography.X509Certificates;

using H = D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate.IIssueWorkloadCertificateHandler;
using I = IssueWorkloadCertificateInput;
using O = IssueWorkloadCertificateOutput;

/// <summary>
/// Issues a short-lived workload leaf certificate signed by the active issuing
/// intermediate certificate authority.
/// </summary>
/// <remarks>
/// Validates the workload identity at the top, loads + decrypts the active
/// <c>mtls-ca-intermediate</c> managed key (none → <c>503</c>), reconstructs the
/// issuer certificate + private key, issues the leaf via the pure
/// <see cref="WorkloadCertificateIssuance"/> rule, and writes a leaf-issuance
/// audit entry in one <see cref="IKeyCustodianDbContext.SaveChangesAsync"/>. The
/// unwrapped issuer private key is zeroed in a <c>finally</c>; the leaf private
/// key is returned to the caller (the workload), which zeroes it after install.
/// </remarks>
public sealed class IssueWorkloadCertificateHandler(
    HandlerContext<IssueWorkloadCertificateHandler> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    IOptions<KeyCustodianOptions> options,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IClock clock)
    : BaseRepoHandler<
        IssueWorkloadCertificateHandler,
        I,
        O>(ctx, classifier),
      H
{
    /// <inheritdoc/>
    /// <remarks>
    /// CA unwrap + ECDSA leaf generation + signing is slow crypto that routinely
    /// exceeds the platform default slow-handler thresholds (100ms warn / 500ms error).
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
        // 1) Validate the workload identity at the top — invalid / unknown / wrong
        //    charset all surface as INVALID_WORKLOAD_IDENTITY before any DB or crypto.
        var workloadResult = WorkloadIdentity.Create(input.WorkloadServiceId);

        if (workloadResult.BubbleOnFailure<WorkloadIdentity, O>(
            out var workloadBubble, out var workload))
            return workloadBubble;

        // 2) Load the active issuing intermediate CA. None active → 503 (the CA is a
        //    dependency that is not ready, a retryable condition, not a client error).
        var intermediateRecord = await db.Keys
            .ForDomain(KeyDomain.MTLS_CA_INTERMEDIATE)
            .Active()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (intermediateRecord is null)
            return NoActiveIssuingCa(workload!);

        // A row in the intermediate domain with Active status must be a CA key
        // carrying its certificate material — a malformed shape is corruption.
        if (intermediateRecord.ToDomain() is not ActiveKey intermediate
            || intermediate.KeyType != KeyType.X509CaCertificate
            || intermediate.CaCertificateMaterial is null)
            return NoActiveIssuingCa(workload!);

        // 3) Unwrap the intermediate's private key, reconstruct the issuer cert +
        //    key, and issue the leaf. Zero the unwrapped private key in finally.
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
                issuerCertificate,
                issuerKey,
                Duration.FromTimeSpan(options.Value.LeafValidity),
                clock);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(issuerKeyPkcs8);
        }

        if (issuanceResult.BubbleOnFailure<IssuedWorkloadCertificate, O>(
            out var issuanceBubble, out var issued))
            return issuanceBubble;

        // 4) Persist the leaf-issuance audit entry (the only write on the leaf path).
        db.LeafIssuanceAudit.Add(
            LeafIssuanceAudit.Record(workload!, intermediate.Kid, issued!.NotAfter, clock)
            .ToRecord());

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        KeyCustodianMetrics.SR_LeafCertificatesIssuedTotal.Add(1);

        return D2Result<O?>.Ok(
            new O(issued));
    }

    private D2Result<O?> NoActiveIssuingCa(WorkloadIdentity workload)
    {
        KeyCustodianLog.NoActiveIssuingCa(Context.Logger, workload.ServiceId);
        KeyCustodianMetrics.SR_NoActiveIssuingCaTotal.Add(1);
        return KeyCustodianFailures<O?>.NoActiveIssuingCa();
    }
}
