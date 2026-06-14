// -----------------------------------------------------------------------
// <copyright file="RunDueRotationsHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;

using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RotateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetRotationPlan;

/// <summary>
/// Orchestrates all key-lifecycle actions that are currently due across all
/// domains by composing the read-only plan from
/// <see cref="IGetRotationPlanHandler"/> with the per-action command handlers.
/// </summary>
/// <remarks>
/// <para>
/// Execution order within a run: bootstrap → activate → rotate →
/// generate-successor → retire. Per-domain failures are isolated: a failure in
/// one domain does not prevent the other domains from being serviced.
/// The number of failures is surfaced in
/// <see cref="RunDueRotationsOutput.Errors"/> for alerting.
/// </para>
/// <para>
/// For the <c>DueToGenerateSuccessor</c> and <c>DueToActivate</c> paths the
/// handler resolves the key type by loading the relevant live key's
/// <see cref="KeyType"/> from the store (the active key for generate-successor,
/// the pending key for activate). For bootstrap the caller supplies the key type
/// via <see cref="RunDueRotationsInput.BootstrapKeyTypes"/>. Domains that need
/// bootstrap but have no entry in that map are counted in
/// <see cref="RunDueRotationsOutput.Skipped"/> without error.
/// </para>
/// <para>
/// This handler is <see cref="BaseHandler{TSelf, TInput, TOutput}"/>, not
/// <c>BaseRepoHandler</c> — it composes sub-handlers rather than writing directly
/// to the database. All DB mutations flow through the injected sub-handlers.
/// </para>
/// </remarks>
public sealed class RunDueRotationsHandler(
    HandlerContext<RunDueRotationsHandler> ctx,
    IKeyCustodianDbContext db,
    IGetRotationPlanHandler getPlan,
    IGenerateKeyHandler generate,
    IActivateKeyHandler activate,
    IRotateKeyHandler rotate,
    IRetireKeyHandler retire)
    : BaseHandler<RunDueRotationsHandler, RunDueRotationsInput, RunDueRotationsOutput>(ctx),
      IRunDueRotationsHandler
{
    /// <inheritdoc/>
    /// <remarks>
    /// Orchestrates bootstrap / activate / rotate / generate / retire across all
    /// domains — wall-clock routinely exceeds the platform default slow-handler
    /// thresholds (100ms warn / 500ms error).
    /// </remarks>
    protected override HandlerOptions DefaultOptions => new()
    {
        SlowThreshold = TimeSpan.FromSeconds(30),
        CriticalThreshold = TimeSpan.FromSeconds(120),
    };

    /// <inheritdoc/>
    protected override async ValueTask<D2Result<RunDueRotationsOutput?>> ExecuteAsync(
        RunDueRotationsInput input, CancellationToken ct)
    {
        var planResult = await getPlan
            .HandleAsync(new GetRotationPlanInput(), ct)
            .ConfigureAwait(false);
        if (!planResult.Success)
            return D2Result<RunDueRotationsOutput?>.BubbleFail(planResult);

        var plan = planResult.Data!;

        var bootstrapped = new List<string>();
        var activated = new List<string>();
        var rotated = new List<string>();
        var successorsGenerated = new List<string>();
        var retired = new List<string>();
        var skipped = new List<string>();
        var errors = 0;

        // Bootstrap — domains with no live keys: generate a new pending key.
        foreach (var domain in plan.DomainsToBootstrap)
        {
            if (!input.BootstrapKeyTypes.TryGetValue(domain, out var keyType))
            {
                KeyCustodianLog.BootstrapKeyTypeMissing(Context.Logger, domain);
                skipped.Add(domain);
                continue;
            }

            var result = await generate
                .HandleAsync(new GenerateKeyInput(domain, keyType), ct)
                .ConfigureAwait(false);
            if (result.Success)
            {
                bootstrapped.Add(domain);
            }
            else
            {
                KeyCustodianLog.RotationActionFailed(
                    Context.Logger, "bootstrap", domain, result.ErrorCode);
                errors++;
            }
        }

        // Activate — soaked pending keys with no active incumbent.
        foreach (var domain in plan.DueToActivate)
        {
            var pendingRecord = await db.Keys
                .AsNoTracking()
                .ForDomain(domain)
                .Pending()
                .Select(k => new { k.Kid })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (pendingRecord is null)
            {
                KeyCustodianLog.RecordGoneFromPlan(Context.Logger, "activate", domain);
                errors++;
                continue;
            }

            var result = await activate
                .HandleAsync(new ActivateKeyInput(pendingRecord.Kid), ct)
                .ConfigureAwait(false);
            if (result.Success)
            {
                activated.Add(domain);
            }
            else
            {
                KeyCustodianLog.RotationActionFailed(
                    Context.Logger, "activate", domain, result.ErrorCode);
                errors++;
            }
        }

        // Rotate — active incumbent swapped to its soaked pending successor.
        foreach (var domain in plan.DueToRotate)
        {
            var result = await rotate
                .HandleAsync(new RotateKeyInput(domain), ct)
                .ConfigureAwait(false);
            if (result.Success)
            {
                rotated.Add(domain);
            }
            else
            {
                KeyCustodianLog.RotationActionFailed(
                    Context.Logger, "rotate", domain, result.ErrorCode);
                errors++;
            }
        }

        // Generate-successor — active key cadence elapsed; no pending successor yet.
        foreach (var domain in plan.DueToGenerateSuccessor)
        {
            var activeRecord = await db.Keys
                .AsNoTracking()
                .ForDomain(domain)
                .Active()
                .Select(k => new { k.KeyType })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (activeRecord is null)
            {
                KeyCustodianLog.RecordGoneFromPlan(Context.Logger, "generate-successor", domain);
                errors++;
                continue;
            }

            var result = await generate
                .HandleAsync(new GenerateKeyInput(domain, activeRecord.KeyType), ct)
                .ConfigureAwait(false);
            if (result.Success)
            {
                successorsGenerated.Add(domain);
            }
            else
            {
                KeyCustodianLog.RotationActionFailed(
                    Context.Logger, "generate-successor", domain, result.ErrorCode);
                errors++;
            }
        }

        // Retire — retiring keys whose grace window has elapsed.
        foreach (var domain in plan.DueToRetire)
        {
            var retiringRecord = await db.Keys
                .AsNoTracking()
                .ForDomain(domain)
                .Retiring()
                .Select(k => new { k.Kid })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (retiringRecord is null)
            {
                KeyCustodianLog.RecordGoneFromPlan(Context.Logger, "retire", domain);
                errors++;
                continue;
            }

            var result = await retire
                .HandleAsync(new RetireKeyInput(retiringRecord.Kid), ct)
                .ConfigureAwait(false);
            if (result.Success)
            {
                retired.Add(domain);
            }
            else
            {
                KeyCustodianLog.RotationActionFailed(
                    Context.Logger, "retire", domain, result.ErrorCode);
                errors++;
            }
        }

        return D2Result<RunDueRotationsOutput?>.Ok(
            new RunDueRotationsOutput(
                bootstrapped, activated, rotated, successorsGenerated, retired, skipped, errors));
    }
}
