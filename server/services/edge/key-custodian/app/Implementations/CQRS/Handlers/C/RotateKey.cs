// -----------------------------------------------------------------------
// <copyright file="RotateKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Implementations.CQRS.Handlers.C;

using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using D2.Edge.KeyCustodian.App.Crypto;
using D2.Edge.KeyCustodian.App.Interfaces.CQRS.Handlers.C;
using D2.Edge.KeyCustodian.App.Interfaces.Crypto;
using D2.Edge.KeyCustodian.App.Interfaces.Messaging.Pub;
using D2.Edge.KeyCustodian.App.Interfaces.Policy;
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
using D2.Shared.Handler.Repo;
using D2.Shared.Handler.Repo.Abstractions;
using D2.Shared.Result;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using IClock = D2.Shared.Time.IClock;

/// <summary>
/// Atomically rotates a domain's active incumbent to its soaked pending
/// successor (gate D-2).
/// </summary>
/// <remarks>
/// Validates the domain at the top, loads the tracked active incumbent + the
/// tracked pending successor (each missing → 404), smoke-tests the successor,
/// rotates the incumbent to retiring AND activates the successor in ONE
/// <see cref="IKeyCustodianDbContext.SaveChangesAsync"/> (no gap with no active
/// signing key), then announces the rotation. A post-commit announce failure is
/// logged but does not fail the handler (D-4).
/// </remarks>
public sealed class RotateKey(
    HandlerContext<RotateKey> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    ISmokeTester smokeTester,
    IRotationPolicyProvider policyProvider,
    IKeyRotationAnnouncer announcer,
    [FromKeyedServices(KeyCustodianCrypto.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IClock clock)
    : BaseRepoHandler<RotateKey, RotateKeyInput, RotationOutcome>(ctx, classifier), IRotateKey
{
    /// <inheritdoc/>
    protected override async ValueTask<D2Result<RotationOutcome?>> ExecuteAsync(
        RotateKeyInput input, CancellationToken ct)
    {
        var domainResult = KeyDomain.Create(input.Domain);
        if (domainResult.BubbleOnFailure<KeyDomain, RotationOutcome>(out var bubbled, out var domain))
            return bubbled;

        var incumbentRecord = await db.Keys
            .ForDomain(domain!.Value)
            .Active()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (incumbentRecord is null)
            return KeyCustodianFailures<RotationOutcome?>.KeyNotFound();

        var successorRecord = await db.Keys
            .ForDomain(domain.Value)
            .Pending()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (successorRecord is null)
            return KeyCustodianFailures<RotationOutcome?>.KeyNotFound();

        var incumbent = (ActiveKey)incumbentRecord.ToDomain();
        var successor = (PendingKey)successorRecord.ToDomain();

        // Smoke-test the successor before swapping it into active service.
        var unwrapped = rootCrypto.Decrypt(successor.KeyMaterialEncrypted.Bytes.Span);
        D2Result smokeResult;
        try
        {
            smokeResult = smokeTester.Verify(
                successor.KeyType, unwrapped, successor.PublicKeyMaterial?.Bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(unwrapped);
        }

        if (!smokeResult.Success)
        {
            KeyCustodianAppLog.SmokeTestFailed(
                Context.Logger, successor.Kid.Value, successor.KeyType.ToString());
            KeyCustodianMetrics.SR_SmokeTestFailuresTotal.Add(1);
            return D2Result<RotationOutcome?>.BubbleFail(smokeResult);
        }

        var proofResult = SmokeProof.ForPassedSmokeTest(successor.KeyType, clock);
        if (proofResult.BubbleOnFailure<SmokeProof, RotationOutcome>(out var proofBubble, out var proof))
            return proofBubble;

        var policyResult = policyProvider.ForDomain(domain);
        if (policyResult.BubbleOnFailure<RotationPolicy, RotationOutcome>(out var policyBubble, out var policy))
            return policyBubble;

        // 1) incumbent → retiring.
        var rotateResult = incumbent.Rotate(successor, clock);
        if (rotateResult.BubbleOnFailure<(RetiringKey Retiring, PendingKey Successor), RotationOutcome>(out var rotateBubble, out var rotated))
            return rotateBubble;

        // 2) successor → active (soak already elapsed for a rotation candidate).
        var activateResult = successor.Activate(proof, policy, clock);
        if (activateResult.BubbleOnFailure<ActiveKey, RotationOutcome>(out var activateBubble, out var activated))
            return activateBubble;

        var retiring = rotated.Retiring;
        retiring.ProjectOnto(incumbentRecord);
        activated!.ProjectOnto(successorRecord);
        db.Audit.Add(
            EncryptionKeyAudit.Record(
                incumbent.Kid, KeyAuditAction.Rotated, KeyStatus.Retiring, clock).ToRecord());
        db.Audit.Add(
            EncryptionKeyAudit.Record(
                activated!.Kid, KeyAuditAction.Activated, KeyStatus.Active, clock).ToRecord());

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        KeyCustodianAppLog.RotationCompleted(
            Context.Logger, domain.Value, incumbent.Kid.Value, activated.Kid.Value);

        // D-4: announce after the durable commit; a failure is logged, not fatal.
        var announceResult = await announcer
            .AnnounceAsync(domain, activated.Kid, KeyStatus.Active, urgent: false, ct)
            .ConfigureAwait(false);
        if (!announceResult.Success)
        {
            KeyCustodianAppLog.AnnounceFailed(
                Context.Logger, domain.Value, activated.Kid.Value, announceResult.ErrorCode);
            KeyCustodianMetrics.SR_AnnounceFailuresTotal.Add(
                1, new KeyValuePair<string, object?>("urgent", "false"));
        }

        return D2Result<RotationOutcome?>.Ok(
            new RotationOutcome(incumbent.Kid.Value, activated.Kid.Value));
    }
}
