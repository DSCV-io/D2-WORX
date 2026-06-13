// -----------------------------------------------------------------------
// <copyright file="KeyCustodianHealthCheck.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Infra.Observability;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Readiness health check for the KeyCustodian module.
/// </summary>
/// <remarks>
/// <para>
/// Reports:
/// <list type="bullet">
///   <item><description>
///     <see cref="HealthStatus.Unhealthy"/> when the database is unreachable OR
///     the root keyring cannot be loaded — KeyCustodian cannot serve keys at all.
///   </description></item>
///   <item><description>
///     <see cref="HealthStatus.Degraded"/> when every configured domain is
///     reachable but at least one has no <c>Active</c> key (e.g. during the
///     first-boot soak window before bootstrap activates). The readiness endpoint
///     still returns 200; the degraded state is the operator signal to watch the
///     rotation backlog, not a "down" indicator.
///   </description></item>
///   <item><description>
///     <see cref="HealthStatus.Healthy"/> when every configured domain has an
///     <c>Active</c> key.
///   </description></item>
/// </list>
/// </para>
/// <para>
/// The "configured domains" set is the closed <see cref="KeyDomain.All"/> catalog
/// — the same set the rotation service bootstraps. Raw database connectivity is
/// ALSO covered by the framework <c>AddDbContextCheck</c> (both tagged
/// <c>ready</c>); this check adds the KeyCustodian-semantic readiness signal
/// (root key loadable + active-key-per-domain).
/// </para>
/// </remarks>
public sealed class KeyCustodianHealthCheck(
    IServiceScopeFactory scopeFactory,
    IRootKeyProvider rootKeyProvider)
    : IHealthCheck
{
    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // 1. Root key must load (fail-fast surface — Unhealthy if the keyring
        //    can't be built). Never surface key bytes in the description.
        try
        {
            _ = rootKeyProvider.GetRootKeyring();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                "KeyCustodian root keyring could not be loaded.");
        }

        // 2. Query active-key domains. A DB fault here is Unhealthy (can't serve).
        List<string> activeDomains;
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IKeyCustodianDbContext>();

            activeDomains = await db.Keys
                .AsNoTracking()
                .Active()
                .Select(k => k.KeyDomain)
                .Distinct()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                "KeyCustodian database is unreachable.");
        }

        // 3. Degrade when any configured domain lacks an Active key.
        var activeSet = activeDomains.ToHashSet(StringComparer.Ordinal);
        var missing = KeyDomain.All
            .Select(d => d.Value)
            .Where(d => !activeSet.Contains(d))
            .ToList();

        if (missing.Count > 0)
        {
            return HealthCheckResult.Degraded(
                $"KeyCustodian has no Active key for {missing.Count} configured "
                + $"domain(s): {string.Join(", ", missing)}.");
        }

        return HealthCheckResult.Healthy(
            "KeyCustodian has an Active key for every configured domain.");
    }
}
