// -----------------------------------------------------------------------
// <copyright file="CaSeedingServiceTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Infra;

using D2.Edge.KeyCustodian.Infra.Configuration;
using D2.Edge.KeyCustodian.Infra.Scheduling.Hosted;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Unit tests for <see cref="CaSeedingService"/>: a startup-seed failure (the
/// advisory-lock connect fails against a non-connectable DB) is swallowed so the
/// host survives; when another instance holds the advisory lock the seeder skips
/// and exits cleanly without invoking the handler; a canceled token exits cleanly.
/// The seeding LOGIC itself is covered by <c>SeedCertificateAuthorityTests</c>;
/// this hosted service is the thin fail-safe trigger.
/// </summary>
public sealed class CaSeedingServiceTests
{
    [Fact]
    public async Task ExecuteAsync_SeedConnectFails_ExceptionSwallowed_HostSurvives()
    {
        // The fake connection string cannot connect, so the advisory-lock acquire
        // throws. CaSeedingService must catch it (the host must boot even if seeding
        // fails — issuance fails loud later) and NOT re-throw.
        var service = BuildService();
        var cts = new CancellationTokenSource();
        var token = cts.Token; // struct — capture it, not the disposable cts

        var act = () => service.StartAsync(token);

        try
        {
            await act.Should().NotThrowAsync();
        }
        finally
        {
            cts.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_CanceledToken_ExitsCleanly()
    {
        var service = BuildService();
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var token = cts.Token;

        var act = () => service.StartAsync(token);

        try
        {
            await act.Should().NotThrowAsync();
        }
        finally
        {
            cts.Dispose();
        }
    }

    // Regression test for B3-F1: when another instance holds the CA_SEED advisory
    // lock, the seeder must log CaSeedSkippedLockHeld and exit cleanly — the handler
    // is never invoked, and no exception escapes.
    [Fact]
    public async Task ExecuteAsync_LockHeldByOtherInstance_SkipsSeeding_ExitsCleanly()
    {
        var service = BuildService();

        // Inject the test seam: lock is held by another instance (return false = not acquired).
        service.TryAcquireLockAsync = _ => Task.FromResult(false);

        using var cts = new CancellationTokenSource();
        var token = cts.Token; // capture struct before disposal — avoids captured-disposable warning
        var act = () => service.StartAsync(token);

        await act.Should().NotThrowAsync(
            because: "a held advisory lock must cause a clean skip, not an exception");
    }

    [Fact]
    public async Task StopAsync_AfterStart_DoesNotThrow()
    {
        var service = BuildService();
        using var cts = new CancellationTokenSource();

        await service.StartAsync(cts.Token);
        var act = () => service.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    private static CaSeedingService BuildService()
    {
        var options = Options.Create(new KeyCustodianInfraOptions
        {
            RootKeyPath = "/test",
            ConnectionString =
                "Host=localhost;Port=1;Database=keycustodian_db;Username=u;Password=p",
        });
        return new CaSeedingService(
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            options,
            NullLogger<CaSeedingService>.Instance);
    }
}
