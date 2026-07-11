// -----------------------------------------------------------------------
// <copyright file="AuthServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound;

using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions.Jwks;
using D2.Shared.Auth.Abstractions.Sessions;
using D2.Shared.Auth.Jwks;
using D2.Shared.Auth.Sessions;
using D2.Shared.Auth.Validation;
using D2.Shared.Caching;
using D2.Shared.Caching.Local.Default;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;

/// <summary>
/// Smoke tests for the <c>AddD2Auth</c> composition root — verifies the full
/// DI surface end-to-end (jwks provider, session liveness tracker, OIDC
/// <see cref="IConfigurationManager{T}"/>, named OIDC discovery HttpClient,
/// both hosted services, options validation, idempotency, null-arg throws)
/// and pins the public name constant that hosts grep / reference by string.
/// </summary>
public sealed class AuthServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2Auth_RegistersIJwksProvider()
    {
        var sp = BuildProvider();

        sp.GetService<IJwksProvider>().Should().NotBeNull();
    }

    [Fact]
    public void AddD2Auth_RegistersISessionLivenessTracker()
    {
        var sp = BuildProvider();

        sp.GetService<ISessionLivenessTracker>().Should().NotBeNull();
    }

    [Fact]
    public void AddD2Auth_AliasesIJwksProviderToHttpJwksProviderSingleton()
    {
        var sp = BuildProvider();

        var iface = sp.GetRequiredService<IJwksProvider>();
        var concrete = sp.GetRequiredService<HttpJwksProvider>();

        iface.Should().BeSameAs(concrete);
    }

    [Fact]
    public void AddD2Auth_AliasesISessionLivenessTrackerToTieredCacheTrackerSingleton()
    {
        var sp = BuildProvider();

        var iface = sp.GetRequiredService<ISessionLivenessTracker>();
        var concrete = sp.GetRequiredService<TieredCacheSessionLivenessTracker>();

        iface.Should().BeSameAs(concrete);
    }

    [Fact]
    public void AddD2Auth_RegistersOpenIdConfigurationManager()
    {
        var sp = BuildProvider();

        var configManager = sp.GetService<IConfigurationManager<OpenIdConnectConfiguration>>();

        configManager.Should().NotBeNull();
    }

    [Fact]
    public void AddD2Auth_RegistersJwksBackplaneSubscriberAsHostedService()
    {
        var sp = BuildProvider();
        var hosted = sp.GetServices<IHostedService>().ToList();

        hosted.Should().Contain(h => h is JwksBackplaneSubscriber);
    }

    [Fact]
    public void AddD2Auth_RegistersSessionRevokedBackplaneSubscriberAsHostedService()
    {
        var sp = BuildProvider();
        var hosted = sp.GetServices<IHostedService>().ToList();

        hosted.Should().Contain(h => h is SessionRevokedBackplaneSubscriber);
    }

    [Fact]
    public void AddD2Auth_RegistersNamedOidcDiscoveryHttpClient()
    {
        var sp = BuildProvider();
        var factory = sp.GetRequiredService<IHttpClientFactory>();

        var client = factory.CreateClient(
            AuthServiceCollectionExtensions.OIDC_DISCOVERY_HTTP_CLIENT_NAME);

        client.Should().NotBeNull();
    }

    [Fact]
    public void AddD2Auth_NamedOidcDiscoveryHttpClient_AppliesConfiguredTimeout()
    {
        // Without the configured timeout the BCL default 100s applies — a hung
        // upstream Edge JWKS endpoint would tie up the calling thread for the
        // whole window. Default timeout sourced from JwksProviderOptions = 5s.
        var sp = BuildProvider();
        var factory = sp.GetRequiredService<IHttpClientFactory>();

        var client = factory.CreateClient(
            AuthServiceCollectionExtensions.OIDC_DISCOVERY_HTTP_CLIENT_NAME);

        client.Timeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddD2Auth_OverriddenHttpRequestTimeout_FlowsToNamedClient()
    {
        var services = BaseServices();
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri("https://edge.internal");
            opts.Audience = "files";
            opts.Jwks = opts.Jwks with { HttpRequestTimeout = TimeSpan.FromSeconds(2) };
        });
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IHttpClientFactory>();

        var client = factory.CreateClient(
            AuthServiceCollectionExtensions.OIDC_DISCOVERY_HTTP_CLIENT_NAME);

        client.Timeout.Should().Be(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void AddD2Auth_MissingTrustedRootPath_FailsValidationOnFirstResolve()
    {
        // When operators set a trusted-root path it must exist at host build —
        // fail-loud rather than first-request TLS blow-up.
        var services = BaseServices();
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri("https://edge.internal");
            opts.Audience = "files";
            opts.Jwks = opts.Jwks with
            {
                TrustedRootCertificatePath = Path.Combine(
                    Path.GetTempPath(),
                    "d2-missing-oidc-root-" + Guid.NewGuid().ToString("N") + ".crt"),
            };
        });
        var sp = services.BuildServiceProvider();

        var act = () => _ = sp.GetRequiredService<IOptions<AuthOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddD2Auth_EmptyTrustedRootPath_PassesValidation()
    {
        // Public-CA deployments leave TrustedRootCertificatePath empty —
        // system store only.
        var services = BaseServices();
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri("https://edge.internal");
            opts.Audience = "files";
            opts.Jwks = opts.Jwks with { TrustedRootCertificatePath = null };
        });
        var sp = services.BuildServiceProvider();

        var opts = sp.GetRequiredService<IOptions<AuthOptions>>().Value;

        opts.Jwks.TrustedRootCertificatePath.Should().BeNull();
        sp.GetRequiredService<IJwksProvider>().Should().BeOfType<HttpJwksProvider>();
    }

    [Fact]
    public void AddD2Auth_ValidTrustedRootPath_PassesValidationAndRegistersHttpJwksProvider()
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(),
            "d2-oidc-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var rootPath = Path.Combine(tempDir, "ca-root.crt");

        try
        {
            using var key = System.Security.Cryptography.ECDsa.Create(
                System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
            var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
                "CN=D2 Auth Test Root",
                key,
                System.Security.Cryptography.HashAlgorithmName.SHA256);
            using var cert = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddYears(1));
            File.WriteAllBytes(
                rootPath,
                cert.Export(
                    System.Security.Cryptography.X509Certificates.X509ContentType.Cert));

            var services = BaseServices();
            services.AddD2Auth(opts =>
            {
                opts.Issuer = new Uri("https://edge.internal");
                opts.Audience = "files";
                opts.Jwks = opts.Jwks with { TrustedRootCertificatePath = rootPath };
            });
            var sp = services.BuildServiceProvider();

            var auth = sp.GetRequiredService<IOptions<AuthOptions>>().Value;
            auth.Jwks.TrustedRootCertificatePath.Should().Be(rootPath);

            // Non-issuer hosts keep HttpJwksProvider as the default IJwksProvider.
            sp.GetRequiredService<IJwksProvider>().Should().BeOfType<HttpJwksProvider>();
            sp.GetRequiredService<HttpJwksProvider>().Should().NotBeNull();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void AddD2Auth_EmptyBackplaneChannelKey_FailsValidationOnFirstResolve()
    {
        // Empty / whitespace silently never matches a backplane invalidation
        // — every key-rotated event would be dropped. Reject at host build.
        var services = BaseServices();
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri("https://edge.internal");
            opts.Audience = "files";
            opts.Jwks = opts.Jwks with { BackplaneChannelKey = "   " };
        });
        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<AuthOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddD2Auth_RegistersTimeProvider()
    {
        var sp = BuildProvider();

        sp.GetService<TimeProvider>().Should().NotBeNull();
    }

    [Fact]
    public void AddD2Auth_RegistersJwtValidator()
    {
        var sp = BuildProvider();

        sp.GetService<JwtValidator>().Should().NotBeNull();
    }

    [Fact]
    public void AddD2Auth_RegistersClaimsToContextMapper()
    {
        var sp = BuildProvider();

        sp.GetService<ClaimsToContextMapper>().Should().NotBeNull();
    }

    [Fact]
    public void AddD2Auth_JwtValidatorIsSingleton()
    {
        var sp = BuildProvider();

        var first = sp.GetRequiredService<JwtValidator>();
        var second = sp.GetRequiredService<JwtValidator>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddD2Auth_ClaimsToContextMapperIsSingleton()
    {
        var sp = BuildProvider();

        var first = sp.GetRequiredService<ClaimsToContextMapper>();
        var second = sp.GetRequiredService<ClaimsToContextMapper>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddD2Auth_EmptyValidAlgorithms_FailsValidationOnFirstResolve()
    {
        // ValidAlgorithms = empty would silently accept ANY algorithm — the
        // alg=none + HMAC-with-public-key defenses live in the allowlist.
        // Reject at host build.
        var services = BaseServices();
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri("https://edge.internal");
            opts.Audience = "files";
            opts.Validator = opts.Validator with { ValidAlgorithms = Array.Empty<string>() };
        });
        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<AuthOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddD2Auth_WhitespaceAlgorithm_FailsValidationOnFirstResolve()
    {
        var services = BaseServices();
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri("https://edge.internal");
            opts.Audience = "files";
            opts.Validator = opts.Validator with
            {
                ValidAlgorithms = new[] { "RS256", "   " },
            };
        });
        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<AuthOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void OidcDiscoveryHttpClientName_IsStable()
    {
        // Pin the public name — hosts wire `services.AddHttpClient(name).AddXxx()`
        // by string; renames here would silently detach a host's resilience
        // pipeline / tracing handler from our OIDC discovery client.
        AuthServiceCollectionExtensions.OIDC_DISCOVERY_HTTP_CLIENT_NAME
            .Should().Be("d2-auth-oidc-discovery");
    }

    [Fact]
    public void AddD2Auth_NullServices_Throws()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2Auth(_ => { });

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2Auth_NullConfigure_Throws()
    {
        var services = new ServiceCollection();

        var act = () => services.AddD2Auth(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2Auth_MissingIssuer_FailsValidationOnFirstResolve()
    {
        // ValidateOnStart() fires only inside IHost.StartAsync. For a unit
        // test, resolving IOptions<T>.Value directly triggers the same
        // Validate(...) chain — that's the host's mechanism too. Either way
        // the failure surface is OptionsValidationException; ValidateOnStart
        // just guarantees it fires at host build rather than first request.
        var services = BaseServices();
        services.AddD2Auth(opts =>
        {
            opts.Audience = "files";

            // Issuer intentionally omitted (default null)
        });
        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<AuthOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddD2Auth_MissingAudience_FailsValidationOnFirstResolve()
    {
        var services = BaseServices();
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri("https://edge.internal");

            // Audience intentionally omitted (default null)
            opts.Audience = null;
        });
        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<AuthOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddD2Auth_HttpIssuer_FailsValidationOnFirstResolve()
    {
        var services = BaseServices();
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri("http://edge.internal");
            opts.Audience = "files";
        });
        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<AuthOptions>>().Value;

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddD2Auth_HttpsIssuer_PassesValidationOnFirstResolve()
    {
        var services = BaseServices();
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri("https://edge.internal");
            opts.Audience = "files";
        });
        var sp = services.BuildServiceProvider();

        var act = () => sp.GetRequiredService<IOptions<AuthOptions>>().Value;

        act.Should().NotThrow();
    }

    [Fact]
    public void AddD2Auth_CalledTwice_IsIdempotent()
    {
        // TryAdd* in AddD2Auth means a second invocation should not duplicate
        // singletons. Composition roots (e.g. when libraries call AddD2Auth
        // defensively) must not double-register.
        var services = BaseServices();
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri("https://edge.internal");
            opts.Audience = "files";
        });
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri("https://edge.internal");
            opts.Audience = "files";
        });

        var sp = services.BuildServiceProvider();

        // Singleton resolutions of the same type should yield the same instance.
        var jwks1 = sp.GetRequiredService<HttpJwksProvider>();
        var jwks2 = sp.GetRequiredService<HttpJwksProvider>();
        jwks1.Should().BeSameAs(jwks2);

        var tracker1 = sp.GetRequiredService<TieredCacheSessionLivenessTracker>();
        var tracker2 = sp.GetRequiredService<TieredCacheSessionLivenessTracker>();
        tracker1.Should().BeSameAs(tracker2);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = BaseServices();
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri("https://edge.internal");
            opts.Audience = "files";
        });
        return services.BuildServiceProvider();
    }

    private static ServiceCollection BaseServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2LocalCache();
        services.AddSingleton<ITieredCache, FakeTieredCache>();
        return services;
    }

    /// <summary>
    /// Minimal in-memory <see cref="ITieredCache"/> stand-in for composition
    /// resolution — only ExistsAsync is called by the registered tracker, but
    /// every interface member is implemented to satisfy the type contract.
    /// </summary>
    private sealed class FakeTieredCache : ITieredCache
    {
        public ValueTask<D2.Shared.Result.D2Result<bool>> ExistsAsync(
            string key, CancellationToken ct = default)
            => new(D2.Shared.Result.D2Result<bool>.Ok());

        public ValueTask<D2.Shared.Result.D2Result<T?>> GetAsync<T>(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2.Shared.Result.D2Result<IReadOnlyDictionary<string, T?>>>
            GetManyAsync<T>(
                IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2.Shared.Result.D2Result> SetAsync<T>(
            string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2.Shared.Result.D2Result> SetManyAsync<T>(
            IReadOnlyDictionary<string, T> entries,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2.Shared.Result.D2Result> RemoveAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2.Shared.Result.D2Result> RemoveManyAsync(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2.Shared.Result.D2Result<TimeSpan?>> GetTtlAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2.Shared.Result.D2Result<long>> IncrementAsync(
            string key,
            long delta = 1,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2.Shared.Result.D2Result<bool>> SetNxAsync<T>(
            string key,
            T value,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2.Shared.Result.D2Result<bool>> AcquireLockAsync(
            string key, string token, TimeSpan ttl, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2.Shared.Result.D2Result> ReleaseLockAsync(
            string key, string token, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2.Shared.Result.D2Result> SetAndBroadcastAsync<T>(
            string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2.Shared.Result.D2Result> SetManyAndBroadcastAsync<T>(
            IReadOnlyDictionary<string, T> entries,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2.Shared.Result.D2Result> RemoveAndBroadcastAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2.Shared.Result.D2Result> RemoveManyAndBroadcastAsync(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
