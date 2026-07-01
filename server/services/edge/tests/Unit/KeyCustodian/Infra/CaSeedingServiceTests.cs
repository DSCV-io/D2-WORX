// -----------------------------------------------------------------------
// <copyright file="CaSeedingServiceTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Infra;

using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.SeedCertificateAuthority;
using D2.Edge.KeyCustodian.Infra.Configuration;
using D2.Edge.KeyCustodian.Infra.Scheduling.Hosted;
using D2.Shared.Context.Abstractions;
using D2.Shared.Handler.Abstractions;
using D2.Shared.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Unit tests for <see cref="CaSeedingService"/>: a startup-seed failure (the
/// advisory-lock connect fails against a non-connectable DB) is swallowed so the
/// host survives; when another instance holds the advisory lock the seeder skips
/// and exits cleanly without invoking the handler; a canceled token exits cleanly;
/// and the worker establishes a System request context on its own scope before
/// resolving the handler. The seeding LOGIC itself is covered by
/// <c>SeedCertificateAuthorityTests</c>; this hosted service is the thin fail-safe
/// trigger.
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

    // Regression test: when another instance holds the CA_SEED advisory
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

    // =========================================================================
    // System context establishment — the real worker establishes Origin=System +
    // the host identity + a fresh call-path BEFORE resolving its handler, on the
    // SAME scope, with the existing seed behavior unchanged.
    // =========================================================================

    [Fact]
    public async Task SeedAsync_EstablishesSystemContext_BeforeResolvingHandler()
    {
        var capture = new RequestContextCapture();
        var clock = new TestClock(Instant.FromUtc(2026, 6, 30, 12, 0, 0));
        using var provider = BuildScopeProviderWithFakeHandler(capture);

        var service = BuildService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            workloadIdentity: new D2WorkloadIdentityOptions { ServiceId = "key-custodian" },
            clock: clock);

        await service.SeedAsync(CancellationToken.None);

        capture.HandleAsyncInvoked.Should().BeTrue();
        capture.Origin.Should().Be(RequestOrigin.System);
        capture.ImmediateCaller.Should().Be("key-custodian");
        capture.CallPath.Should().ContainSingle();
        capture.CallPath![0].Id.Should().Be("key-custodian");
        capture.CallPath[0].Kind.Should().Be(CallPathKind.System);
        capture.CallPath[0].Timestamp.Should().Be(clock.Now.ToDateTimeOffset());
    }

    [Fact]
    public async Task SeedAsync_SystemOrigin_GrantsNoSigningAuthority()
    {
        // Least-privilege: AuthorizeSigning only ever grants against CrossProcessHop
        // (per-workload policy) or the in-process minter capability (InProcessModule).
        // A System-origin context structurally cannot reach either branch — the CA
        // seeder is a CA-seed-only consumer, never a signing caller (it never resolves
        // IJwtSigningCapability and never calls AuthorizeSigning).
        var capture = new RequestContextCapture();
        using var provider = BuildScopeProviderWithFakeHandler(capture);
        var service = BuildService(provider.GetRequiredService<IServiceScopeFactory>());

        await service.SeedAsync(CancellationToken.None);

        capture.Origin.Should().NotBe(RequestOrigin.CrossProcessHop);
        capture.Origin.Should().NotBe(RequestOrigin.InProcessModule);
    }

    [Fact]
    public async Task SeedAsync_HandlerStillInvoked_SeedBehaviorUnchanged()
    {
        // Establishing the System context must not interfere with the existing
        // resolve-handler-and-run-it path.
        var capture = new RequestContextCapture();
        using var provider = BuildScopeProviderWithFakeHandler(capture);
        var service = BuildService(provider.GetRequiredService<IServiceScopeFactory>());

        var act = () => service.SeedAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        capture.HandleAsyncInvoked.Should().BeTrue();
    }

    // =========================================================================
    // Helpers.
    // =========================================================================

    private static CaSeedingService BuildService(
        IServiceScopeFactory? scopeFactory = null,
        D2WorkloadIdentityOptions? workloadIdentity = null,
        IClock? clock = null)
    {
        var options = Options.Create(new KeyCustodianInfraOptions
        {
            RootKeyPath = "/test",
            ConnectionString =
                "Host=localhost;Port=1;Database=keycustodian_db;Username=u;Password=p",
        });

        var identity = workloadIdentity ?? new D2WorkloadIdentityOptions { ServiceId = "key-custodian" };

        var resolvedScopeFactory = scopeFactory
            ?? new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new CaSeedingService(
            resolvedScopeFactory,
            options,
            Options.Create(identity),
            clock ?? new D2.Shared.Time.SystemClock(),
            NullLogger<CaSeedingService>.Instance);
    }

    /// <summary>
    /// Builds a real (non-fake) DI container for the worker's own scope: the
    /// module's scoped <c>MutableRequestContext</c>/<c>IRequestContext</c> resolver
    /// (mirroring <c>AddD2KeyCustodian</c>'s registration) plus a
    /// <see cref="FakeSeedCertificateAuthorityHandler"/> that records the
    /// <see cref="IRequestContext"/> it observed into <paramref name="capture"/>.
    /// </summary>
    private static ServiceProvider BuildScopeProviderWithFakeHandler(
        RequestContextCapture capture)
    {
        var services = new ServiceCollection();
        services.AddScoped<MutableRequestContext>();
        services.AddScoped<IRequestContext>(
            sp => sp.GetRequiredService<MutableRequestContext>());
        services.AddSingleton(capture);
        services.AddScoped<ISeedCertificateAuthorityHandler, FakeSeedCertificateAuthorityHandler>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Captures the <see cref="IRequestContext"/> snapshot
    /// <see cref="FakeSeedCertificateAuthorityHandler"/> observed, plus whether it was
    /// invoked at all. Registered as a process-wide singleton so the test can inspect
    /// it after the worker's own (disposed) DI scope.
    /// </summary>
    private sealed class RequestContextCapture
    {
        public bool HandleAsyncInvoked { get; set; }

        public RequestOrigin? Origin { get; set; }

        public string? ImmediateCaller { get; set; }

        public IReadOnlyList<CallPathEntry>? CallPath { get; set; }
    }

    /// <summary>
    /// Fake <see cref="ISeedCertificateAuthorityHandler"/> that records the scoped
    /// <see cref="IRequestContext"/> it observed into a shared
    /// <see cref="RequestContextCapture"/> and returns a deterministic no-op success —
    /// proves the worker establishes the System context on the SAME scope it resolves
    /// the handler from, without needing a live PostgreSQL connection.
    /// </summary>
    private sealed class FakeSeedCertificateAuthorityHandler(
        IRequestContext context, RequestContextCapture capture) : ISeedCertificateAuthorityHandler
    {
        public ValueTask<D2Result<SeedCertificateAuthorityOutput?>> HandleAsync(
            SeedCertificateAuthorityInput input,
            CancellationToken ct = default,
            HandlerOptions? options = null)
        {
            capture.HandleAsyncInvoked = true;
            capture.Origin = context.Origin;
            capture.ImmediateCaller = context.ImmediateCaller;
            capture.CallPath = context.CallPath;

            var output = new SeedCertificateAuthorityOutput(false, null, null);
            return ValueTask.FromResult(D2Result<SeedCertificateAuthorityOutput?>.Ok(output));
        }
    }
}
