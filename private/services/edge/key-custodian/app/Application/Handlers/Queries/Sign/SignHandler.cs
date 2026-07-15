// -----------------------------------------------------------------------
// <copyright file="SignHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.Sign;

using DcsvIo.D2.Private.Auth;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Signing;
using H = DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.Sign.ISignHandler;
using I = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Signing.SignInput;
using O = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Signing.SignOutput;

/// <summary>
/// Loads a key domain's active <see cref="KeyType.RsaSigning"/> key, decrypts the
/// private key via root crypto, signs the input (RS256) via the pure
/// <see cref="RsaSigning"/> rule (zeroing the unwrapped key in a finally), and
/// returns the signature + kid. Authority-gated through
/// <see cref="WorkloadCapabilityAuthority.AuthorizeSigning"/> using the established
/// <c>IRequestContext.Origin</c> + <c>IRequestContext.ImmediateCaller</c> surfaced on
/// the handler context. The general surface categorically rejects <c>jwks-signing</c>
/// (reachable only via the dedicated minter capability) and both CA domains
/// (never signable anywhere), and sharply rejects any domain whose bound
/// <see cref="KeyType"/> is not <see cref="KeyType.RsaSigning"/> with a permanent 400
/// (a non-signing-bound domain can never hold a signing key â€” never the retryable 503).
/// </summary>
/// <remarks>
/// Transport-agnostic â€” reads ONLY the scoped request context (<c>Context.Request</c>),
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
    /// (fail-closed) before any authority rule or crypto runs â€” layered under the
    /// transport-level scope check the Edge composition root wires on the gRPC
    /// method. Only the operation-varying scope requirement is per-handler; JWT
    /// signature / expiry / audience validation stay transport-level.</remarks>
    protected override HandlerOptions DefaultOptions => new()
    {
        SlowThreshold = TimeSpan.FromSeconds(1),
        CriticalThreshold = TimeSpan.FromSeconds(5),
        ScopeRequirement = new ScopeRequirement(
            HandlerScopeMatch.Any,
            new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Sign }),
    };

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input, CancellationToken ct)
    {
        // 1) Validate the domain at the TOP â€” invalid/unknown â†’ 400 before any DB/crypto.
        var domainResult = KeyDomain.Create(input.KeyDomain);

        if (domainResult.BubbleOnFailure<KeyDomain, O>(out var domainBubble, out var domain))
            return domainBubble;

        // 2) Authority gate â€” read the ESTABLISHED Origin + ImmediateCaller (set by the
        //    boundary that produced this context), resolve the policy, and call the refined
        //    rule. The handler passes Origin straight to the rule â€” the single fail-closed
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

        // 3) Sharp fail-loud reject for a structurally-unsignable domain: only an
        //    RsaSigning-bound domain can EVER hold an active signing key, so a domain
        //    bound to any other key type is a permanent 400 â€” never the retryable 503
        //    the signer core returns for a not-yet-provisioned key.
        if (domain!.KeyType != KeyType.RsaSigning)
            return KeyCustodianFailures<O?>.KeyTypeDomainMismatch();

        // 4) Sign via the shared core (empty-input reject + active-key load + decrypt +
        //    sign + zero). The general surface never reaches jwks-signing â€” step (2) above
        //    rejected it categorically before this point.
        var signResult = await KeyDomainSigner
            .SignActiveKeyAsync(db, rootCrypto, domain, input.SigningInput, ct)
            .ConfigureAwait(false);

        if (signResult.BubbleOnFailure<O, O>(out var signBubble, out var output))
            return signBubble;

        // 5) Return the signature + the kid that produced it (Query â€” no DB write, no audit).
        return D2Result<O?>.Ok(output);
    }

    private D2Result<O?> DenyWithTelemetry(
        D2Result authResult, string? immediateCaller, KeyDomain domain)
    {
        // Switch on the EMITTED error-code constants, never raw string literals â€” they are
        // in scope via the app/GlobalUsings.cs DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Errors global
        // using. This switch maps the rule's typed code to a bounded reason tag drawn from
        // the KeyCustodianMetrics.AuthorityRejections named-constant closed set.
        var reason = authResult.ErrorCode switch
        {
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED,
            KeyCustodianErrorCodes.KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.MINTER_REQUIRED,
            KeyCustodianErrorCodes.KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.NEVER_SIGNABLE,
            KeyCustodianErrorCodes.KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.NOT_IN_ALLOWED_SET,

            // Forbidden â€” cross-process call with no caller identity.
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

        // The dedicated highest-severity counter fires when a caller tried to reach a
        // crown-jewel key on the general surface: the cluster-signing root
        // (MinterCapabilityRequired) or a CA trust anchor (CrossProcessDomainRejected â€”
        // never-signable). The bounded reason tag is a named closed-set constant
        // (KeyCustodianMetrics.AuthorityRejections.Reason), not a raw literal.
        if (reason is KeyCustodianMetrics.AuthorityRejections.Reason.MINTER_REQUIRED
            or KeyCustodianMetrics.AuthorityRejections.Reason.NEVER_SIGNABLE)
            KeyCustodianMetrics.SR_CrossProcessSigningRejections.Add(1);

        return D2Result<O?>.BubbleFail(authResult);
    }
}
