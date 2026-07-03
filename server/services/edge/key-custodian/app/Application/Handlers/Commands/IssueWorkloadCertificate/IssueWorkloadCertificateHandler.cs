// -----------------------------------------------------------------------
// <copyright file="IssueWorkloadCertificateHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;

using D2.Shared.Auth.Abstractions;

using H = D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate.IIssueWorkloadCertificateHandler;
using I = IssueWorkloadCertificateInput;
using O = IssueWorkloadCertificateOutput;

/// <summary>
/// Issues a short-lived workload leaf certificate from a PKCS#10
/// certificate-signing request, signed by the active issuing intermediate via the
/// isolated <see cref="ICaLeafSigningCapability"/>. The single chokepoint BOTH
/// planes (in-process façade + gRPC) flow through — authority gate, scope check,
/// CSR verification, audit write, and deny telemetry all live here.
/// </summary>
/// <remarks>
/// <para>
/// <b>Fail-closed, cross-process-only.</b> The FIRST gate is the real
/// <see cref="WorkloadCertificateAuthority.AuthorizeIssuance"/> rule over the
/// established <c>IRequestContext.Origin</c> + <c>ImmediateCaller</c>: an
/// unestablished origin, a non-cross-process plane, and an absent mTLS peer all
/// deny BEFORE the CSR is even parsed — a garbage or forged CSR from an
/// unauthorized context surfaces the 403, never <c>INVALID_CSR</c> (no CSR-parse
/// oracle), and never a CA-state 503 (no CA-state oracle).
/// </para>
/// <para>
/// <b>Structural self-issue.</b> The leaf's subject-alternative-name is ALWAYS
/// derived from the authenticated mTLS peer identity
/// (<c>Context.Request.ImmediateCaller</c>, sourced from the validated peer
/// certificate's SPIFFE SAN); the CSR's subject / SAN / requested extensions are
/// ignored by construction — only its verified public key reaches the leaf.
/// </para>
/// <para>
/// <b>No leaf private key anywhere.</b> The workload generates its own keypair;
/// KeyCustodian verifies the CSR's proof-of-possession + P-256 curve
/// (<see cref="CsrVerification"/>) and signs the extracted public key via the
/// isolated capability (the ONLY holder of the intermediate-CA unwrap — this
/// handler never touches the raw CA key). Below the gates: audit entry in one
/// <see cref="IKeyCustodianDbContext.SaveChangesAsync"/>, then the issuance
/// counter + forensic log.
/// </para>
/// </remarks>
public sealed class IssueWorkloadCertificateHandler(
    HandlerContext<IssueWorkloadCertificateHandler> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    IOptions<KeyCustodianOptions> options,
    ICaLeafSigningCapability leafSigner,
    IClock clock)
    : BaseRepoHandler<
        IssueWorkloadCertificateHandler,
        I,
        O>(ctx, classifier),
      H
{
    /// <inheritdoc/>
    /// <remarks>
    /// CA unwrap + leaf signing is slow crypto that routinely exceeds the platform
    /// default slow-handler thresholds (100ms warn / 500ms error). The per-handler
    /// <c>ScopeRequirement</c> is defense-in-depth: <c>BaseHandler</c> enforces the
    /// <c>internal.kc.issue</c> scope in-process from <c>IRequestContext.Scopes</c>
    /// (fail-closed) before the authority rule or any crypto runs — layered under
    /// the transport-level scope check the Edge composition root wires on the gRPC
    /// method. Only the operation-varying scope requirement is per-handler; JWT
    /// signature / expiry / audience validation stay transport-level.
    /// </remarks>
    protected override HandlerOptions DefaultOptions => new()
    {
        SlowThreshold = TimeSpan.FromSeconds(2),
        CriticalThreshold = TimeSpan.FromSeconds(10),
        ScopeRequirement = new ScopeRequirement(
            HandlerScopeMatch.Any,
            new HashSet<string>(StringComparer.Ordinal) { Scopes.Internal.Kc.Issue }),
    };

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input, CancellationToken ct)
    {
        // 1) Authority precedes ALL work — read the ESTABLISHED Origin +
        //    ImmediateCaller (set by the boundary that produced this context; §9.41)
        //    and call the pure rule. Ordering is load-bearing: a garbage CSR from an
        //    unauthorized context gets the 403, never INVALID_CSR (no CSR-parse
        //    oracle) and never the CA-state 503 (no CA-state oracle). The handler
        //    NEVER re-implements deny logic.
        var immediateCaller = Context.Request.ImmediateCaller;
        var origin = Context.Request.Origin;

        var authorityResult = WorkloadCertificateAuthority.AuthorizeIssuance(
            immediateCaller, origin);

        if (authorityResult.Failed)
            return DenyWithTelemetry(authorityResult, immediateCaller);

        // 2) CSR verification — AFTER the gate, BEFORE any store access: size cap,
        //    PKCS#10 parse, proof-of-possession, P-256 curve OID. Any failure is the
        //    uniform 400 INVALID_CSR (validation, not an authority rejection — no
        //    counter). Only the verified public key survives; the CSR's subject /
        //    SAN / extensions are never read.
        var csrResult = CsrVerification.Verify(input.CsrDer);

        if (csrResult.BubbleOnFailure<PublicKey, O>(out var csrBubble, out var leafPublicKey))
            return csrBubble;

        // 3) Belt-and-braces re-validation of the peer id. The value came from a
        //    VALIDATED SPIFFE SAN, so this always passes on a live transport; it
        //    pins the invariant against a mis-established test/context value.
        var workloadResult = WorkloadIdentity.Create(immediateCaller);

        if (workloadResult.BubbleOnFailure<WorkloadIdentity, O>(
            out var workloadBubble, out var workload))
            return workloadBubble;

        // 4) Sign via the isolated leaf-signing capability — the ONLY holder of the
        //    intermediate-CA unwrap; the raw CA private key never enters this
        //    handler. No active intermediate → the retryable 503.
        var signResult = await leafSigner.SignLeafAsync(
                leafPublicKey!,
                workload!,
                Duration.FromTimeSpan(options.Value.LeafValidity),
                ct)
            .ConfigureAwait(false);

        if (signResult.Failed)
        {
            if (string.Equals(
                    signResult.ErrorCode,
                    KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA,
                    StringComparison.Ordinal))
            {
                KeyCustodianLog.NoActiveIssuingCa(Context.Logger, workload!.ServiceId);
                KeyCustodianMetrics.SR_NoActiveIssuingCaTotal.Add(1);
            }

            return D2Result<O?>.BubbleFail(signResult);
        }

        var signed = signResult.Data!;

        // 5) Persist the leaf-issuance audit entry (the only write on the leaf
        //    path), then the counter + the forensic log (the audit row is the
        //    durable record; the log is the log-side complement).
        db.LeafIssuanceAudit.Add(
            LeafIssuanceAudit.Record(
                workload!, signed.IssuerKid, signed.Certificate.NotAfter, clock)
            .ToRecord());

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        KeyCustodianMetrics.SR_LeafCertificatesIssuedTotal.Add(1);
        KeyCustodianLog.LeafCertificateIssued(
            Context.Logger,
            workload!.ServiceId,
            signed.IssuerKid.Value,
            NodaTime.Text.InstantPattern.ExtendedIso.Format(signed.Certificate.NotAfter));

        return D2Result<O?>.Ok(
            new O(signed.Certificate));
    }

    private D2Result<O?> DenyWithTelemetry(D2Result authResult, string? immediateCaller)
    {
        // Switch on the EMITTED error-code constants, never raw string literals (in
        // scope via the app/GlobalUsings.cs D2.Edge.KeyCustodian.Domain.Errors global
        // using). The uniform 403 ISSUANCE_NOT_AUTHORIZED splits by deny arm for
        // TELEMETRY ONLY — the wire code stays uniform (no plane-probing signal).
        var reason = authResult.ErrorCode switch
        {
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED,
            KeyCustodianErrorCodes.KEYCUSTODIAN_ISSUANCE_NOT_AUTHORIZED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.UNAUTHORIZED_PLANE,

            // Forbidden — a cross-process hop with no caller identity.
            _ => KeyCustodianMetrics.AuthorityRejections.Reason.IDENTITY_ABSENT,
        };

        // Issuance is a TARGETLESS capability (no key-domain target) — the log's
        // target field carries the closed-set Target.NONE marker, never a literal.
        KeyCustodianLog.AuthorityRejected(
            Context.Logger,
            immediateCaller ?? KeyCustodianMetrics.AuthorityRejections.Workload.NONE,
            KeyCustodianMetrics.AuthorityRejections.Capability.ISSUANCE,
            KeyCustodianMetrics.AuthorityRejections.Target.NONE);

        KeyCustodianMetrics.SR_AuthorityRejectionsTotal.Add(
            1,
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_CAPABILITY,
                KeyCustodianMetrics.AuthorityRejections.Capability.ISSUANCE),
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_REASON, reason));

        return D2Result<O?>.BubbleFail(authResult);
    }
}
