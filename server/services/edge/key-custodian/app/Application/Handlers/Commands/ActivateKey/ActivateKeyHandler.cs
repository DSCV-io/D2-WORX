// -----------------------------------------------------------------------
// <copyright file="ActivateKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;

/// <summary>
/// Smoke-tests and activates a pending key.
/// </summary>
/// <remarks>
/// Validates the kid at the top, loads the tracked pending record (not found →
/// 404; not pending → 409), unwraps + smoke-tests the material (failure →
/// <c>KEYCUSTODIAN_SMOKE_TEST_FAILED</c>, with the unwrapped bytes zeroed on
/// every path), builds the <see cref="SmokeProof"/>, resolves the domain policy,
/// activates the aggregate (propagating <c>SOAK_NOT_ELAPSED</c> /
/// <c>SMOKE_PROOF_TYPE_MISMATCH</c>), projects the result back, appends an
/// <c>Activated</c> audit entry, and saves once.
/// </remarks>
public sealed class ActivateKeyHandler(
    HandlerContext<ActivateKeyHandler> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    IRotationPolicyProvider policyProvider,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IClock clock)
    : BaseRepoHandler<ActivateKeyHandler, ActivateKeyInput, KeySummary>(ctx, classifier),
      IActivateKeyHandler
{
    /// <inheritdoc/>
    protected override async ValueTask<D2Result<KeySummary?>> ExecuteAsync(
        ActivateKeyInput input, CancellationToken ct)
    {
        var kidResult = Kid.Create(input.Kid);
        if (kidResult.BubbleOnFailure<Kid, KeySummary>(out var bubbled, out var kid))
            return bubbled;

        var record = await db.Keys
            .FirstOrDefaultAsync(k => k.Kid == kid!.Value, ct)
            .ConfigureAwait(false);
        if (record is null)
            return KeyCustodianFailures<KeySummary?>.KeyNotFound();

        if (record.Status != KeyStatus.Pending)
            return KeyCustodianFailures<KeySummary?>.KeyStateConflict();

        var pending = (PendingKey)record.ToDomain();

        // Unwrap, smoke-test, and zero the unwrapped bytes on every path.
        var unwrapped = rootCrypto.Decrypt(pending.KeyMaterialEncrypted.Bytes.Span);
        D2Result smokeResult;
        try
        {
            smokeResult = SmokeTesting.Verify(
                pending.KeyType, unwrapped, pending.PublicKeyMaterial?.Bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(unwrapped);
        }

        if (!smokeResult.Success)
        {
            KeyCustodianLog.SmokeTestFailed(
                Context.Logger, kid!.Value, pending.KeyType.ToString());
            KeyCustodianMetrics.SR_SmokeTestFailuresTotal.Add(1);
            return D2Result<KeySummary?>.BubbleFail(smokeResult);
        }

        var proofResult = SmokeProof.ForPassedSmokeTest(pending.KeyType, clock);
        if (proofResult.BubbleOnFailure<SmokeProof, KeySummary>(out var proofBubble, out var proof))
            return proofBubble;

        var policyResult = policyProvider.ForDomain(pending.KeyDomain);
        if (policyResult.BubbleOnFailure<RotationPolicy, KeySummary>(
            out var policyBubble, out var policy))
            return policyBubble;

        var activateResult = pending.Activate(proof, policy, clock);
        if (activateResult.BubbleOnFailure<ActiveKey, KeySummary>(
            out var activateBubble, out var active))
            return activateBubble;

        active!.ProjectOnto(record);
        db.Audit.Add(
            EncryptionKeyAudit.Record(kid!, KeyAuditAction.Activated, KeyStatus.Active, clock)
            .ToRecord());

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return D2Result<KeySummary?>.Ok(KeySummary.From(active!));
    }
}
