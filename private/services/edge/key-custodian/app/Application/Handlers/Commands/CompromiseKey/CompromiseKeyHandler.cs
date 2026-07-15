// -----------------------------------------------------------------------
// <copyright file="CompromiseKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.CompromiseKey;

using D2.Edge.KeyCustodian.App.Application.CertificateAuthority;

using H = D2.Edge.KeyCustodian.App.Application.Handlers.Commands.CompromiseKey.ICompromiseKeyHandler;
using I = CompromiseKeyInput;
using O = CompromiseKeyOutput;

/// <summary>
/// Marks a live key compromised and — as a best-effort follow-up — generates a
/// replacement pending key.
/// </summary>
/// <remarks>
/// Authority precedes work: the System-plane-only
/// <see cref="KeyLifecycleAuthority.AuthorizeLifecycleMutation"/> gate runs FIRST
/// (fail-closed; deny emits the lifecycle authority-rejection telemetry). Then
/// validates the operator <c>Reason</c> as a 400 input error (operator
/// input — NOT the domain's 500 defense-in-depth guard), loads the LIVE key by
/// kid (not found among live keys → 404), compromises it via the sealed state's
/// <c>Compromise</c> method, projects the result, and appends a <c>Compromised</c>
/// audit entry carrying a NON-SENSITIVE breadcrumb (NEVER the raw reason).
/// The compromise transition (+ its audit) commits in its OWN
/// <c>SaveChangesAsync</c> FIRST — the durable kill — so a compromised key can
/// NEVER stay live because a replacement could not be built or inserted. It then
/// announces the compromise urgently after that durable commit; a post-commit
/// announce failure is logged, not fatal.
/// Only THEN, and only when requested, is a replacement pending key generated as a
/// best-effort follow-up in a SEPARATE save: generation is SKIPPED (and the
/// pre-existing successor reported instead) when the domain is already mid-rotation
/// (a live pending successor exists), and a build failure or a second-save conflict
/// is logged and yields a <see langword="null"/> replacement — the already-committed
/// compromise is never rolled back.
/// </remarks>
public sealed class CompromiseKeyHandler(
    HandlerContext<CompromiseKeyHandler> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    IOptions<KeyCustodianOptions> options,
    IKeyRotationAnnouncer announcer,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    ICaRootSigningCapability rootSigning,
    IClock clock)
    : BaseRepoHandler<CompromiseKeyHandler, I, O>(
        ctx, classifier),
      H
{
    private const string _AUDIT_DETAIL = "operator-initiated";

    // Non-sensitive reason breadcrumb for the best-effort replacement second-save failure
    // (most commonly a racing one-pending-per-domain unique violation). Never an exception
    // message (§3.1).
    private const string _REPLACEMENT_SAVE_CONFLICT = "persistence-conflict";

    /// <inheritdoc/>
    /// <remarks>
    /// <c>LogInput = false</c> — <see cref="CompromiseKeyInput"/> carries
    /// <c>[RedactData] Reason</c>, but the Serilog destructuring policy is an
    /// optional wire-up; disabling input logging removes the dependency entirely
    /// (defense-in-depth — the sensitive operator reason never enters logs).
    /// Root-wrap encryption + replacement-key generation also routinely exceeds
    /// the platform default slow-handler thresholds (100ms warn / 500ms error).
    /// </remarks>
    protected override HandlerOptions DefaultOptions => new()
    {
        LogInput = false,
        SlowThreshold = TimeSpan.FromSeconds(2),
        CriticalThreshold = TimeSpan.FromSeconds(10),
    };

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input, CancellationToken ct)
    {
        // Authority precedes work: lifecycle mutations are System-plane-only, fail-closed.
        // The future operator/admin transport for compromise-key must CONSCIOUSLY extend
        // KeyLifecycleAuthority (and add its own scope) — the deny arm forces it.
        var authorityResult =
            KeyLifecycleAuthority.AuthorizeLifecycleMutation(Context.Request.Origin);

        if (authorityResult.Failed)
        {
            return LifecycleAuthorityTelemetry.Deny<O>(
                Context.Logger,
                authorityResult,
                Context.Request.ImmediateCaller,
                "compromise-key");
        }

        // Operator input — a missing reason is a 400 validation error, NOT the
        // domain's 500 precondition guard.
        if (input.Reason.Falsey())
        {
            return D2Result<O?>.ValidationFailed(
                inputErrors:
                [
                    new InputError(
                        nameof(I.Reason), [TK.Common.Errors.NOT_NULL_VIOLATION]),
                ]);
        }

        var kidResult = Kid.Create(input.Kid);

        if (kidResult.BubbleOnFailure<Kid, O>(out var bubbled, out var kid))
            return bubbled;

        var record = await db.Keys
            .Live()
            .FirstOrDefaultAsync(k => k.Kid == kid!.Value, ct)
            .ConfigureAwait(false);

        if (record is null)
            return KeyCustodianFailures<O?>.KeyNotFound();

        var compromiseResult = CompromiseLiveKey(record.ToDomain(), input.Reason!);

        if (compromiseResult.BubbleOnFailure<CompromisedKey, O>(
            out var compBubble, out var compromised))
            return compBubble;

        compromised!.ProjectOnto(record);

        db.Audit.Add(
            EncryptionKeyAudit.Record(
                kid!, KeyAuditAction.Compromised, KeyStatus.Compromised, clock, _AUDIT_DETAIL)
            .ToRecord());

        // Save #1 — the durable kill. The compromise transition (+ its audit) commits in
        // its OWN transaction BEFORE any replacement work, so a compromised key can NEVER
        // stay live because a replacement could not be built or inserted. Everything after
        // this point is best-effort and can never roll the kill back.
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        KeyCustodianMetrics.SR_CompromisesTotal.Add(1);

        // Post-commit announce: urgent after the durable commit; NOT gated on replacement
        // generation. A failure is logged (sanitized, no raw exception) and is non-fatal —
        // consumers self-heal via keyring TTL refresh.
        var announceResult = await announcer
            .AnnounceAsync(
                compromised!.KeyDomain, compromised.Kid, KeyStatus.Compromised, urgent: true, ct)
            .ConfigureAwait(false);

        if (!announceResult.Success)
        {
            KeyCustodianLog.AnnounceFailed(
                Context.Logger,
                compromised.KeyDomain.Value,
                compromised.Kid.Value,
                announceResult.ErrorCode);
            KeyCustodianMetrics.SR_AnnounceFailuresTotal.Add(
                1, new KeyValuePair<string, object?>("urgent", "true"));
        }

        // Replacement generation is a best-effort follow-up AFTER the durable compromise
        // commit. A build failure OR a racing pending-insert conflict on the second save is
        // logged, not fatal — the compromise already committed, so a null replacement kid
        // is returned rather than rolling the kill back.
        var replacementKid = input.GenerateReplacement
            ? await TryGenerateReplacementAsync(compromised, ct).ConfigureAwait(false)
            : null;

        return D2Result<O?>.Ok(
            new O(compromised.Kid.Value, replacementKid));
    }

    // Best-effort replacement generation, run ONLY after the compromise has durably
    // committed. Returns the replacement kid (a freshly-generated one, or the pre-existing
    // live pending successor when the domain is already mid-rotation), or null when a live
    // key could not be generated. NEVER throws for a build/save failure — the compromise is
    // already durable, so a missing replacement is logged and surfaced as a null kid.
    private async Task<string?> TryGenerateReplacementAsync(
        CompromisedKey compromised, CancellationToken ct)
    {
        var compromisedDomain = compromised.KeyDomain.Value;
        var compromisedKid = compromised.Kid.Value;

        // Mid-rotation: a live pending successor already IS the replacement. Report it and
        // insert nothing — a second Pending would breach the one-pending-per-domain unique
        // index (23505) on the best-effort save below.
        var existingSuccessorKid = await db.Keys
            .ForDomain(compromisedDomain)
            .Pending()
            .Where(k => k.Kid != compromisedKid)
            .Select(k => k.Kid)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (existingSuccessorKid is not null)
            return existingSuccessorKid;

        // A build failure is reachable (e.g. a compromised intermediate whose issuing root
        // is not active) — it is logged and yields a null replacement, never a rollback.
        var replacementResult = await BuildReplacementAsync(compromised, ct)
            .ConfigureAwait(false);

        if (!replacementResult.Success)
        {
            KeyCustodianLog.ReplacementGenerationFailed(
                Context.Logger, compromisedDomain, replacementResult.ErrorCode);

            return null;
        }

        var replacement = replacementResult.Data!;
        db.Keys.Add(replacement.ToNewRecord());

        db.Audit.Add(
            EncryptionKeyAudit.Record(
                replacement.Kid, KeyAuditAction.Generated, KeyStatus.Pending, clock)
            .ToRecord());

        // Save #2 — separate, best-effort. A racing pending insert hitting the
        // one-pending-per-domain index (23505) is logged, not fatal; the compromise is
        // already durable, so a null replacement kid is returned.
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            KeyCustodianLog.ReplacementGenerationFailed(
                Context.Logger, compromisedDomain, _REPLACEMENT_SAVE_CONFLICT);

            return null;
        }

        KeyCustodianLog.ReplacementKeyGenerated(
            Context.Logger, compromisedDomain, replacement.Kid.Value);

        return replacement.Kid.Value;
    }

    // Compromise lives only on the live sealed states (Pending / Active /
    // Retiring) — the type switch is exhaustive over the LIVE filter applied by
    // the query, so a terminal state here is a corrupt-row programmer error.
    private D2Result<CompromisedKey> CompromiseLiveKey(EncryptionKey key, string reason) =>
        key switch
        {
            PendingKey pending => pending.Compromise(reason, clock),
            ActiveKey active => active.Compromise(reason, clock),
            RetiringKey retiring => retiring.Compromise(reason, clock),
            _ => KeyCustodianFailures<CompromisedKey>.KeyStateConflict(),
        };

    private async Task<D2Result<PendingKey>> BuildReplacementAsync(
        CompromisedKey compromised, CancellationToken ct)
    {
        // CA-certificate keys take the dedicated CA-generation path (subject +
        // issuer are not expressible through the symmetric/RSA KeyGeneration rule).
        // A compromised intermediate gets a real root-signed replacement; a
        // compromised root gets a real self-signed replacement — the re-anchor case.
        if (compromised.KeyType == KeyType.X509CaCertificate)
        {
            return await CaSuccessorFactory
                .BuildAsync(
                    db,
                    rootCrypto,
                    rootSigning,
                    options.Value,
                    clock,
                    compromised.KeyDomain,
                    KeyCustodianMetrics.CaRootKeyUses.Operation.COMPROMISE_REPLACEMENT,
                    ct)
                .ConfigureAwait(false);
        }

        var genResult = KeyGeneration.Generate(
            compromised.KeyType, options.Value.RsaKeySizeBits, options.Value.SecretLengthBytes);

        if (!genResult.Success)
            return D2Result<PendingKey>.BubbleFail(genResult);

        var generated = genResult.Data!;

        byte[] wrapped;

        try
        {
            wrapped = rootCrypto.Encrypt(generated.Plaintext);
        }
        finally
        {
            generated.Zero();
        }

        var encryptedMaterial = KeyMaterialEncrypted.FromTrusted(wrapped);
        PublicKeyMaterial? publicMaterial = generated.PublicSpki is { } spki
            ? PublicKeyMaterial.FromTrusted(spki)
            : null;

        var kid = Kid.FromTrusted(KidMinting.Mint());
        return PendingKey.Create(
            kid,
            compromised.KeyDomain,
            compromised.KeyType,
            encryptedMaterial,
            publicMaterial,
            caCertificateMaterial: null,
            clock.GetCurrentInstant());
    }
}
