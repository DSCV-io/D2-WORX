// -----------------------------------------------------------------------
// <copyright file="RotateKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RotateKey;

using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using D2.Edge.KeyCustodian.App.Application.Observability;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.App.Infrastructure.Messaging;
using D2.Edge.KeyCustodian.App.Infrastructure.Persistence;
using D2.Edge.KeyCustodian.App.Infrastructure.Vault;
using D2.Edge.KeyCustodian.Domain.Entities;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.Errors;
using D2.Edge.KeyCustodian.Domain.Rules;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Encryption;
using D2.Shared.Handler.Repo;
using D2.Shared.Handler.Repo.Abstractions;
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
public sealed class RotateKeyHandler(
    HandlerContext<RotateKeyHandler> ctx,
    IDbExceptionClassifier classifier,
    IKeyCustodianDbContext db,
    IRotationPolicyProvider policyProvider,
    IKeyRotationAnnouncer announcer,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IClock clock)
    : BaseRepoHandler<RotateKeyHandler, RotateKeyInput, RotateKeyOutput>(ctx, classifier),
      IRotateKeyHandler
{
    /// <inheritdoc/>
    protected override async ValueTask<D2Result<RotateKeyOutput?>> ExecuteAsync(
        RotateKeyInput input, CancellationToken ct)
    {
        var domainResult = KeyDomain.Create(input.Domain);
        if (domainResult.BubbleOnFailure<KeyDomain, RotateKeyOutput>(
            out var bubbled, out var domain))
            return bubbled;

        var incumbentRecord = await db.Keys
            .ForDomain(domain!.Value)
            .Active()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (incumbentRecord is null)
            return KeyCustodianFailures<RotateKeyOutput?>.KeyNotFound();

        var successorRecord = await db.Keys
            .ForDomain(domain.Value)
            .Pending()
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (successorRecord is null)
            return KeyCustodianFailures<RotateKeyOutput?>.KeyNotFound();

        var incumbent = (ActiveKey)incumbentRecord.ToDomain();
        var successor = (PendingKey)successorRecord.ToDomain();

        // Smoke-test the successor before swapping it into active service.
        var unwrapped = rootCrypto.Decrypt(successor.KeyMaterialEncrypted.Bytes.Span);
        D2Result smokeResult;
        try
        {
            smokeResult = SmokeTesting.Verify(
                successor.KeyType, unwrapped, successor.PublicKeyMaterial?.Bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(unwrapped);
        }

        if (!smokeResult.Success)
        {
            KeyCustodianLog.SmokeTestFailed(
                Context.Logger, successor.Kid.Value, successor.KeyType.ToString());
            KeyCustodianMetrics.SR_SmokeTestFailuresTotal.Add(1);
            return D2Result<RotateKeyOutput?>.BubbleFail(smokeResult);
        }

        var proofResult = SmokeProof.ForPassedSmokeTest(successor.KeyType, clock);
        if (proofResult.BubbleOnFailure<SmokeProof, RotateKeyOutput>(
            out var proofBubble, out var proof))
            return proofBubble;

        var policyResult = policyProvider.ForDomain(domain);
        if (policyResult.BubbleOnFailure<RotationPolicy, RotateKeyOutput>(
            out var policyBubble, out var policy))
            return policyBubble;

        // 1) incumbent → retiring.
        var rotateResult = incumbent.Rotate(successor, clock);
        if (rotateResult
            .BubbleOnFailure<(RetiringKey Retiring, PendingKey Successor), RotateKeyOutput>(
                out var rotateBubble, out var rotated))
            return rotateBubble;

        // 2) successor → active (soak already elapsed for a rotation candidate).
        var activateResult = successor.Activate(proof, policy, clock);
        if (activateResult.BubbleOnFailure<ActiveKey, RotateKeyOutput>(
            out var activateBubble, out var activated))
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

        KeyCustodianLog.RotationCompleted(
            Context.Logger, domain.Value, incumbent.Kid.Value, activated.Kid.Value);

        // D-4: announce after the durable commit; a failure is logged, not fatal.
        var announceResult = await announcer
            .AnnounceAsync(domain, activated.Kid, KeyStatus.Active, urgent: false, ct)
            .ConfigureAwait(false);
        if (!announceResult.Success)
        {
            KeyCustodianLog.AnnounceFailed(
                Context.Logger, domain.Value, activated.Kid.Value, announceResult.ErrorCode);
            KeyCustodianMetrics.SR_AnnounceFailuresTotal.Add(
                1, new KeyValuePair<string, object?>("urgent", "false"));
        }

        return D2Result<RotateKeyOutput?>.Ok(
            new RotateKeyOutput(incumbent.Kid.Value, activated.Kid.Value));
    }
}
