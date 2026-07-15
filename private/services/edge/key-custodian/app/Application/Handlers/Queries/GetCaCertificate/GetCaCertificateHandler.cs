// -----------------------------------------------------------------------
// <copyright file="GetCaCertificateHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetCaCertificate;

using D2.Private.Auth;
using H = D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetCaCertificate.IGetCaCertificateHandler;
using I = D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateInput;
using O = D2.Edge.KeyCustodian.Client.CaCertificate.GetCaCertificateOutput;

/// <summary>
/// Returns the certificate-authority chain â€” the active root (the trust anchor a
/// workload pins) + the active issuing intermediate (chain convenience) â€” as
/// DER-encoded public certificate material. Authority-gated through
/// <see cref="WorkloadCertificateAuthority.AuthorizeCaCertificateFetch"/> using
/// the established <c>IRequestContext.Origin</c> + <c>ImmediateCaller</c>:
/// served on the cross-process + in-process planes only (the internal trust
/// anchor never rides the public plane), broad within those planes (public
/// material â€” no per-workload policy map).
/// </summary>
/// <remarks>
/// Transport-agnostic â€” reads ONLY the scoped request context
/// (<c>Context.Request</c>), never <c>ServerCallContext</c> / <c>HttpContext</c>.
/// Both tiers are REQUIRED: a partial chain is not "the chain", so a missing or
/// malformed root OR intermediate is the retryable 503 (the CA has not been
/// seeded or is between rotations). No decrypt â€” the certificate columns are
/// plaintext public material; no DB write â€” Query.
/// </remarks>
public sealed class GetCaCertificateHandler(
    HandlerContext<GetCaCertificateHandler> ctx,
    IKeyCustodianDbContext db)
    : BaseHandler<GetCaCertificateHandler, I, O>(ctx), H
{
    /// <inheritdoc/>
    /// <remarks>The per-handler <c>ScopeRequirement</c> is defense-in-depth:
    /// <c>BaseHandler</c> enforces the <c>internal.kc.cacert</c> scope in-process from
    /// <c>IRequestContext.Scopes</c> (fail-closed) before the authority rule runs â€”
    /// layered under the transport-level scope check the Edge composition root wires
    /// on the gRPC method. Only the operation-varying scope requirement is
    /// per-handler; JWT signature / expiry / audience validation stay
    /// transport-level. Thresholds stay default (two indexed reads, no crypto).</remarks>
    protected override HandlerOptions DefaultOptions => new()
    {
        ScopeRequirement = new ScopeRequirement(
            HandlerScopeMatch.Any,
            new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Cacert }),
    };

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input, CancellationToken ct)
    {
        // 1) Authority gate â€” read the ESTABLISHED Origin + ImmediateCaller (set by
        //    the boundary that produced this context; Â§9.41) and call the pure rule.
        //    Authority precedes the store reads so an unauthorized caller learns
        //    nothing about CA state (403 before any 503 â€” no CA-state oracle).
        var immediateCaller = Context.Request.ImmediateCaller;
        var origin = Context.Request.Origin;

        var authResult = WorkloadCertificateAuthority.AuthorizeCaCertificateFetch(
            immediateCaller, origin);

        if (authResult.Failed)
            return DenyWithTelemetry(authResult, immediateCaller);

        // 2) Load the active root + active intermediate. Either tier missing or
        //    malformed â†’ the retryable 503 (a partial chain is not "the chain").
        var rootBytes = await LoadActiveCaCertificateAsync(KeyDomain.MTLS_CA_ROOT, ct)
            .ConfigureAwait(false);

        if (rootBytes is null)
            return CaCertificateUnavailable(immediateCaller);

        var intermediateBytes = await LoadActiveCaCertificateAsync(
                KeyDomain.MTLS_CA_INTERMEDIATE, ct)
            .ConfigureAwait(false);

        if (intermediateBytes is null)
            return CaCertificateUnavailable(immediateCaller);

        // 3) Return the chain (Query â€” no DB write, no audit row). Public trust
        //    material â€” presented on the wire in every TLS handshake.
        return D2Result<O?>.Ok(new O(rootBytes, intermediateBytes));
    }

    /// <summary>
    /// Loads the active CA certificate DER for a CA key domain, or
    /// <see langword="null"/> when the tier is absent or malformed (no active row,
    /// a non-CA shape, or missing certificate material â€” all "tier not ready").
    /// </summary>
    /// <param name="domain">The CA key domain wire value (root or intermediate).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The DER bytes, or <see langword="null"/> when the tier is not servable.</returns>
    private async Task<byte[]?> LoadActiveCaCertificateAsync(string domain, CancellationToken ct)
    {
        var record = await db.Keys
            .AsNoTracking()
            .ForDomain(domain)
            .Active()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (record is null)
            return null;

        // An active row in a CA domain must be a CA key carrying its certificate
        // material â€” a malformed shape is treated as the tier being absent.
        if (record.ToDomain() is not ActiveKey active
            || active.KeyType != KeyType.X509CaCertificate
            || active.CaCertificateMaterial is null)
            return null;

        return active.CaCertificateMaterial.Bytes.ToArray();
    }

    private D2Result<O?> CaCertificateUnavailable(string? immediateCaller)
    {
        KeyCustodianLog.CaCertificateUnavailable(
            Context.Logger,
            immediateCaller ?? KeyCustodianMetrics.AuthorityRejections.Workload.NONE);
        KeyCustodianMetrics.SR_NoActiveIssuingCaTotal.Add(1);
        return KeyCustodianFailures<O?>.NoActiveIssuingCa();
    }

    private D2Result<O?> DenyWithTelemetry(D2Result authResult, string? immediateCaller)
    {
        // Switch on the EMITTED error-code constants, never raw string literals (in
        // scope via the app/GlobalUsings.cs D2.Edge.KeyCustodian.Domain.Errors global
        // using). The uniform 403 CA_CERTIFICATE_NOT_AUTHORIZED splits by deny arm
        // for TELEMETRY ONLY â€” the wire code stays uniform.
        var reason = authResult.ErrorCode switch
        {
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED,
            KeyCustodianErrorCodes.KEYCUSTODIAN_CA_CERTIFICATE_NOT_AUTHORIZED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.UNAUTHORIZED_PLANE,

            // Forbidden â€” a served plane with no caller identity.
            _ => KeyCustodianMetrics.AuthorityRejections.Reason.IDENTITY_ABSENT,
        };

        // The CA-chain fetch is a TARGETLESS capability (no key-domain target) â€” the
        // log's target field carries the closed-set Target.NONE marker.
        KeyCustodianLog.AuthorityRejected(
            Context.Logger,
            immediateCaller ?? KeyCustodianMetrics.AuthorityRejections.Workload.NONE,
            KeyCustodianMetrics.AuthorityRejections.Capability.CA_CERT,
            KeyCustodianMetrics.AuthorityRejections.Target.NONE);

        KeyCustodianMetrics.SR_AuthorityRejectionsTotal.Add(
            1,
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_CAPABILITY,
                KeyCustodianMetrics.AuthorityRejections.Capability.CA_CERT),
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_REASON, reason));

        return D2Result<O?>.BubbleFail(authResult);
    }
}
