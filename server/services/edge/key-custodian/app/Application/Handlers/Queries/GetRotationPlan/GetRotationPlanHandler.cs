// -----------------------------------------------------------------------
// <copyright file="GetRotationPlanHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetRotationPlan;

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.App.Infrastructure.Persistence;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using IClock = D2.Shared.Time.IClock;

/// <summary>
/// Computes the read-only plan of lifecycle actions due across all key domains.
/// </summary>
/// <remarks>
/// Pure read: loads all live keys (no-tracking), walks the
/// <see cref="KeyDomain.All"/> catalog, and classifies each domain against its
/// policy windows using the injected clock. Execution / orchestration is the
/// Infra-layer rotation service's job — this handler only reports.
/// </remarks>
public sealed class GetRotationPlanHandler(
    HandlerContext<GetRotationPlanHandler> ctx,
    IKeyCustodianDbContext db,
    IRotationPolicyProvider policyProvider,
    IClock clock)
    : BaseHandler<GetRotationPlanHandler, GetRotationPlanInput, GetRotationPlanOutput>(ctx), IGetRotationPlanHandler
{
    /// <inheritdoc/>
    protected override async ValueTask<D2Result<GetRotationPlanOutput?>> ExecuteAsync(
        GetRotationPlanInput input, CancellationToken ct)
    {
        var liveKeys = await db.Keys
            .AsNoTracking()
            .Live()
            .Select(k => new LiveKeyView(k.KeyDomain, k.Status, k.CreatedAt, k.ActivatedAt, k.RetiringAt))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var now = clock.GetCurrentInstant();
        var byDomain = liveKeys.ToLookup(k => k.KeyDomain);

        var bootstrap = new List<string>();
        var dueToActivate = new List<string>();
        var dueToRotate = new List<string>();
        var dueToGenerateSuccessor = new List<string>();
        var dueToRetire = new List<string>();

        foreach (var domain in KeyDomain.All)
        {
            var keys = byDomain[domain.Value].ToList();

            var policyResult = policyProvider.ForDomain(domain);
            if (policyResult.BubbleOnFailure<RotationPolicy, GetRotationPlanOutput>(out var bubbled, out var policy))
                return bubbled;

            if (keys.Count == 0)
            {
                bootstrap.Add(domain.Value);
                continue;
            }

            var active = keys.FirstOrDefault(k => k.Status == KeyStatus.Active);
            var pending = keys.FirstOrDefault(k => k.Status == KeyStatus.Pending);

            // A soaked pending key with no active incumbent → activate (bootstrap path).
            if (pending is not null && active is null
                && now - pending.CreatedAt >= policy!.SmokeSoak)
                dueToActivate.Add(domain.Value);

            // An active key whose cadence has elapsed (measured from activation).
            if (active is { ActivatedAt: { } activatedAt }
                && now - activatedAt >= policy!.Cadence)
            {
                // A soaked pending successor exists → rotate; else generate one.
                if (pending is not null && now - pending.CreatedAt >= policy.SmokeSoak)
                    dueToRotate.Add(domain.Value);
                else if (pending is null)
                    dueToGenerateSuccessor.Add(domain.Value);
            }

            // Retiring keys whose grace window has elapsed → retire.
            foreach (var retiring in keys.Where(k => k.Status == KeyStatus.Retiring))
            {
                if (retiring.RetiringAt is { } retiringAt && now - retiringAt >= policy!.Grace)
                    dueToRetire.Add(domain.Value);
            }
        }

        return D2Result<GetRotationPlanOutput?>.Ok(
            new GetRotationPlanOutput(bootstrap, dueToActivate, dueToRotate, dueToGenerateSuccessor, dueToRetire));
    }

    /// <summary>Projected read view of a live key — only the fields the plan needs.</summary>
    private sealed record LiveKeyView(
        string KeyDomain,
        KeyStatus Status,
        Instant CreatedAt,
        Instant? ActivatedAt,
        Instant? RetiringAt);
}
