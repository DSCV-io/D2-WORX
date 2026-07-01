// -----------------------------------------------------------------------
// <copyright file="KeyRotationService.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Infra.Scheduling.Hosted;

using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;
using D2.Edge.KeyCustodian.Infra.Configuration;
using D2.Edge.KeyCustodian.Infra.Observability;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Context.Abstractions;
using D2.Shared.EntityFrameworkCore.Postgres;
using D2.Shared.Time;
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
/// <see cref="AdvisoryLocks.KeycustodianDb.ROTATION"/> via
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
/// </remarks>
public sealed class KeyRotationService(
    IServiceScopeFactory scopeFactory,
    IOptions<KeyCustodianInfraOptions> options,
    IOptions<D2WorkloadIdentityOptions> workloadIdentity,
    IClock clock,
    ILogger<KeyRotationService> logger)
    : BackgroundService
{
    private readonly KeyCustodianInfraOptions r_options = options.Value;
    private readonly string r_hostServiceId = workloadIdentity.Value.ServiceId;
    private readonly IClock r_clock = clock;

    /// <summary>
    /// Builds the compiled domain → <see cref="KeyType"/> map used to bootstrap
    /// domains that have no live keys yet. Derived from the closed
    /// <see cref="KeyDomain.All"/> catalog: the signing domain gets an RSA key,
    /// the payload-encryption domains get AES keys, and the opaque-secret domains
    /// get symmetric secrets. CA domains are excluded — they are seeded by the
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
            if (IsCaDomain(domain.Value))
                continue;

            map[domain.Value] = KeyTypeForDomain(domain.Value);
        }

        return map;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="domainValue"/> identifies
    /// a CA-certificate key domain (<c>mtls-ca-root</c> or
    /// <c>mtls-ca-intermediate</c>). CA domains are excluded from the auto-bootstrap
    /// map because their keys are seeded by the <c>CaSeedingService</c> on startup,
    /// not by the standard key-generation generator.
    /// </summary>
    /// <param name="domainValue">The domain wire value to test.</param>
    /// <returns><see langword="true"/> if the domain is a CA domain.</returns>
    internal static bool IsCaDomain(string domainValue) =>
        domainValue is KeyDomain.MTLS_CA_ROOT or KeyDomain.MTLS_CA_INTERMEDIATE;

    /// <summary>
    /// Maps a domain wire value to the <see cref="KeyType"/> used to bootstrap
    /// that domain when no live keys exist yet. The signing domain gets an RSA key;
    /// the opaque-secret domains get a symmetric secret; all other (encryption)
    /// domains get an AES payload key.
    /// </summary>
    /// <param name="domainValue">The domain wire value (e.g. <c>"jwks-signing"</c>).</param>
    /// <returns>The <see cref="KeyType"/> for initial key generation in that domain.</returns>
    internal static KeyType KeyTypeForDomain(string domainValue) => domainValue switch
    {
        KeyDomain.JWKS_SIGNING => KeyType.RsaSigning,
        KeyDomain.COOKIE => KeyType.Secret,
        KeyDomain.CLIENT_SECRET => KeyType.Secret,

        // The encryption-domain catalog (audit / notifications / courier / …) are
        // symmetric payload-encryption keyrings.
        _ => KeyType.AesPayload,
    };

    /// <summary>
    /// Resolves a fresh DI scope, establishes the worker's
    /// <see cref="RequestOrigin.System"/> request context on it, then runs
    /// <see cref="IRunDueRotationsHandler"/> and logs the outcome. Internal so a unit
    /// test can drive it directly — the real advisory-lock acquire in
    /// <see cref="RunTickAsync"/> requires a live PostgreSQL connection.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    internal async Task ExecuteRotationAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.EstablishSystemContext(r_hostServiceId, r_clock);
        var handler = scope.ServiceProvider.GetRequiredService<IRunDueRotationsHandler>();

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
                    AdvisoryLocks.KeycustodianDb.ROTATION,
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
