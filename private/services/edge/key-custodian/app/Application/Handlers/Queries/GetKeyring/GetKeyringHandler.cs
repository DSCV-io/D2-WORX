// -----------------------------------------------------------------------
// <copyright file="GetKeyringHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetKeyring;

using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Private.Auth;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;
using DcsvIo.D2.Private.Encryption;
using H = DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetKeyring.IGetKeyringHandler;
using I = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring.GetKeyringInput;
using O = DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring.GetKeyringOutput;

/// <summary>
/// Loads a payload key domain's Active + Retiring <see cref="KeyType.AesPayload"/> keys,
/// root-unwraps each, and returns the active kid, every decryptable entry, and the domain's
/// AAD context. Authority-gated through
/// <see cref="WorkloadCapabilityAuthority.AuthorizeKeyringFetch"/> using the established
/// <c>IRequestContext.Origin</c> + <c>IRequestContext.ImmediateCaller</c>. A keyring is a
/// full encrypt+decrypt capability, so custody of the raw AES key bytes transfers to the
/// caller (which assembles a <c>PayloadCryptoKeyring</c>) â€” KeyCustodian never zeroes them,
/// and the DTO's <c>[RedactData(SecretInformation)]</c> keeps them out of logs.
/// </summary>
/// <remarks>
/// Transport-agnostic â€” reads ONLY the scoped request context (<c>Context.Request</c>),
/// never <c>ServerCallContext</c> / <c>Grpc.Core</c> / <c>HttpContext</c>. Authority runs
/// BEFORE the key-type fork so an unauthorized caller probing a non-payload domain gets a
/// uniform 403 (no domain-fact oracle): in production a non-payload domain is denied by the
/// authority arm (no caller can hold a validator-forbidden non-payload grant), and the
/// key-type fork is defense-in-depth only (reachable only by a test that injects a
/// validator-forbidden grant). No Active key yet is the retryable 503.
/// </remarks>
public sealed class GetKeyringHandler(
    HandlerContext<GetKeyringHandler> ctx,
    IKeyCustodianDbContext db,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IKeyringDomainAuthorityPolicy keyringPolicy)
    : BaseHandler<GetKeyringHandler, I, O>(ctx), H
{
    /// <inheritdoc/>
    /// <remarks>The per-handler <c>ScopeRequirement</c> is defense-in-depth: <c>BaseHandler</c>
    /// enforces the <c>internal.kc.keyring</c> scope in-process from
    /// <c>IRequestContext.Scopes</c> (fail-closed) before any authority rule or crypto runs â€”
    /// layered under the transport-level scope check the Edge composition root wires on the
    /// gRPC method. Only the operation-varying scope requirement is per-handler; JWT
    /// signature / expiry / audience validation stay transport-level. Thresholds stay default
    /// (an AES unwrap of a handful of 32-byte keys + one indexed read is fast).</remarks>
    protected override HandlerOptions DefaultOptions => new()
    {
        ScopeRequirement = new ScopeRequirement(
            HandlerScopeMatch.Any,
            new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Keyring }),
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
        //    boundary that produced this context), resolve the policy, and call the pure
        //    rule. Authority runs BEFORE the type fork (step 3), so an unauthorized caller
        //    probing a non-payload domain gets a uniform 403 (no domain-fact oracle). The
        //    handler NEVER re-implements deny logic.
        var immediateCaller = Context.Request.ImmediateCaller;
        var origin = Context.Request.Origin;
        var allowedSet = keyringPolicy.AllowedKeyringDomainsFor(immediateCaller);

        var authResult = WorkloadCapabilityAuthority.AuthorizeKeyringFetch(
            immediateCaller, origin, domain!, allowedSet);

        if (authResult.Failed)
            return DenyWithTelemetry(authResult, immediateCaller, origin, domain!);

        // 3) Sharp fail-loud reject for a non-payload domain: only an AesPayload-bound
        //    domain can EVER hold a keyring, so a domain bound to any other key type is a
        //    permanent 400 â€” never the retryable 503. DEFENSE-IN-DEPTH ONLY: unreachable in
        //    production (authority runs first AND the boot validator refuses every
        //    non-payload grant, so a non-payload domain is never in any allowed set and is
        //    denied at step 2 with a uniform 403). Do NOT reorder before authority â€” that
        //    reopens the domain-type oracle.
        if (domain!.KeyType != KeyType.AesPayload)
            return KeyCustodianFailures<O?>.KeyTypeDomainMismatch();

        // 3b) Additive sealed-mode reject (defense-in-depth beneath nonexistence): a sealed
        //     domain re-admitted to the catalog (e.g. via a future regression) would be
        //     AesPayload-bound, so the KeyType fork above does NOT catch it â€” a sealed domain
        //     has no symmetric keyring by construction. Refuse it with the same sharp-400. Kept
        //     WITH the type fork (AFTER authority) so the no-domain-oracle ordering is preserved.
        if (ProductEncryptionDomainModes.ModeFor(domain.Value)
            == ProductEncryptionDomainMode.Sealed)
        {
            return KeyCustodianFailures<O?>.KeyTypeDomainMismatch();
        }

        // 4) Load Active + Retiring payload keys for the domain. `.Payload()` is
        //    redundant-with-the-fork by construction but keeps the query self-defending â€”
        //    a corrupt wrong-type row can never be served.
        var records = await db.Keys
            .AsNoTracking()
            .ForDomain(domain.Value)
            .Payload()
            .Where(k => k.Status == KeyStatus.Active || k.Status == KeyStatus.Retiring)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // 5) Rehydrate + type-verify: partition into the single Active key + the Retiring
        //    set (mirrors the sign core's shape).
        ActiveKey? active = null;
        var retiring = new List<RetiringKey>(records.Count);

        foreach (var record in records)
        {
            switch (record.ToDomain())
            {
                case ActiveKey a when a.KeyType == KeyType.AesPayload:
                    active = a;
                    break;
                case RetiringKey r when r.KeyType == KeyType.AesPayload:
                    retiring.Add(r);
                    break;

                // Corruption-invariant guard: unreachable via the real record-to-domain
                // mapper (deterministic rehydrate to an AesPayload Active/Retiring key),
                // reachable only by mocking ToDomain().
                default:
                    return KeyCustodianFailures<O?>.PreconditionViolated();
            }
        }

        // 6) No ACTIVE key â†’ 503 (retryable). The 503 fires whenever there is no Active row,
        //    even if Retiring rows exist â€” a keyring with no active kid cannot be assembled
        //    (PayloadCryptoKeyring requires ActiveKid present).
        if (active is null)
        {
            KeyCustodianMetrics.SR_EmptyKeyringServed.Add(1);
            KeyCustodianLog.KeyringKeyUnavailable(Context.Logger, domain.Value);
            return KeyCustodianFailures<O?>.KeyringKeyUnavailable();
        }

        // 7) Assemble entries â€” active first, then retiring newest-activated-first
        //    (deterministic; multiple Retiring rows are possible when rotations outpace the
        //    grace window). Custody of the unwrapped bytes transfers to the caller â€” KC does
        //    NOT zero them (the DTO's [RedactData] keeps them out of logs; the consumer copies
        //    them into a defensive-copying + zeroing PayloadCryptoKeyring).
        retiring.Sort(static (x, y) => y.ActivatedAt.CompareTo(x.ActivatedAt));

        var entries = new List<KeyringEntry>(1 + retiring.Count)
        {
            new(active.Kid.Value, rootCrypto.Decrypt(active.KeyMaterialEncrypted.Bytes.Span)),
        };

        foreach (var r in retiring)
        {
            entries.Add(new KeyringEntry(
                r.Kid.Value, rootCrypto.Decrypt(r.KeyMaterialEncrypted.Bytes.Span)));
        }

        // 8) Return the active kid, every decryptable entry, and the domain's AAD context
        //    (Query â€” no DB write, no audit row).
        return D2Result<O?>.Ok(new O(active.Kid.Value, entries, KeyringAadProjection.For(domain)));
    }

    private D2Result<O?> DenyWithTelemetry(
        D2Result authResult, string? immediateCaller, RequestOrigin origin, KeyDomain domain)
    {
        // Switch on the EMITTED error-code constants, never raw string literals (in scope
        // via the app/GlobalUsings.cs DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Errors global using). The
        // uniform 403 KEYRING_DOMAIN_NOT_AUTHORIZED splits by deny arm for TELEMETRY ONLY â€”
        // the wire code stays uniform (no domain-existence oracle): a plane deny (origin not
        // in the served set) is UNAUTHORIZED_PLANE, a policy miss is NOT_IN_ALLOWED_SET. The
        // handler distinguishes them via the locally-read Origin.
        var reason = authResult.ErrorCode switch
        {
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED =>
                KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED,
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED =>
                origin is not (RequestOrigin.CrossProcessHop or RequestOrigin.InProcessModule)
                    ? KeyCustodianMetrics.AuthorityRejections.Reason.UNAUTHORIZED_PLANE
                    : KeyCustodianMetrics.AuthorityRejections.Reason.NOT_IN_ALLOWED_SET,

            // Forbidden â€” an authorized plane with no caller identity.
            _ => KeyCustodianMetrics.AuthorityRejections.Reason.IDENTITY_ABSENT,
        };

        KeyCustodianLog.AuthorityRejected(
            Context.Logger,
            immediateCaller ?? KeyCustodianMetrics.AuthorityRejections.Workload.NONE,
            KeyCustodianMetrics.AuthorityRejections.Capability.KEYRING,
            domain.Value);

        KeyCustodianMetrics.SR_AuthorityRejectionsTotal.Add(
            1,
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_CAPABILITY,
                KeyCustodianMetrics.AuthorityRejections.Capability.KEYRING),
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_REASON, reason));

        // No SR_CrossProcessSigningRejections here â€” that counter is the signing
        // crown-jewel signal (the cluster-signing root / CA trust anchors); keyring has no
        // never-servable minter analog (the non-payload crown jewels are excluded by the
        // boot validator + the type fork, not a per-fetch crown-jewel counter).
        return D2Result<O?>.BubbleFail(authResult);
    }
}
