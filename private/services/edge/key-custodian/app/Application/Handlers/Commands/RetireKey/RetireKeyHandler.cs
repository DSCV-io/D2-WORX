// -----------------------------------------------------------------------
// <copyright file="RetireKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;

using H = DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey.IRetireKeyHandler;
using I = RetireKeyInput;
using O = DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Rules.KeySummary;

/// <summary>
/// Retires a retiring key once its grace window has elapsed.
/// </summary>
/// <remarks>
/// Authority precedes work: the System-plane-only
/// <see cref="KeyLifecycleAuthority.AuthorizeLifecycleMutation"/> gate runs FIRST
/// (fail-closed; deny emits the lifecycle authority-rejection telemetry). Then
/// validates the kid, loads the tracked record (not found → 404; not
/// retiring → 409), retires the aggregate (propagating <c>GRACE_NOT_ELAPSED</c>),
/// projects the result, appends a <c>Retired</c> audit entry, and saves once.
/// </remarks>
public sealed class RetireKeyHandler(
    HandlerContext<RetireKeyHandler> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    IRotationPolicyProvider policyProvider,
    IClock clock)
    : BaseRepoHandler<RetireKeyHandler, I, O>(ctx, classifier),
      H
{
    /// <inheritdoc/>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input, CancellationToken ct)
    {
        // Authority precedes work: lifecycle mutations are System-plane-only, fail-closed.
        var authorityResult =
            KeyLifecycleAuthority.AuthorizeLifecycleMutation(Context.Request.Origin);

        if (authorityResult.Failed)
        {
            return LifecycleAuthorityTelemetry.Deny<O>(
                Context.Logger, authorityResult, Context.Request.ImmediateCaller, "retire-key");
        }

        var kidResult = Kid.Create(input.Kid);

        if (kidResult.BubbleOnFailure<Kid, O>(out var bubbled, out var kid))
            return bubbled;

        var record = await db.Keys
            .FirstOrDefaultAsync(k => k.Kid == kid!.Value, ct)
            .ConfigureAwait(false);

        if (record is null)
            return KeyCustodianFailures<O?>.KeyNotFound();

        if (record.Status != KeyStatus.Retiring)
            return KeyCustodianFailures<O?>.KeyStateConflict();

        if (record.ToDomain() is not RetiringKey retiring)
            return KeyCustodianFailures<O?>.KeyStateConflict();

        var policyResult = policyProvider.ForDomain(retiring.KeyDomain);

        if (policyResult.BubbleOnFailure<RotationPolicy, O>(
            out var policyBubble, out var policy))
            return policyBubble;

        var retireResult = retiring.Retire(policy, clock);

        if (retireResult.BubbleOnFailure<RetiredKey, O>(
            out var retireBubble, out var retired))
            return retireBubble;

        retired!.ProjectOnto(record);

        db.Audit.Add(
            EncryptionKeyAudit.Record(kid!, KeyAuditAction.Retired, KeyStatus.Retired, clock)
            .ToRecord());

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return D2Result<O?>.Ok(O.From(retired!));
    }
}
