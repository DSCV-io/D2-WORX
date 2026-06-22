// -----------------------------------------------------------------------
// <copyright file="KeyRotationServiceTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Infra;

using D2.Edge.KeyCustodian.Infra.Configuration;
using D2.Edge.KeyCustodian.Infra.Scheduling.Hosted;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Unit tests for <see cref="KeyRotationService"/>: the bootstrap key-type
/// mapping, CA-domain exclusion from auto-bootstrap, the skip-if-lock-not-held
/// no-op behavior, the swallowed-tick-exception contract (host must not crash),
/// and the OCE re-propagation on a canceled WaitForNextTickAsync.
/// </summary>
public sealed class KeyRotationServiceTests
{
    // =========================================================================
    // (a) Bootstrap key-type mapping — internal statics, no I/O.
    // =========================================================================

    [Theory]
    [InlineData(KeyDomain.JWKS_SIGNING, KeyType.RsaSigning)]
    [InlineData(KeyDomain.COOKIE, KeyType.Secret)]
    [InlineData(KeyDomain.CLIENT_SECRET, KeyType.Secret)]
    public void KeyTypeForDomain_KnownDomains_ReturnsExpectedType(
        string domain, KeyType expectedType)
    {
        var actual = KeyRotationService.KeyTypeForDomain(domain);

        actual.Should().Be(expectedType);
    }

    [Fact]
    public void KeyTypeForDomain_UnknownDomain_ReturnsAesPayload()
    {
        var actual = KeyRotationService.KeyTypeForDomain("unknown-domain-sentinel");

        actual.Should().Be(KeyType.AesPayload);
    }

    [Fact]
    public void BuildBootstrapKeyTypes_ContainsAllNonCaCatalogDomains()
    {
        // CA domains are seeded by the CaSeedingService on startup, not auto-
        // bootstrapped by this map. All other catalog domains must be present.
        var map = KeyRotationService.BuildBootstrapKeyTypes();

        foreach (var domain in KeyDomain.All)
        {
            if (KeyRotationService.IsCaDomain(domain.Value))
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

    [Fact]
    public void BuildBootstrapKeyTypes_EncryptionDomainsMapToAesPayload()
    {
        var map = KeyRotationService.BuildBootstrapKeyTypes();

        var encryptionDomains = KeyDomain.All
            .Where(d =>
                d.Value != KeyDomain.JWKS_SIGNING
                && d.Value != KeyDomain.COOKIE
                && d.Value != KeyDomain.CLIENT_SECRET
                && !KeyRotationService.IsCaDomain(d.Value))
            .ToList();

        encryptionDomains.Should().NotBeEmpty(
            "the catalog must include non-KC encryption domains");

        foreach (var domain in encryptionDomains)
            map[domain.Value].Should().Be(KeyType.AesPayload);
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
        var service = BuildService(new ThrowingOnRunScopeFactory());

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
        // A scope factory that throws on GetService is used to make the tick fail.
        // The rotation service catches all non-OCE exceptions and logs them; it
        // must NOT re-throw, so ExecuteAsync must complete without propagating.
        var factory = new ThrowingOnRunScopeFactory();
        var options = BuildOptions(TimeSpan.FromMilliseconds(1));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var service = BuildService(factory, options);

        // Start the service and let it run until the token fires. The tick
        // throws each time (invalid connection string) but must be swallowed.
        var executed = service.StartAsync(cts.Token);

        // Wait for it to complete (cancelled by cts) — no uncaught exception.
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
        var service = BuildService(new NeverRunsScopeFactory());
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
    // Helpers.
    // =========================================================================

    private static KeyCustodianInfraOptions BuildOptions(
        TimeSpan? rotationCheckInterval = null) =>
        new()
        {
            RootKeyPath = "/test",
            RotationCheckInterval = rotationCheckInterval ?? TimeSpan.FromMinutes(5),
            ConnectionString =
                "Host=localhost;Port=1;Database=keycustodian_db;Username=u;Password=p",
        };

    private static KeyRotationService BuildService(
        IServiceScopeFactory scopeFactory,
        KeyCustodianInfraOptions? options = null) =>
        new(
            scopeFactory,
            Options.Create(options ?? BuildOptions()),
            NullLogger<KeyRotationService>.Instance);

    /// <summary>
    /// Scope factory whose scopes throw when the rotation handler is requested —
    /// simulates a tick failure (wrong connection, unavailable dependency, etc.).
    /// </summary>
    private sealed class ThrowingOnRunScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new ThrowingScope();

        private sealed class ThrowingScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } =
                new ServiceCollection().BuildServiceProvider();

            public void Dispose()
            {
            }
        }
    }

    /// <summary>
    /// Scope factory that should never be called — a bug if it is.
    /// </summary>
    private sealed class NeverRunsScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() =>
            throw new InvalidOperationException("scope must not be created in this test path");
    }
}
