// -----------------------------------------------------------------------
// <copyright file="AuthAppBuilderExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Http;

using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Auth;
using DcsvIo.D2.Auth.Errors;
using DcsvIo.D2.Auth.Http;
using DcsvIo.D2.Auth.Http.Endpoints;
using DcsvIo.D2.Auth.Validation;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Local.Default;
using DcsvIo.D2.Tests.Unit.Auth.Inbound.Http.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "TestHost lifetime is bounded by individual tests, not the class.")]
public sealed class AuthAppBuilderExtensionsTests
{
    [Fact]
    public void UseD2Auth_NullApp_Throws()
    {
        IApplicationBuilder? app = null;

        var act = () => app!.UseD2Auth();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task UseD2Auth_RequestWithoutBearer_ResultsIn401ProblemDetails()
    {
        // End-to-end pipeline-position smoke: routing matched, middleware
        // sat between routing and dispatch, short-circuited the protected
        // endpoint with a ProblemDetails response.
        using var builder = new TestJwtBuilder();
        using var host = await BuildHostAsync(builder);
        var client = host.GetTestClient();

        var response = await client.GetAsync(new Uri("/protected", UriKind.Relative));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("d2_error_code").GetString()
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public async Task UseD2Auth_HarmlessEndpoint_PassesWithoutBearer()
    {
        using var builder = new TestJwtBuilder();
        using var host = await BuildHostAsync(builder);
        var client = host.GetTestClient();

        var response = await client.GetAsync(new Uri("/harmless", UriKind.Relative));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task UseD2Auth_AuthenticatedRequest_PassesAndPopulatesContext()
    {
        using var builder = new TestJwtBuilder();
        using var host = await BuildHostAsync(builder);
        var client = host.GetTestClient();
        var token = builder.MintToken("https://edge.internal", "files");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync(new Uri("/protected", UriKind.Relative));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Be("authenticated");
    }

    private static async Task<IHost> BuildHostAsync(TestJwtBuilder jwtBuilder)
    {
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddLogging();
                        services.AddRouting();
                        services.AddD2LocalCache();
                        services.AddSingleton<ITieredCache, FakeTieredCacheStubB>();
                        services.AddD2Auth(opts =>
                        {
                            opts.Issuer = new Uri("https://edge.internal");
                            opts.Audience = "files";
                        });

                        // Override the JwksProvider with the fake so we don't
                        // fetch real JWKS from a non-existent issuer in tests.
                        services.RemoveAll<DcsvIo.D2.Auth.Abstractions.Jwks.IJwksProvider>();
                        services.RemoveAll<DcsvIo.D2.Auth.Jwks.HttpJwksProvider>();
                        services.AddSingleton<DcsvIo.D2.Auth.Abstractions.Jwks.IJwksProvider>(
                            new FakeJwksProvider(jwtBuilder.PublicKey));
                        services.RemoveAll<JwtValidator>();
                        services.AddSingleton(sp => new JwtValidator(
                            sp.GetRequiredService<
                                DcsvIo.D2.Auth.Abstractions.Jwks.IJwksProvider>(),
                            sp.GetRequiredService<IOptions<AuthOptions>>(),
                            sp.GetRequiredService<ClaimsToContextMapper>(),
                            Microsoft.Extensions.Logging.Abstractions
                                .NullLogger<JwtValidator>.Instance));
                        services.AddD2AuthHttp();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseD2Auth();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet(
                                "/protected", () => "authenticated");
                            endpoints.MapGet("/harmless", () => "harmless-ok")
                                .MarkAsD2HarmlessEndpoint();
                        });
                    });
            });

        var host = await hostBuilder.StartAsync();
        return host;
    }

    /// <summary>
    /// In-memory tiered-cache stub for the integration host. Same shape as the
    /// stub in <c>AuthHttpServiceCollectionExtensionsTests</c>; lives here as
    /// a separate type to keep test fixtures next to their tests.
    /// </summary>
    private sealed class FakeTieredCacheStubB : ITieredCache
    {
        public ValueTask<DcsvIo.D2.Result.D2Result<bool>> ExistsAsync(
            string key, CancellationToken ct = default)
            => new(DcsvIo.D2.Result.D2Result<bool>.Ok(true));

        public ValueTask<DcsvIo.D2.Result.D2Result<T?>> GetAsync<T>(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<DcsvIo.D2.Result.D2Result<IReadOnlyDictionary<string, T?>>>
            GetManyAsync<T>(
                IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<DcsvIo.D2.Result.D2Result> SetAsync<T>(
            string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<DcsvIo.D2.Result.D2Result> SetManyAsync<T>(
            IReadOnlyDictionary<string, T> entries,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<DcsvIo.D2.Result.D2Result> RemoveAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<DcsvIo.D2.Result.D2Result> RemoveManyAsync(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<DcsvIo.D2.Result.D2Result<TimeSpan?>> GetTtlAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<DcsvIo.D2.Result.D2Result<long>> IncrementAsync(
            string key,
            long delta = 1,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<DcsvIo.D2.Result.D2Result<bool>> SetNxAsync<T>(
            string key,
            T value,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<DcsvIo.D2.Result.D2Result<bool>> AcquireLockAsync(
            string key, string token, TimeSpan ttl, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<DcsvIo.D2.Result.D2Result> ReleaseLockAsync(
            string key, string token, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<DcsvIo.D2.Result.D2Result> SetAndBroadcastAsync<T>(
            string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<DcsvIo.D2.Result.D2Result> SetManyAndBroadcastAsync<T>(
            IReadOnlyDictionary<string, T> entries,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<DcsvIo.D2.Result.D2Result> RemoveAndBroadcastAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<DcsvIo.D2.Result.D2Result> RemoveManyAndBroadcastAsync(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
