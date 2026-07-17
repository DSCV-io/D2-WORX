// -----------------------------------------------------------------------
// <copyright file="CaSeedingService.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Scheduling.Hosted;

using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.SeedCertificateAuthority;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Configuration;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// In-process startup task that seeds the certificate-authority hierarchy ONCE on
/// boot. Registered AFTER the migrator (the schema must exist) and BEFORE the
/// rotation service (the CA must exist before the first rotation tick classifies
/// the CA domains).
/// </summary>
/// <remarks>
/// <para>
/// <b>Leaderless coordination.</b> The run acquires
/// <see cref="AdvisoryLocks.D2Keycustodian.CA_SEED"/> via
/// <see cref="PgAdvisoryLock.TryAcquireSessionAsync"/> (skip-if-held), a DIFFERENT
/// key from the migrator's and the rotation timer's, so across a multi-instance
/// deployment exactly one instance seeds; the others skip and rely on the
/// idempotent active-CA gate.
/// </para>
/// <para>
/// <b>Idempotent.</b> The seeding LOGIC lives in the testable
/// <see cref="ISeedCertificateAuthorityHandler"/>; this hosted service is the thin
/// trigger. A re-run on an already-seeded store is a no-op (the command's
/// active-CA gate). CA domains are seeded here, NOT auto-bootstrapped by the
/// rotation service (the bootstrap map excludes them).
/// </para>
/// <para>
/// <b>Fail-safe.</b> A seed failure is logged and swallowed so the host still
/// starts; certificate issuance then fails loud (503) until a CA is seeded or
/// rotated in. A seed failure must never crash the host on boot.
/// </para>
/// <para>
/// <b>System work plane.</b> Opens work via platform
/// <see cref="ISystemWorkScopeFactory"/> — never a hand-rolled
/// <c>CreateAsyncScope</c> + <c>EstablishSystemContext</c> pair. The factory
/// pairs with dual-path <c>IRequestContext</c> (Items or scoped Mutable) so
/// System workers never need a throw-only resolver.
/// </para>
/// </remarks>
public sealed class CaSeedingService(
    ISystemWorkScopeFactory systemWork,
    IOptions<KeyCustodianInfraOptions> options,
    ILogger<CaSeedingService> logger)
    : BackgroundService
{
    private readonly KeyCustodianInfraOptions r_options = options.Value;

    /// <summary>
    /// Gets or sets the advisory-lock acquisition seam used by unit tests. When set,
    /// <see cref="ExecuteAsync"/> bypasses the real
    /// <see cref="PgAdvisoryLock.TryAcquireSessionAsync"/>. Return <see langword="true"/>
    /// to simulate this instance acquiring the lock; <see langword="false"/> to simulate
    /// another instance holding it. The delegate must NOT perform I/O.
    /// </summary>
    internal Func<CancellationToken, Task<bool>>? TryAcquireLockAsync { get; set; }

    /// <summary>
    /// Opens a System work scope via <see cref="ISystemWorkScopeFactory"/>, then runs
    /// <see cref="ISeedCertificateAuthorityHandler"/> and logs the outcome. Internal so
    /// a unit test can drive it directly — the real advisory-lock acquire in
    /// <see cref="ExecuteAsync"/> requires a live PostgreSQL connection.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    internal async Task SeedAsync(CancellationToken ct)
    {
        await using var work = await systemWork.BeginAsync(ct).ConfigureAwait(false);
        var handler = work.Services.GetRequiredService<ISeedCertificateAuthorityHandler>();

        var result = await handler
            .HandleAsync(new SeedCertificateAuthorityInput(), ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            // A CA-load failure (missing/invalid chain) returns a typed failure result.
            // Log a clear degrade warning and continue boot — workload-certificate
            // issuance returns 503 until the CA files are installed and the seeder
            // re-runs on the next startup.
            KeyCustodianInfraLog.CaSeedRunFailed(logger, result.ErrorCode);
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (TryAcquireLockAsync is not null)
            {
                // Test path: no real Postgres connection; the seam decides IsHeld.
                var held = await TryAcquireLockAsync(stoppingToken).ConfigureAwait(false);

                if (!held)
                {
                    KeyCustodianInfraLog.CaSeedSkippedLockHeld(logger);
                    return;
                }

                await SeedAsync(stoppingToken).ConfigureAwait(false);
                return;
            }

            await using var seedLock = await PgAdvisoryLock
                .TryAcquireSessionAsync(
                    r_options.ConnectionString,
                    AdvisoryLocks.D2Keycustodian.CA_SEED,
                    stoppingToken)
                .ConfigureAwait(false);

            if (!seedLock.IsHeld)
            {
                // Another instance owns the seed; skip — the idempotent gate covers us.
                KeyCustodianInfraLog.CaSeedSkippedLockHeld(logger);
                return;
            }

            await SeedAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down — exit cleanly.
        }
        catch (Exception ex)
        {
            // CA seeding must never crash the host on boot. Surface PII-safely;
            // issuance fails loud later if no CA was seeded.
            KeyCustodianInfraLog.CaSeedFailed(
                logger,
                SanitizedExceptionRender.TypeName(ex),
                SanitizedExceptionRender.FirstFrame(ex));
        }
    }
}
