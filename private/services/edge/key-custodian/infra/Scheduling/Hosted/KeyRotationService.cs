// -----------------------------------------------------------------------
// <copyright file="KeyRotationService.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Scheduling.Hosted;

using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Configuration;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// In-process scheduler that drives <c>RunDueRotations</c> on a fixed interval.
/// Each tick acquires a PostgreSQL try-advisory-lock so that, across a
/// multi-instance deployment, exactly one instance services a given rotation
/// window; the others skip until the next tick.
/// </summary>
/// <remarks>
/// <para>
/// <b>Leaderless coordination.</b> The tick attempts
/// <see cref="AdvisoryLocks.D2Keycustodian.ROTATION"/> via
/// <see cref="PgAdvisoryLock.TryAcquireSessionAsync"/> (skip-if-held). The lock is
/// a DIFFERENT key from the migrator's, so migration and rotation never block each
/// other. The lock auto-releases on connection drop (and on dispose) so a crashed
/// instance never wedges rotations.
/// </para>
/// <para>
/// <b>Bootstrap rides the first tick.</b> No dedicated startup generator — the
/// first tick's <c>RunDueRotations</c> sees domains with no live keys and
/// bootstraps them from the compiled key-type catalog
/// (<see cref="BuildBootstrapKeyTypes"/>).
/// </para>
/// <para>
/// The trigger is in-process by design (a future external scheduler receiver would
/// land as a sibling <c>Scheduling/&lt;Provider&gt;/</c> adapter); rotation is rare
/// and non-latency-sensitive, so a timer plus the advisory lock is sufficient.
/// </para>
/// <para>
/// <b>System work plane.</b> Each tick opens work via platform
/// <see cref="ISystemWorkScopeFactory"/> — never a hand-rolled
/// <c>CreateAsyncScope</c> + <c>EstablishSystemContext</c> pair.
/// </para>
/// </remarks>
public sealed class KeyRotationService(
    ISystemWorkScopeFactory systemWork,
    IOptions<KeyCustodianInfraOptions> options,
    ILogger<KeyRotationService> logger)
    : BackgroundService
{
    private readonly KeyCustodianInfraOptions r_options = options.Value;

    /// <summary>
    /// Builds the domain → <see cref="KeyType"/> map used to bootstrap domains that
    /// have no live keys yet. Derived ENTIRELY from the closed
    /// <see cref="KeyDomain.All"/> catalog's per-domain key-type binding
    /// (<see cref="KeyDomain.KeyType"/>) — there is no second domain→type map to
    /// drift, and no catch-all arm that could silently bootstrap a new domain with
    /// the wrong algorithm. CA domains are excluded — they are seeded by the
    /// <c>CaSeedingService</c> on startup, not auto-bootstrapped here. A domain
    /// absent from this map is skipped by <c>RunDueRotations</c> without error.
    /// </summary>
    /// <returns>The domain-keyed bootstrap key-type map (ordinal comparer).</returns>
    internal static IReadOnlyDictionary<string, KeyType> BuildBootstrapKeyTypes()
    {
        var map = new Dictionary<string, KeyType>(StringComparer.Ordinal);

        foreach (var domain in KeyDomain.All)
        {
            // CA-certificate domains are seeded by the CaSeedingService on startup,
            // not by the standard auto-bootstrap generator. Excluding them here
            // ensures they are never silently bootstrapped as AES keys.
            if (IsCaDomain(domain))
                continue;

            map[domain.Value] = domain.KeyType;
        }

        return map;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="domain"/> is a
    /// CA-certificate key domain — derived from the domain's bound
    /// <see cref="KeyDomain.KeyType"/> (never a name list, so a future CA-class
    /// domain is excluded automatically). CA domains are excluded from the
    /// auto-bootstrap map because their keys are seeded by the
    /// <c>CaSeedingService</c> on startup, not by the standard key-generation
    /// generator.
    /// </summary>
    /// <param name="domain">The catalog domain to test.</param>
    /// <returns><see langword="true"/> if the domain is a CA domain.</returns>
    internal static bool IsCaDomain(KeyDomain domain) =>
        domain.KeyType == KeyType.X509CaCertificate;

    /// <summary>
    /// Opens a System work scope via <see cref="ISystemWorkScopeFactory"/>, then runs
    /// <see cref="IRunDueRotationsHandler"/> and logs the outcome. Internal so a unit
    /// test can drive it directly — the real advisory-lock acquire in
    /// <see cref="RunTickAsync"/> requires a live PostgreSQL connection.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    internal async Task ExecuteRotationAsync(CancellationToken ct)
    {
        await using var work = await systemWork.BeginAsync(ct).ConfigureAwait(false);
        var handler = work.Services.GetRequiredService<IRunDueRotationsHandler>();

        var input = new RunDueRotationsInput(BuildBootstrapKeyTypes());
        var result = await handler.HandleAsync(input, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            KeyCustodianInfraLog.RotationRunFailed(logger, result.ErrorCode);
            return;
        }

        var output = result.Data!;

        KeyCustodianInfraLog.RotationRunCompleted(
            logger,
            output.Bootstrapped.Count,
            output.Activated.Count,
            output.Rotated.Count,
            output.SuccessorsGenerated.Count,
            output.Retired.Count,
            output.Skipped.Count,
            output.Errors);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = r_options.RotationCheckInterval;
        KeyCustodianInfraLog.RotationServiceStarted(logger, interval);

        // Bootstrap rides the FIRST tick — run immediately, then on the interval.
        using var timer = new PeriodicTimer(interval);

        do
        {
            await RunTickAsync(stoppingToken).ConfigureAwait(false);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken).ConfigureAwait(false));
    }

    private static async Task<bool> WaitForNextTickAsync(
        PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task RunTickAsync(CancellationToken ct)
    {
        try
        {
            await using var rotationLock = await PgAdvisoryLock
                .TryAcquireSessionAsync(
                    r_options.ConnectionString,
                    AdvisoryLocks.D2Keycustodian.ROTATION,
                    ct)
                .ConfigureAwait(false);

            if (!rotationLock.IsHeld)
            {
                // Another instance owns this window; skip silently until next tick.
                KeyCustodianInfraLog.RotationTickSkippedLockHeld(logger);
                return;
            }

            await ExecuteRotationAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down — let the loop exit cleanly.
            throw;
        }
        catch (Exception ex)
        {
            // A rotation tick must never crash the host. Surface, then wait for the
            // next tick (no retry storm — the cadence bounds the retry rate).
            KeyCustodianInfraLog.RotationTickFailed(
                logger,
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex));
        }
    }
}
