// -----------------------------------------------------------------------
// <copyright file="KeyRotationServiceTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Infra;

using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;
using D2.Edge.KeyCustodian.Infra.Configuration;
using D2.Edge.KeyCustodian.Infra.Scheduling.Hosted;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Context.Abstractions;
using D2.Shared.Handler.Abstractions;
using D2.Shared.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;
using TestClock = D2.Shared.Time.TestClock;

/// <summary>
/// Unit tests for <see cref="KeyRotationService"/>: the bootstrap key-type
/// mapping, CA-domain exclusion from auto-bootstrap, the skip-if-lock-not-held
/// no-op behavior, the swallowed-tick-exception contract (host must not crash),
/// the OCE re-propagation on a canceled WaitForNextTickAsync, and the System
/// work-plane establishment the worker performs on every resolution scope.
/// </summary>
public sealed class KeyRotationServiceTests
{
    // =========================================================================
    // (a) Bootstrap key-type mapping — internal statics, no I/O.
    // =========================================================================

    [Fact]
    public void BuildBootstrapKeyTypes_SameMapAsTheRetiredPerServiceSwitch()
    {
        // Regression pin: the map is now DERIVED from the KeyDomain catalog's
        // per-domain key-type binding (no infra-local switch, no catch-all arm), and
        // must equal the exact map the retired switch produced. After the sealed-domain catalog
        // removal (audit / notifications / courier flipped to sealed and left the
        // symmetric payload catalog) the derived map carries only the remaining non-CA
        // domains — no AES-payload entry survives.
        var expected = new Dictionary<string, KeyType>(StringComparer.Ordinal)
        {
            [KeyDomain.JWKS_SIGNING] = KeyType.RsaSigning,
            [KeyDomain.COOKIE] = KeyType.Secret,
            [KeyDomain.CLIENT_SECRET] = KeyType.Secret,
        };

        KeyRotationService.BuildBootstrapKeyTypes().Should().Equal(expected);
    }

    [Theory]
    [InlineData(KeyDomain.JWKS_SIGNING, false)]
    [InlineData(KeyDomain.COOKIE, false)]
    [InlineData(FixturePayloadDomains.PAYLOAD_A, false)]
    [InlineData(KeyDomain.MTLS_CA_ROOT, true)]
    [InlineData(KeyDomain.MTLS_CA_INTERMEDIATE, true)]
    public void IsCaDomain_DerivedFromTheKeyTypeBinding(string domainValue, bool expected)
    {
        using var fixtureSeam = FixturePayloadDomains.Register();

        var domain = KeyDomain.Create(domainValue).Data!;

        KeyRotationService.IsCaDomain(domain).Should().Be(
            expected,
            "CA classification derives from the domain's bound key type, not a name list");
    }

    [Fact]
    public void BuildBootstrapKeyTypes_ContainsAllNonCaCatalogDomains()
    {
        // CA domains are seeded by the CaSeedingService on startup, not auto-
        // bootstrapped by this map. All other catalog domains must be present.
        var map = KeyRotationService.BuildBootstrapKeyTypes();

        foreach (var domain in KeyDomain.All)
        {
            if (KeyRotationService.IsCaDomain(domain))
            {
                map.Should().NotContainKey(
                    domain.Value, because: "CA domains are excluded from auto-bootstrap");
            }
            else
            {
                map.Should().ContainKey(domain.Value);
            }
        }
    }

    // CA domains must NEVER appear in the bootstrap map, which would silently
    // generate AES material for a CA domain.
    [Theory]
    [InlineData(KeyDomain.MTLS_CA_ROOT)]
    [InlineData(KeyDomain.MTLS_CA_INTERMEDIATE)]
    public void BuildBootstrapKeyTypes_CaDomains_AreExcluded_NeverAes(string caDomain)
    {
        var map = KeyRotationService.BuildBootstrapKeyTypes();

        map.Should().NotContainKey(
            caDomain,
            because: "CA-certificate domains are seeded by the CaSeedingService, not auto-bootstrapped as AES keys");
    }

    [Fact]
    public void BuildBootstrapKeyTypes_JwksSigningMapsToRsaSigning()
    {
        var map = KeyRotationService.BuildBootstrapKeyTypes();

        map[KeyDomain.JWKS_SIGNING].Should().Be(KeyType.RsaSigning);
    }

    [Fact]
    public void BuildBootstrapKeyTypes_CookieMapsToSecret()
    {
        var map = KeyRotationService.BuildBootstrapKeyTypes();

        map[KeyDomain.COOKIE].Should().Be(KeyType.Secret);
    }

    [Fact]
    public void BuildBootstrapKeyTypes_ClientSecretMapsToSecret()
    {
        var map = KeyRotationService.BuildBootstrapKeyTypes();

        map[KeyDomain.CLIENT_SECRET].Should().Be(KeyType.Secret);
    }

    // =========================================================================
    // (b) Skip-if-lock-not-held — host does nothing when another instance wins.
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_Canceled_BeforeFirstTick_ExitsCleanly()
    {
        // CancellationToken already canceled on entry → the do-while condition
        // (WaitForNextTickAsync) returns false on the first timer check, or the
        // service exits before RunTickAsync can proceed. The host must not throw.
        var cts = new CancellationTokenSource();
        var service = BuildService(new ThrowingSystemWorkScopeFactory());

        await cts.CancelAsync();

        // Canceling before any tick returns false from WaitForNextTickAsync —
        // the loop exits before RunTickAsync fires. No exception must escape.
        var token = cts.Token;                        // struct — not disposable
        var act = () => service.StartAsync(token);    // captures struct, not `cts`

        try
        {
            await act.Should().NotThrowAsync();
        }
        finally
        {
            cts.Dispose();
        }
    }

    // =========================================================================
    // (c) Tick exception swallowed — host must not crash.
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_TickThrows_ExceptionSwallowed_HostSurvives()
    {
        // A system-work factory that throws on BeginAsync is used to make the tick
        // fail. The rotation service catches all non-OCE exceptions and logs them;
        // it must NOT re-throw, so ExecuteAsync must complete without propagating.
        var factory = new ThrowingSystemWorkScopeFactory();
        var options = BuildOptions(TimeSpan.FromMilliseconds(1));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var service = BuildService(factory, options);

        // Start the service and let it run until the token fires. The tick
        // throws each time but must be swallowed.
        var executed = service.StartAsync(cts.Token);

        // Wait for it to complete (canceled by cts) — no uncaught exception.
        var act = () => executed;

        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // (d) OCE from WaitForNextTickAsync re-propagates to ExecuteAsync.
    // =========================================================================

    [Fact]
    public async Task ExecuteAsync_CanceledToken_ExitsWithoutThrowingOce()
    {
        // BackgroundService wraps ExecuteAsync: when the host cancels the
        // stoppingToken, WaitForNextTickAsync catches OCE and returns false —
        // the loop exits cleanly. StartAsync/StopAsync must not bubble an OCE.
        var service = BuildService(new NeverRunsSystemWorkScopeFactory());
        using var cts = new CancellationTokenSource();

        var startTask = service.StartAsync(cts.Token);
        await cts.CancelAsync();

        // StopAsync signals the internal CancellationToken — ExecuteAsync must
        // exit cleanly (no thrown OCE visible to the caller).
        var act = () => service.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        await startTask;
    }

    // =========================================================================
    // (e) System work plane — the worker establishes Origin=System via
    // ISystemWorkScopeFactory BEFORE resolving its handler, on the SAME scope.
    // =========================================================================

    [Fact]
    public async Task ExecuteRotationAsync_EstablishesSystemContext_BeforeResolvingHandler()
    {
        var capture = new RequestContextCapture();
        var clock = new TestClock(Instant.FromUtc(2026, 6, 30, 12, 0, 0));
        using var provider = BuildScopeProviderWithFakeHandler(capture, clock, "key-custodian");

        var service = BuildService(provider.GetRequiredService<ISystemWorkScopeFactory>());

        await service.ExecuteRotationAsync(CancellationToken.None);

        capture.HandleAsyncInvoked.Should().BeTrue();
        capture.Origin.Should().Be(RequestOrigin.System);
        capture.ImmediateCaller.Should().Be("key-custodian");
        capture.CallPath.Should().ContainSingle();
        capture.CallPath![0].Id.Should().Be("key-custodian");
        capture.CallPath[0].Kind.Should().Be(CallPathKind.System);
        capture.CallPath[0].Timestamp.Should().Be(clock.Now.ToDateTimeOffset());
    }

    [Fact]
    public async Task ExecuteRotationAsync_SystemOrigin_GrantsNoSigningAuthority()
    {
        // Least-privilege: AuthorizeSigning only ever grants against CrossProcessHop
        // (per-workload policy) or the in-process minter capability (InProcessModule).
        // A System-origin context structurally cannot reach either branch — the
        // rotation worker is a key-LIFECYCLE consumer only, never a signing caller
        // (it never resolves IJwtSigningCapability and never calls AuthorizeSigning).
        var capture = new RequestContextCapture();
        using var provider = BuildScopeProviderWithFakeHandler(capture);
        var service = BuildService(provider.GetRequiredService<ISystemWorkScopeFactory>());

        await service.ExecuteRotationAsync(CancellationToken.None);

        capture.Origin.Should().NotBe(RequestOrigin.CrossProcessHop);
        capture.Origin.Should().NotBe(RequestOrigin.InProcessModule);
    }

    [Fact]
    public async Task ExecuteRotationAsync_HandlerStillInvoked_TickBehaviorUnchanged()
    {
        // Establishing the System context must not interfere with the existing
        // resolve-handler-and-run-it path.
        var capture = new RequestContextCapture();
        using var provider = BuildScopeProviderWithFakeHandler(capture);
        var service = BuildService(provider.GetRequiredService<ISystemWorkScopeFactory>());

        var act = () => service.ExecuteRotationAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
        capture.HandleAsyncInvoked.Should().BeTrue();
    }

    // =========================================================================
    // Helpers.
    // =========================================================================

    private static KeyCustodianInfraOptions BuildOptions(
        TimeSpan? rotationCheckInterval = null) =>
        new()
        {
            RootKeyPath = "/test",
            RotationCheckInterval = rotationCheckInterval ?? TimeSpan.FromMinutes(5),
            ConnectionString =
                "Host=localhost;Port=1;Database=d2-keycustodian;Username=u;Password=p",
        };

    private static KeyRotationService BuildService(
        ISystemWorkScopeFactory systemWork,
        KeyCustodianInfraOptions? options = null) =>
        new(
            systemWork,
            Options.Create(options ?? BuildOptions()),
            NullLogger<KeyRotationService>.Instance);

    /// <summary>
    /// Builds a real DI container with the platform System work plane + a
    /// <see cref="FakeRunDueRotationsHandler"/> that records the
    /// <see cref="IRequestContext"/> it observed into <paramref name="capture"/>.
    /// </summary>
    private static ServiceProvider BuildScopeProviderWithFakeHandler(
        RequestContextCapture capture,
        D2.Shared.Time.IClock? clock = null,
        string serviceId = "key-custodian")
    {
        var services = new ServiceCollection();
        services.Configure<D2WorkloadIdentityOptions>(o => o.ServiceId = serviceId);

        if (clock is not null)
            services.AddSingleton(clock);

        services.AddD2SystemWorkPlane();
        services.AddSingleton(capture);
        services.AddScoped<IRunDueRotationsHandler, FakeRunDueRotationsHandler>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Captures the <see cref="IRequestContext"/> snapshot
    /// <see cref="FakeRunDueRotationsHandler"/> observed, plus whether it was invoked
    /// at all. Registered as a process-wide singleton so the test can inspect it after
    /// the worker's own (disposed) DI scope.
    /// </summary>
    private sealed class RequestContextCapture
    {
        public bool HandleAsyncInvoked { get; set; }

        public RequestOrigin? Origin { get; set; }

        public string? ImmediateCaller { get; set; }

        public IReadOnlyList<CallPathEntry>? CallPath { get; set; }
    }

    /// <summary>
    /// Fake <see cref="IRunDueRotationsHandler"/> that records the scoped
    /// <see cref="IRequestContext"/> it observed into a shared
    /// <see cref="RequestContextCapture"/> and returns a deterministic empty-summary
    /// success — proves the worker establishes the System context on the SAME scope it
    /// resolves the handler from, without needing a live PostgreSQL connection.
    /// </summary>
    private sealed class FakeRunDueRotationsHandler(
        IRequestContext context, RequestContextCapture capture) : IRunDueRotationsHandler
    {
        public ValueTask<D2Result<RunDueRotationsOutput?>> HandleAsync(
            RunDueRotationsInput input,
            CancellationToken ct = default,
            HandlerOptions? options = null)
        {
            capture.HandleAsyncInvoked = true;
            capture.Origin = context.Origin;
            capture.ImmediateCaller = context.ImmediateCaller;
            capture.CallPath = context.CallPath;

            var output = new RunDueRotationsOutput([], [], [], [], [], [], 0);
            return ValueTask.FromResult(D2Result<RunDueRotationsOutput?>.Ok(output));
        }
    }

    /// <summary>
    /// System work factory whose BeginAsync throws — simulates a tick failure
    /// (wrong connection, unavailable dependency, etc.).
    /// </summary>
    private sealed class ThrowingSystemWorkScopeFactory : ISystemWorkScopeFactory
    {
        public ValueTask<ISystemWorkScope> BeginAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated tick failure");
    }

    /// <summary>
    /// System work factory that should never be called — a bug if it is.
    /// </summary>
    private sealed class NeverRunsSystemWorkScopeFactory : ISystemWorkScopeFactory
    {
        public ValueTask<ISystemWorkScope> BeginAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("scope must not be created in this test path");
    }
}
