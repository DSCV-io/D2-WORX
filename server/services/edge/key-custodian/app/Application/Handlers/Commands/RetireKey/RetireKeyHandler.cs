// -----------------------------------------------------------------------
// <copyright file="RetireKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;

/// <summary>
/// Retires a retiring key once its grace window has elapsed.
/// </summary>
/// <remarks>
/// Validates the kid at the top, loads the tracked record (not found → 404; not
/// retiring → 409), retires the aggregate (propagating <c>GRACE_NOT_ELAPSED</c>),
/// projects the result, appends a <c>Retired</c> audit entry, and saves once.
/// </remarks>
public sealed class RetireKeyHandler(
    HandlerContext<RetireKeyHandler> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    IRotationPolicyProvider policyProvider,
    IClock clock)
    : BaseRepoHandler<RetireKeyHandler, RetireKeyInput, KeySummary>(ctx, classifier),
      IRetireKeyHandler
{
    /// <inheritdoc/>
    protected override async ValueTask<D2Result<KeySummary?>> ExecuteAsync(
        RetireKeyInput input, CancellationToken ct)
    {
        var kidResult = Kid.Create(input.Kid);
        if (kidResult.BubbleOnFailure<Kid, KeySummary>(out var bubbled, out var kid))
            return bubbled;

        var record = await db.Keys
            .FirstOrDefaultAsync(k => k.Kid == kid!.Value, ct)
            .ConfigureAwait(false);
        if (record is null)
            return KeyCustodianFailures<KeySummary?>.KeyNotFound();

        if (record.Status != KeyStatus.Retiring)
            return KeyCustodianFailures<KeySummary?>.KeyStateConflict();

        if (record.ToDomain() is not RetiringKey retiring)
            return KeyCustodianFailures<KeySummary?>.KeyStateConflict();

        var policyResult = policyProvider.ForDomain(retiring.KeyDomain);
        if (policyResult.BubbleOnFailure<RotationPolicy, KeySummary>(
            out var policyBubble, out var policy))
            return policyBubble;

        var retireResult = retiring.Retire(policy, clock);
        if (retireResult.BubbleOnFailure<RetiredKey, KeySummary>(
            out var retireBubble, out var retired))
            return retireBubble;

        retired!.ProjectOnto(record);
        db.Audit.Add(
            EncryptionKeyAudit.Record(kid!, KeyAuditAction.Retired, KeyStatus.Retired, clock)
            .ToRecord());

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return D2Result<KeySummary?>.Ok(KeySummary.From(retired!));
    }
}
