// -----------------------------------------------------------------------
// <copyright file="AuthEndpointGuardBootFailTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ServiceDefaults;

using AwesomeAssertions;
using D2.Shared.Auth.Http.Endpoints;
using D2.Shared.Auth.Startup;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.Hosting;
using global::Microsoft.AspNetCore.Http;
using global::Microsoft.AspNetCore.Routing;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.DependencyInjection;
using global::Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// Integration test for the deny-by-default auth endpoint guard boot-fail
/// path: verifies that a host with an undeclared endpoint fails to start,
/// and that a host with all endpoints declared starts cleanly.
/// </summary>
/// <remarks>
/// Builds a minimal ASP.NET Core host using
/// <c>Microsoft.AspNetCore.TestHost</c> directly. The guard is wired via
/// <see cref="AuthEndpointGuardServiceCollectionExtensions.AddD2AuthEndpointGuard"/>;
/// no full auth stack is needed (the guard only walks
/// <c>EndpointDataSource</c>, not JWT validation).
/// </remarks>
public sealed class AuthEndpointGuardBootFailTests
{
    [Fact]
    public async Task Host_WithUndeclaredEndpoint_ThrowsOnStart()
    {
        // An endpoint with no auth declaration must fail boot.
        var act = async () =>
        {
            using var host = await BuildHostAsync(
                endpoints =>
                {
                    // Register an endpoint with NO auth declaration.
                    // The guard must surface this as a boot failure.
                    endpoints.MapGet(
                        "/undeclared",
                        () => Results.Text("should not start"));
                });
        };

        await act.Should().ThrowAsync<InvalidOperationException>(
            "the guard must abort host start when an undeclared endpoint is mapped");
    }

    [Fact]
    public async Task Host_WithUndeclaredEndpoint_ExceptionMessageNamesRoute()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var host = await BuildHostAsync(
                endpoints =>
                {
                    endpoints.MapGet(
                        "/secret/endpoint",
                        () => Results.Text("boot should fail"));
                });
        });

        ex.Message.Should().Contain(
            "/secret/endpoint",
            "the guard exception must name the undeclared route for operator triage");
    }

    [Fact]
    public async Task Host_WithAllEndpointsDeclared_StartsCleanly()
    {
        // All endpoints carry a declared auth intent → guard passes → host starts.
        using var host = await BuildHostAsync(
            endpoints =>
            {
                // Declared as harmless — guard accepts this.
                endpoints.MapGet(
                    "/declared",
                    () => Results.Text("ok"))
                    .WithMetadata(EndpointScopeMetadata.HarmlessEndpoint);
            });

        host.Should().NotBeNull(
            "host must start cleanly when all endpoints are declared");
    }

    [Fact]
    public async Task Host_WithGuardNotRegistered_UndeclaredEndpoint_StartsCleanly()
    {
        // If the guard is not registered, undeclared endpoints must not block boot.
        // Mirrors the SkipAuthEndpointGuard=true path at the host-registration level.
        using var host = await BuildHostAsync(
            endpoints =>
            {
                endpoints.MapGet(
                    "/undeclared-allowed",
                    () => Results.Text("ok"));
            },
            registerGuard: false);

        host.Should().NotBeNull(
            "unregistered guard must not block host start on undeclared endpoints");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Task<IHost> BuildHostAsync(
        Action<IEndpointRouteBuilder>? extraEndpoints = null,
        bool registerGuard = true)
    {
        // The guard is registered as an IStartupFilter inside ConfigureWebHost.
        // IStartupFilter.Configure(next) runs during HTTP-pipeline construction
        // in GenericWebHostService.StartAsync — after UseEndpoints has wired
        // all endpoints into the routing composite. Registration order relative
        // to ConfigureWebHost vs the outer ConfigureServices does NOT matter
        // for IStartupFilter (unlike the former IHostedService approach which
        // required post-pipeline registration to see a populated endpoint set).
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddLogging();

                        if (registerGuard)
                            services.AddD2AuthEndpointGuard();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            extraEndpoints?.Invoke(endpoints);
                        });
                    });
            });

        return hostBuilder.StartAsync();
    }
}
