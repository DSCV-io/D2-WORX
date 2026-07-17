// -----------------------------------------------------------------------
// <copyright file="GetOrLazyProvisionOwnSealPrivateKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionOwnSealPrivateKey;

using DcsvIo.D2.Private.Auth;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Sealing;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing;
using H = DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionOwnSealPrivateKey.IGetOrLazyProvisionOwnSealPrivateKeyHandler;
using I = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionOwnSealPrivateKeyInput;
using O = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionOwnSealPrivateKeyOutput;

/// <summary>
/// Serves the CALLER'S OWN PRIVATE sealing keys (active + retiring, root-unwrapped PKCS#8) so
/// it can open frames sealed to it. Authority-gated through
/// <see cref="WorkloadCapabilityAuthority.AuthorizeSealDecrypt"/> â€” the seal-decrypt hard gate:
/// cross-process ONLY, so the key-selecting identity (<c>IRequestContext.ImmediateCaller</c>)
/// IS the unforgeable validated mTLS peer id (the interceptor sets Origin + ImmediateCaller
/// atomically). The op carries NO target parameter, so a caller can only ever obtain its OWN
/// key â€” impersonation is structurally unrepresentable. The first request provisions the
/// service's keypair lazily (a <c>Command</c>).
/// </summary>
/// <remarks>
/// Transport-agnostic â€” reads ONLY the scoped request context (<c>Context.Request</c>), never
/// <c>ServerCallContext</c> / <c>Grpc.Core</c> / <c>HttpContext</c>. The structural hard-gate
/// guarantee = (decrypt arm is cross-process-only) âˆ§ (the interceptor's atomic
/// OriginâŸºImmediateCaller coupling) âˆ§ (the op carries no serviceId / target). A forged
/// in-process caller is denied AT THE PLANE ARM â€” it never reaches key selection. No Active
/// key yet is the retryable 503.
/// </remarks>
public sealed class GetOrLazyProvisionOwnSealPrivateKeyHandler(
    HandlerContext<GetOrLazyProvisionOwnSealPrivateKeyHandler> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    IRotationPolicyProvider policyProvider,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IClock clock)
    : BaseRepoHandler<GetOrLazyProvisionOwnSealPrivateKeyHandler, I, O>(ctx, classifier), H
{
    // Explicit field (not the captured primary-ctor parameter, which is also passed to the
    // base) â€” the shared provisioning helper needs the classifier to recognize the
    // uniqueness conflict on the lazy-provision save (CS9107-free).
    private readonly IDbExceptionClassifier r_classifier = classifier;

    /// <inheritdoc/>
    /// <remarks>Lazy provisioning generates + smoke-tests a keypair on the first request for a
    /// service, and root-unwrap is slow crypto â€” both can exceed the platform default
    /// thresholds. The per-handler <c>ScopeRequirement</c> is defense-in-depth: <c>BaseHandler</c>
    /// enforces the <c>internal.kc.seal.open</c> scope in-process (fail-closed) before any
    /// authority rule or crypto runs â€” layered under the transport-level scope check.</remarks>
    protected override HandlerOptions DefaultOptions => new()
    {
        SlowThreshold = TimeSpan.FromSeconds(2),
        CriticalThreshold = TimeSpan.FromSeconds(10),
        ScopeRequirement = new ScopeRequirement(
            HandlerScopeMatch.Any,
            new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Seal.Open }),
    };

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input, CancellationToken ct)
    {
        // 1) Authority gate â€” the seal-decrypt HARD GATE holds structurally: cross-process-only, so
        //    ImmediateCaller is the unforgeable mTLS peer id. Unestablished / non-cross-process
        //    plane / identity-absent â†’ deny (targetless â€” the op carries no key-domain target).
        var immediateCaller = Context.Request.ImmediateCaller;
        var origin = Context.Request.Origin;

        var authResult = WorkloadCapabilityAuthority.AuthorizeSealDecrypt(immediateCaller, origin);

        if (authResult.Failed)
            return DenyWithTelemetry(authResult, immediateCaller);

        // 2) The key-selecting identity is the SAME atomically-established value the arm just
        //    authorized â€” belt-and-braces re-validation pins the invariant against a
        //    mis-established context value (always passes on a live transport).
        var domainResult = KeyDomain.ForSeal(immediateCaller);

        if (domainResult.BubbleOnFailure<KeyDomain, O>(out var domainBubble, out var domain))
            return domainBubble;

        var setResult = await SealKeyProvisioning.LoadOrProvisionAsync(
                db,
                r_classifier,
                policyProvider,
                rootCrypto,
                clock,
                Context.Logger,
                domain!,
                immediateCaller!,
                ct)
            .ConfigureAwait(false);

        if (setResult.BubbleOnFailure<SealKeyServingSet, O>(out var setBubble, out var set))
            return setBubble;

        // 3) Root-unwrap each private key: active first, then retiring newest-activated-first.
        //    Custody of the unwrapped bytes transfers to the caller (which assembles a
        //    RecipientPrivateKeyring); the DTO's [RedactData] keeps them out of logs.
        var entries = new List<SealPrivateEntry>(1 + set!.Retiring.Count)
        {
            new(
                set.Active.Kid.Value,
                rootCrypto.Decrypt(set.Active.KeyMaterialEncrypted.Bytes.Span)),
        };

        foreach (var retiring in set.Retiring)
        {
            entries.Add(new SealPrivateEntry(
                retiring.Kid.Value, rootCrypto.Decrypt(retiring.KeyMaterialEncrypted.Bytes.Span)));
        }

        return D2Result<O?>.Ok(new O(set.Active.Kid.Value, entries));
    }

    private D2Result<O?> DenyWithTelemetry(D2Result authResult, string? immediateCaller)
    {
        // Switch on the EMITTED error-code constants, never raw string literals (in scope via
        // the app/GlobalUsings.cs DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Errors global using). The uniform
        // 403 SEAL_NOT_AUTHORIZED splits by deny arm for TELEMETRY ONLY â€” the wire code stays
        // uniform (no plane-probing signal).
        var reason = authResult.ErrorCode switch
        {
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED,
            KeyCustodianErrorCodes.KEYCUSTODIAN_SEAL_NOT_AUTHORIZED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.UNAUTHORIZED_PLANE,

            // Forbidden â€” a cross-process hop with no caller identity.
            _ => KeyCustodianMetrics.AuthorityRejections.Reason.IDENTITY_ABSENT,
        };

        // Seal-decrypt is a TARGETLESS capability (no key-domain target â€” the key is selected
        // from the caller identity alone) â†’ the log's target field carries Target.NONE.
        KeyCustodianLog.AuthorityRejected(
            Context.Logger,
            immediateCaller ?? KeyCustodianMetrics.AuthorityRejections.Workload.NONE,
            KeyCustodianMetrics.AuthorityRejections.Capability.SEAL_DECRYPT,
            KeyCustodianMetrics.AuthorityRejections.Target.NONE);

        KeyCustodianMetrics.SR_AuthorityRejectionsTotal.Add(
            1,
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_CAPABILITY,
                KeyCustodianMetrics.AuthorityRejections.Capability.SEAL_DECRYPT),
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_REASON, reason));

        return D2Result<O?>.BubbleFail(authResult);
    }
}
