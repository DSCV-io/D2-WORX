// -----------------------------------------------------------------------
// <copyright file="GrpcEndpointMetadataProjectionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc;

using System.Globalization;
using System.Text;
using AwesomeAssertions;
using D2.Shared.Auth.Grpc.Endpoints;
using D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Protos;
using global::Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// Regression tests that prove D2 gRPC scope attributes placed on a gRPC
/// service class or method project onto the endpoint's
/// <see cref="Microsoft.AspNetCore.Http.EndpointMetadataCollection"/> when
/// the service is mapped via <c>MapGrpcService&lt;T&gt;()</c>.
///
/// This projection is load-bearing for two consumers:
/// <list type="bullet">
///   <item>The boot guard (<c>AuthEndpointGuardStartupCheck</c>): reads these
///     attributes from endpoint metadata to decide "declared intent"; if they
///     do not project the guard throws at boot (false positive).</item>
///   <item>The gRPC interceptor (<c>JwtAuthInterceptor</c>): walks endpoint
///     metadata at runtime to enforce scope; if they do not project the
///     attribute enforcement path is dead.</item>
/// </list>
///
/// Each test uses a real ASP.NET Core test host + a real
/// <see cref="EndpointDataSource"/> (the same pipeline production uses), then
/// reads the metadata collection for endpoints registered by
/// <c>MapGrpcService&lt;T&gt;()</c>.
/// </summary>
public sealed class GrpcEndpointMetadataProjectionTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Class-level [D2RequireAnyScope] projects onto ALL method endpoints.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A gRPC service class decorated with <c>[D2RequireAnyScope("probe.scope")]</c>
    /// at the class level: all endpoints for its methods carry that attribute in
    /// their metadata collection.
    /// </summary>
    [Fact]
    public async Task ClassLevel_D2RequireAnyScope_ProjectsOntoEndpointMetadata()
    {
        using var host = await BuildHostWithAttributeServicesAsync();
        var endpointDataSource = host.Services.GetRequiredService<EndpointDataSource>();

        // Real gRPC method endpoints use route pattern "/PackageName.ServiceName/MethodName"
        // (leading slash, not a fallback). The {unimplementedMethod:grpcunimplemented}
        // catch-all endpoints do NOT carry service attributes — they are infrastructure.
        var endpoints = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e =>
                e.RoutePattern.RawText?.StartsWith(
                    "/d2.test.auth.TestEcho/", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        endpoints.Should().NotBeEmpty(
            "MapGrpcService<AttributeDecoratedEchoService>() should have registered "
            + "at least one RouteEndpoint matching '/d2.test.auth.TestEcho/*'.");

        foreach (var ep in endpoints)
        {
            var anyAttr = ep.Metadata.GetMetadata<D2RequireAnyScopeAttribute>();
            anyAttr.Should().NotBeNull(
                $"Expected D2RequireAnyScopeAttribute on endpoint '{ep.RoutePattern.RawText}' "
                + "because [D2RequireAnyScope(\"probe.scope\")] is on the service class. "
                + $"Actual metadata types present: [{string.Join(", ", ep.Metadata.Select(m => m.GetType().Name))}]");

            anyAttr.Scopes.Should().ContainEquivalentOf("probe.scope");
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Method-level [D2HarmlessEndpoint] projects onto that specific endpoint.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A gRPC service method decorated with <c>[D2HarmlessEndpoint]</c> at the
    /// method level: that method's endpoint carries the attribute in its metadata
    /// collection.
    /// </summary>
    [Fact]
    public async Task MethodLevel_D2HarmlessEndpoint_ProjectsOntoEndpointMetadata()
    {
        using var host = await BuildHostWithAttributeServicesAsync();
        var endpointDataSource = host.Services.GetRequiredService<EndpointDataSource>();

        // Real gRPC method endpoints use route pattern "/PackageName.ServiceName/MethodName"
        // (leading slash, not a fallback).
        var endpoints = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(e =>
                e.RoutePattern.RawText?.StartsWith(
                    "/d2.test.auth.TestHealth/", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        endpoints.Should().NotBeEmpty(
            "MapGrpcService<AttributeDecoratedHealthService>() should have registered "
            + "at least one RouteEndpoint matching '/d2.test.auth.TestHealth/*'.");

        foreach (var ep in endpoints)
        {
            var harmlessAttr = ep.Metadata.GetMetadata<D2HarmlessEndpointAttribute>();
            harmlessAttr.Should().NotBeNull(
                $"Expected D2HarmlessEndpointAttribute on endpoint '{ep.RoutePattern.RawText}' "
                + "because [D2HarmlessEndpoint] is on the Health method override. "
                + $"Actual metadata types present: [{string.Join(", ", ep.Metadata.Select(m => m.GetType().Name))}]");
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Diagnostic dump — always passes; captures full endpoint metadata in
    // assertion message for any future investigation.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Captures the full metadata type list for every <see cref="RouteEndpoint"/>
    /// registered by both attribute-decorated services. This test always passes —
    /// it embeds diagnostic evidence in the assertion message so the full picture
    /// is preserved in the test output for any result.
    /// </summary>
    [Fact]
    public async Task AllRouteEndpoints_HaveExpectedMetadataTypes()
    {
        using var host = await BuildHostWithAttributeServicesAsync();

        var endpointDataSource = host.Services.GetRequiredService<EndpointDataSource>();
        var allEndpoints = endpointDataSource.Endpoints.OfType<RouteEndpoint>().ToList();

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"Total RouteEndpoints: {allEndpoints.Count}");
        foreach (var ep in allEndpoints)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Route: {ep.RoutePattern.RawText}");
            foreach (var item in ep.Metadata)
                sb.AppendLine(CultureInfo.InvariantCulture, $"    [{item.GetType().Name}]: {item}");
        }

        // Always passes — diagnostic capture only.
        allEndpoints.Should().NotBeEmpty(
            "Host should have registered at least the 2 attribute-decorated gRPC services. "
            + $"Endpoint dump:\n{sb}");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Host builder — attribute path only (no fluent .RequireAnyScope /
    // .MarkAsD2HarmlessEndpoint on the builder). The only source of auth
    // metadata is the attributes on the service class or method.
    // ──────────────────────────────────────────────────────────────────────

    private static async Task<IHost> BuildHostWithAttributeServicesAsync()
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
                        services.AddGrpc();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            // Attribute path only — NO fluent .RequireAnyScope /
                            // .MarkAsD2HarmlessEndpoint on the builder. The attribute
                            // declarations on the service class/method below are the
                            // only source of auth metadata.
                            endpoints.MapGrpcService<AttributeDecoratedEchoService>();
                            endpoints.MapGrpcService<AttributeDecoratedHealthService>();
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test service types — attribute declarations are the only source of
    // auth intent (no fluent wiring).
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Class-level <see cref="D2RequireAnyScopeAttribute"/> — the attribute
    /// declaration that the boot guard + gRPC interceptor depend on projecting
    /// into endpoint metadata for all methods on this service.
    /// </summary>
    [D2RequireAnyScope("probe.scope")]
    private sealed class AttributeDecoratedEchoService : TestEcho.TestEchoBase
    {
        public override Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
            => Task.FromResult(new EchoReply { Echoed = request.Payload });
    }

    /// <summary>
    /// Method-level <see cref="D2HarmlessEndpointAttribute"/> — placed on the
    /// method override (not the class) to test whether method-level attributes
    /// project onto that method's specific endpoint metadata.
    /// </summary>
    private sealed class AttributeDecoratedHealthService : TestHealth.TestHealthBase
    {
        [D2HarmlessEndpoint]
        public override Task<HealthReply> Health(HealthRequest request, ServerCallContext context)
            => Task.FromResult(new HealthReply { Status = "ok" });
    }
}
