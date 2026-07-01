// -----------------------------------------------------------------------
// <copyright file="SignHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Queries.Sign;

using D2.Shared.Auth.Abstractions;
using H = D2.Edge.KeyCustodian.App.Application.Handlers.Queries.Sign.ISignHandler;
using I = D2.Edge.KeyCustodian.Clients.SignInput;
using O = D2.Edge.KeyCustodian.Clients.SignOutput;

/// <summary>
/// Loads a key domain's active <see cref="KeyType.RsaSigning"/> key, decrypts the
/// private key via root crypto, signs the input (RS256) via the pure
/// <see cref="RsaSigning"/> rule (zeroing the unwrapped key in a finally), and
/// returns the signature + kid. Authority-gated through
/// <see cref="WorkloadCapabilityAuthority.AuthorizeSigning"/> using the established
/// <c>IRequestContext.Origin</c> + <c>IRequestContext.ImmediateCaller</c> surfaced on
/// the handler context. The general surface signs every domain except
/// <c>jwks-signing</c> (categorically rejected here — reachable only via the dedicated
/// minter capability).
/// </summary>
/// <remarks>
/// Transport-agnostic — reads ONLY the scoped request context (<c>Context.Request</c>),
/// never <c>ServerCallContext</c> / <c>Grpc.Core</c> / <c>HttpContext</c>. The
/// established <c>Origin</c> / <c>ImmediateCaller</c> are passed straight to the refined
/// rule, the single fail-closed chokepoint: an unestablished origin denies;
/// <c>jwks-signing</c> is categorically <c>MinterCapabilityRequired</c> on this surface;
/// every other domain requires a cross-process origin + the caller's policy.
/// </remarks>
public sealed class SignHandler(
    HandlerContext<SignHandler> ctx,
    IKeyCustodianDbContext db,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    ISigningDomainAuthorityPolicy signingPolicy)
    : BaseHandler<SignHandler, I, O>(ctx), H
{
    /// <inheritdoc/>
    /// <remarks>RSA private-key import + sign is slow crypto that can exceed the
    /// platform default thresholds (mirrors the issuance handler). The per-handler
    /// <c>ScopeRequirement</c> is defense-in-depth: <c>BaseHandler</c> enforces the
    /// <c>internal.kc.sign</c> scope in-process from <c>IRequestContext.Scopes</c>
    /// (fail-closed) before any authority rule or crypto runs — layered under the
    /// transport-level scope check the Edge composition root wires on the gRPC
    /// method. Only the operation-varying scope requirement is per-handler; JWT
    /// signature / expiry / audience validation stay transport-level.</remarks>
    protected override HandlerOptions DefaultOptions => new()
    {
        SlowThreshold = TimeSpan.FromSeconds(1),
        CriticalThreshold = TimeSpan.FromSeconds(5),
        ScopeRequirement = new ScopeRequirement(
            HandlerScopeMatch.Any,
            new HashSet<string>(StringComparer.Ordinal) { Scopes.Internal.Kc.Sign }),
    };

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input, CancellationToken ct)
    {
        // 1) Validate the domain at the TOP — invalid/unknown → 400 before any DB/crypto.
        var domainResult = KeyDomain.Create(input.KeyDomain);

        if (domainResult.BubbleOnFailure<KeyDomain, O>(out var domainBubble, out var domain))
            return domainBubble;

        // 2) Authority gate — read the ESTABLISHED Origin + ImmediateCaller (set by the
        //    boundary that produced this context), resolve the policy, and call the refined
        //    rule. The handler passes Origin straight to the rule — the single fail-closed
        //    chokepoint: Origin=Unestablished denies; jwks-signing is categorically
        //    MinterCapabilityRequired on this general surface (reachable only via
        //    IJwtSigningCapability); every other domain requires Origin=CrossProcessHop +
        //    policy. The handler NEVER re-implements deny logic.
        var immediateCaller = Context.Request.ImmediateCaller;
        var origin = Context.Request.Origin;
        var allowedSet = signingPolicy.AllowedSigningDomainsFor(immediateCaller);

        var authResult = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller, origin, domain!, allowedSet);

        if (authResult.Failed)
            return DenyWithTelemetry(authResult, immediateCaller, domain!);

        // 3) Sign via the shared core (empty-input reject + active-key load + decrypt +
        //    sign + zero). The general surface never reaches jwks-signing — step (2) above
        //    rejected it categorically before this point.
        var signResult = await KeyDomainSigner
            .SignActiveKeyAsync(db, rootCrypto, domain!, input.SigningInput, ct)
            .ConfigureAwait(false);

        if (signResult.BubbleOnFailure<O, O>(out var signBubble, out var output))
            return signBubble;

        // 4) Return the signature + the kid that produced it (Query — no DB write, no audit).
        return D2Result<O?>.Ok(output);
    }

    private D2Result<O?> DenyWithTelemetry(
        D2Result authResult, string? immediateCaller, KeyDomain domain)
    {
        // Switch on the EMITTED error-code constants, never raw string literals — they are
        // in scope via the app/GlobalUsings.cs D2.Edge.KeyCustodian.Domain.Errors global
        // using. This switch maps the rule's typed code to a bounded reason tag drawn from
        // the KeyCustodianMetrics.AuthorityRejections named-constant closed set.
        var reason = authResult.ErrorCode switch
        {
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED,
            KeyCustodianErrorCodes.KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.MINTER_REQUIRED,
            KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.NOT_IN_ALLOWED_SET,

            // Forbidden — cross-process call with no caller identity.
            _ => KeyCustodianMetrics.AuthorityRejections.Reason.IDENTITY_ABSENT,
        };

        KeyCustodianLog.AuthorityRejected(
            Context.Logger,
            immediateCaller ?? KeyCustodianMetrics.AuthorityRejections.Workload.NONE,
            KeyCustodianMetrics.AuthorityRejections.Capability.SIGN,
            domain.Value);

        KeyCustodianMetrics.SR_AuthorityRejectionsTotal.Add(
            1,
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_CAPABILITY,
                KeyCustodianMetrics.AuthorityRejections.Capability.SIGN),
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_REASON, reason));

        // The dedicated highest-severity counter fires when a caller tried to reach the
        // cluster-signing root on the general surface (MinterCapabilityRequired) — the
        // crown-jewel-key attempt. The bounded reason tag is a named closed-set constant
        // (KeyCustodianMetrics.AuthorityRejections.Reason), not a raw literal.
        if (reason == KeyCustodianMetrics.AuthorityRejections.Reason.MINTER_REQUIRED)
            KeyCustodianMetrics.SR_CrossProcessSigningRejections.Add(1);

        return D2Result<O?>.BubbleFail(authResult);
    }
}
