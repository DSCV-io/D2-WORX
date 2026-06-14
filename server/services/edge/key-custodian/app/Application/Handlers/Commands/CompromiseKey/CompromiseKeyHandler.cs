// -----------------------------------------------------------------------
// <copyright file="CompromiseKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.CompromiseKey;

/// <summary>
/// Marks a live key compromised and (by default) auto-generates a replacement
/// pending key.
/// </summary>
/// <remarks>
/// Validates the operator <c>Reason</c> at the top as a 400 input error (operator
/// input — NOT the domain's 500 defense-in-depth guard), loads the LIVE key by
/// kid (not found among live keys → 404), compromises it via the sealed state's
/// <c>Compromise</c> method, projects the result, appends a <c>Compromised</c>
/// audit entry carrying a NON-SENSITIVE breadcrumb (NEVER the raw reason), and —
/// when requested — generates a replacement pending key for the same domain.
/// Announces the compromise urgently after the durable commit; a post-commit
/// announce failure is logged, not fatal.
/// </remarks>
public sealed class CompromiseKeyHandler(
    HandlerContext<CompromiseKeyHandler> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    IOptions<KeyCustodianOptions> options,
    IKeyRotationAnnouncer announcer,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IClock clock)
    : BaseRepoHandler<CompromiseKeyHandler, CompromiseKeyInput, CompromiseKeyOutput>(
        ctx, classifier),
      ICompromiseKeyHandler
{
    private const string _AUDIT_DETAIL = "operator-initiated";

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
    protected override async ValueTask<D2Result<CompromiseKeyOutput?>> ExecuteAsync(
        CompromiseKeyInput input, CancellationToken ct)
    {
        // Operator input — a missing reason is a 400 validation error, NOT the
        // domain's 500 precondition guard.
        if (input.Reason.Falsey())
        {
            return D2Result<CompromiseKeyOutput?>.ValidationFailed(
                inputErrors:
                [
                    new InputError(
                        nameof(CompromiseKeyInput.Reason), [TK.Common.Errors.NOT_NULL_VIOLATION]),
                ]);
        }

        var kidResult = Kid.Create(input.Kid);
        if (kidResult.BubbleOnFailure<Kid, CompromiseKeyOutput>(out var bubbled, out var kid))
            return bubbled;

        var record = await db.Keys
            .Live()
            .FirstOrDefaultAsync(k => k.Kid == kid!.Value, ct)
            .ConfigureAwait(false);
        if (record is null)
            return KeyCustodianFailures<CompromiseKeyOutput?>.KeyNotFound();

        var compromiseResult = CompromiseLiveKey(record.ToDomain(), input.Reason!);
        if (compromiseResult.BubbleOnFailure<CompromisedKey, CompromiseKeyOutput>(
            out var compBubble, out var compromised))
            return compBubble;

        compromised!.ProjectOnto(record);
        db.Audit.Add(
            EncryptionKeyAudit.Record(
                kid!, KeyAuditAction.Compromised, KeyStatus.Compromised, clock, _AUDIT_DETAIL)
            .ToRecord());

        string? replacementKid = null;
        if (input.GenerateReplacement)
        {
            var replacementResult = TryBuildReplacement(compromised!, clock.GetCurrentInstant());
            if (replacementResult.BubbleOnFailure<PendingKey, CompromiseKeyOutput>(
                out var replBubble, out var replacement))
                return replBubble;

            db.Keys.Add(replacement!.ToNewRecord());
            db.Audit.Add(
                EncryptionKeyAudit.Record(
                    replacement!.Kid, KeyAuditAction.Generated, KeyStatus.Pending, clock)
                .ToRecord());
            replacementKid = replacement.Kid.Value;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        KeyCustodianMetrics.SR_CompromisesTotal.Add(1);

        if (replacementKid is not null)
        {
            KeyCustodianLog.ReplacementKeyGenerated(
                Context.Logger, compromised!.KeyDomain.Value, replacementKid);
        }

        // Post-commit announce: urgent after the durable commit; a failure is logged, not fatal.
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

        return D2Result<CompromiseKeyOutput?>.Ok(
            new CompromiseKeyOutput(compromised.Kid.Value, replacementKid));
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

    private D2Result<PendingKey> TryBuildReplacement(
        CompromisedKey compromised, NodaTime.Instant now)
    {
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
            now);
    }
}
