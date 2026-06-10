// -----------------------------------------------------------------------
// <copyright file="CompromiseKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Implementations.CQRS.Handlers.C;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using D2.Edge.KeyCustodian.App.Crypto;
using D2.Edge.KeyCustodian.App.Interfaces.CQRS.Handlers.C;
using D2.Edge.KeyCustodian.App.Interfaces.Crypto;
using D2.Edge.KeyCustodian.App.Interfaces.Messaging.Pub;
using D2.Edge.KeyCustodian.App.Logging;
using D2.Edge.KeyCustodian.App.Models;
using D2.Edge.KeyCustodian.App.Persistence;
using D2.Edge.KeyCustodian.Domain.Audit;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.Errors;
using D2.Edge.KeyCustodian.Domain.Keys;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Encryption;
using D2.Shared.Handler;
using D2.Shared.Handler.Abstractions;
using D2.Shared.Handler.Repo;
using D2.Shared.Handler.Repo.Abstractions;
using D2.Shared.I18n;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using IClock = D2.Shared.Time.IClock;

/// <summary>
/// Marks a live key compromised and (by default) auto-generates a replacement
/// pending key (gate D-3).
/// </summary>
/// <remarks>
/// Validates the operator <c>Reason</c> at the top as a 400 input error (operator
/// input — NOT the domain's 500 defense-in-depth guard), loads the LIVE key by
/// kid (not found among live keys → 404), compromises it via the sealed state's
/// <c>Compromise</c> method, projects the result, appends a <c>Compromised</c>
/// audit entry carrying a NON-SENSITIVE breadcrumb (NEVER the raw reason), and —
/// when requested — generates a replacement pending key for the same domain.
/// Announces the compromise urgently (D-4 failure semantics); a post-commit
/// announce failure is logged, not fatal.
/// </remarks>
public sealed class CompromiseKey(
    HandlerContext<CompromiseKey> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    IEnumerable<IKeyGenerator> generators,
    IKeyRotationAnnouncer announcer,
    [FromKeyedServices(KeyCustodianCrypto.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IClock clock)
    : BaseRepoHandler<CompromiseKey, CompromiseKeyInput, CompromiseOutcome>(ctx, classifier),
      ICompromiseKey
{
    private const string _AUDIT_DETAIL = "operator-initiated";

    /// <inheritdoc/>
    /// <remarks>
    /// <c>LogInput = false</c> — <see cref="CompromiseKeyInput"/> carries
    /// <c>[RedactData] Reason</c>, but the Serilog destructuring policy is an
    /// optional wire-up; disabling input logging removes the dependency entirely
    /// (defense-in-depth — the sensitive operator reason never enters logs).
    /// </remarks>
    protected override HandlerOptions DefaultOptions => new() { LogInput = false };

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<CompromiseOutcome?>> ExecuteAsync(
        CompromiseKeyInput input, CancellationToken ct)
    {
        // Operator input — a missing reason is a 400 validation error, NOT the
        // domain's 500 precondition guard.
        if (input.Reason.Falsey())
        {
            return D2Result<CompromiseOutcome?>.ValidationFailed(
                inputErrors:
                [
                    new InputError(
                        nameof(CompromiseKeyInput.Reason), [TK.Common.Errors.NOT_NULL_VIOLATION]),
                ]);
        }

        var kidResult = Kid.Create(input.Kid);
        if (kidResult.BubbleOnFailure<Kid, CompromiseOutcome>(out var bubbled, out var kid))
            return bubbled;

        var record = await db.Keys
            .Live()
            .FirstOrDefaultAsync(k => k.Kid == kid!.Value, ct)
            .ConfigureAwait(false);
        if (record is null)
            return KeyCustodianFailures<CompromiseOutcome?>.KeyNotFound();

        var compromiseResult = CompromiseLiveKey(record.ToDomain(), input.Reason!);
        if (compromiseResult.BubbleOnFailure<CompromisedKey, CompromiseOutcome>(out var compBubble, out var compromised))
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
            if (replacementResult.BubbleOnFailure<PendingKey, CompromiseOutcome>(out var replBubble, out var replacement))
                return replBubble;

            db.Keys.Add(replacement!.ToNewRecord());
            db.Audit.Add(
                EncryptionKeyAudit.Record(
                    replacement!.Kid, KeyAuditAction.Generated, KeyStatus.Pending, clock).ToRecord());
            replacementKid = replacement.Kid.Value;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        KeyCustodianMetrics.SR_CompromisesTotal.Add(1);

        if (replacementKid is not null)
        {
            KeyCustodianAppLog.ReplacementKeyGenerated(
                Context.Logger, compromised!.KeyDomain.Value, replacementKid);
        }

        // D-4: urgent announce after the durable commit; a failure is logged, not fatal.
        var announceResult = await announcer
            .AnnounceAsync(
                compromised!.KeyDomain, compromised.Kid, KeyStatus.Compromised, urgent: true, ct)
            .ConfigureAwait(false);
        if (!announceResult.Success)
        {
            KeyCustodianAppLog.AnnounceFailed(
                Context.Logger, compromised.KeyDomain.Value, compromised.Kid.Value, announceResult.ErrorCode);
            KeyCustodianMetrics.SR_AnnounceFailuresTotal.Add(
                1, new KeyValuePair<string, object?>("urgent", "true"));
        }

        return D2Result<CompromiseOutcome?>.Ok(
            new CompromiseOutcome(compromised.Kid.Value, replacementKid));
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

    private D2Result<PendingKey> TryBuildReplacement(CompromisedKey compromised, NodaTime.Instant now)
    {
        var generator = generators.FirstOrDefault(g => g.Handles == compromised.KeyType);
        if (generator is null)
        {
            return KeyCustodianFailures<PendingKey>.PreconditionViolated(
                messages: [TK.Keycustodian.Internal.PRECONDITION_VIOLATED]);
        }

        var generated = generator.Generate();
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

        var kid = Kid.FromTrusted(KeyCustodianCrypto.MintKid());
        return PendingKey.Create(
            kid, compromised.KeyDomain, compromised.KeyType, encryptedMaterial, publicMaterial, now);
    }
}
