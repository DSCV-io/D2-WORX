// -----------------------------------------------------------------------
// <copyright file="GetOrLazyProvisionSealPublicKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionSealPublicKey;

using DcsvIo.D2.Private.Auth;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Sealing;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing;
using H = DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.GetOrLazyProvisionSealPublicKey.IGetOrLazyProvisionSealPublicKeyHandler;
using I = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionSealPublicKeyInput;
using O = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing.GetOrLazyProvisionSealPublicKeyOutput;

/// <summary>
/// Serves a target service's PUBLIC sealing keys (active + retiring SPKI public keys) so a
/// producer can seal a payload TO it. Authority-gated through
/// <see cref="WorkloadCapabilityAuthority.AuthorizeSealEncrypt"/> using the established
/// <c>IRequestContext.Origin</c> + <c>IRequestContext.ImmediateCaller</c> â€” broad within the
/// served planes (public key material is harmless to over-share; the transport scope gates
/// whether the caller may seal at all). The first request for a service's seal domain
/// provisions its keypair lazily (a <c>Command</c>). Public keys are served straight from
/// <c>PublicKeyMaterial</c> (plaintext at rest â€” no root-decrypt).
/// </summary>
/// <remarks>
/// Transport-agnostic â€” reads ONLY the scoped request context (<c>Context.Request</c>), never
/// <c>ServerCallContext</c> / <c>Grpc.Core</c> / <c>HttpContext</c>. Authority precedes the
/// serviceId validation so an unauthorized / unestablished caller gets a uniform 403, never a
/// 400 that would confirm whether the requested serviceId is well-formed (no validation
/// oracle â€” the same posture as the issuance surface). No Active key yet is the retryable 503.
/// </remarks>
public sealed class GetOrLazyProvisionSealPublicKeyHandler(
    HandlerContext<GetOrLazyProvisionSealPublicKeyHandler> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    IRotationPolicyProvider policyProvider,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IClock clock)
    : BaseRepoHandler<GetOrLazyProvisionSealPublicKeyHandler, I, O>(ctx, classifier), H
{
    // Explicit field (not the captured primary-ctor parameter, which is also passed to the
    // base) â€” the shared provisioning helper needs the classifier to recognize the
    // uniqueness conflict on the lazy-provision save (CS9107-free).
    private readonly IDbExceptionClassifier r_classifier = classifier;

    /// <inheritdoc/>
    /// <remarks>Lazy provisioning generates + smoke-tests a keypair on the first request for a
    /// service, which can exceed the platform default slow-handler thresholds. The per-handler
    /// <c>ScopeRequirement</c> is defense-in-depth: <c>BaseHandler</c> enforces the
    /// <c>internal.kc.seal.encrypt</c> scope in-process (fail-closed) before any authority rule
    /// or crypto runs â€” layered under the transport-level scope check.</remarks>
    protected override HandlerOptions DefaultOptions => new()
    {
        SlowThreshold = TimeSpan.FromSeconds(2),
        CriticalThreshold = TimeSpan.FromSeconds(10),
        ScopeRequirement = new ScopeRequirement(
            HandlerScopeMatch.Any,
            new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Seal.Encrypt }),
    };

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input, CancellationToken ct)
    {
        // 1) Authority gate FIRST â€” read the ESTABLISHED Origin + ImmediateCaller and call the
        //    pure rule. Ordering is load-bearing: an unauthorized / unestablished caller gets
        //    the 403 before the serviceId is even validated (no validation oracle).
        var immediateCaller = Context.Request.ImmediateCaller;
        var origin = Context.Request.Origin;

        var authResult = WorkloadCapabilityAuthority.AuthorizeSealEncrypt(immediateCaller, origin);

        if (authResult.Failed)
            return DenyWithTelemetry(authResult, immediateCaller);

        // 2) Validate the target service id (Â§9.4) â†’ 400 before any DB/crypto. The gate has
        //    passed, so immediateCaller is present (used as the provisioning trigger below).
        var workloadResult = WorkloadIdentity.Create(input.ServiceId);

        if (workloadResult.BubbleOnFailure<WorkloadIdentity, O>(
            out var workloadBubble, out var workload))
            return workloadBubble;

        // 3) Build the seal:<serviceId> domain from the ALREADY-VALIDATED identity â€” no second
        //    grammar pass (the service id is validated exactly once, at step 2) â€” and
        //    load-or-lazily-provision its Active + Retiring keys.
        var domain = KeyDomain.ForSeal(workload!);

        var setResult = await SealKeyProvisioning.LoadOrProvisionAsync(
                db,
                r_classifier,
                policyProvider,
                rootCrypto,
                clock,
                Context.Logger,
                domain,
                immediateCaller!,
                ct)
            .ConfigureAwait(false);

        if (setResult.BubbleOnFailure<SealKeyServingSet, O>(out var setBubble, out var set))
            return setBubble;

        // 4) Serve the SPKI public keys straight from PublicKeyMaterial (plaintext at rest â€”
        //    no root-decrypt): active first, then retiring newest-activated-first.
        var entries = new List<SealPublicEntry>(1 + set!.Retiring.Count)
        {
            new(set.Active.Kid.Value, PublicSpkiOf(set.Active)),
        };

        foreach (var retiring in set.Retiring)
            entries.Add(new SealPublicEntry(retiring.Kid.Value, PublicSpkiOf(retiring)));

        return D2Result<O?>.Ok(new O(set.Active.Kid.Value, entries));
    }

    // EcdhSealing keys always carry non-null public material (EnsureMaterialShape enforces it
    // on write; LoadServingKeysAsync type-checks EcdhSealing), so the public material is
    // present by construction on every served key.
    private static byte[] PublicSpkiOf(EncryptionKey key) =>
        key.PublicKeyMaterial!.Bytes.ToArray();

    private D2Result<O?> DenyWithTelemetry(D2Result authResult, string? immediateCaller)
    {
        // Switch on the EMITTED error-code constants, never raw string literals (in scope via
        // the app/GlobalUsings.cs DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Errors global using). The uniform
        // 403 SEAL_NOT_AUTHORIZED splits by deny arm for TELEMETRY ONLY â€” the wire code stays
        // uniform (no plane / service-existence oracle).
        var reason = authResult.ErrorCode switch
        {
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED,
            KeyCustodianErrorCodes.KEYCUSTODIAN_SEAL_NOT_AUTHORIZED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.UNAUTHORIZED_PLANE,

            // Forbidden â€” a served plane with no caller identity.
            _ => KeyCustodianMetrics.AuthorityRejections.Reason.IDENTITY_ABSENT,
        };

        // Seal-encrypt denies precede serviceId resolution, so the target is unresolved â†’
        // the closed-set Target.NONE marker, never a literal.
        KeyCustodianLog.AuthorityRejected(
            Context.Logger,
            immediateCaller ?? KeyCustodianMetrics.AuthorityRejections.Workload.NONE,
            KeyCustodianMetrics.AuthorityRejections.Capability.SEAL_ENCRYPT,
            KeyCustodianMetrics.AuthorityRejections.Target.NONE);

        KeyCustodianMetrics.SR_AuthorityRejectionsTotal.Add(
            1,
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_CAPABILITY,
                KeyCustodianMetrics.AuthorityRejections.Capability.SEAL_ENCRYPT),
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_REASON, reason));

        return D2Result<O?>.BubbleFail(authResult);
    }
}
