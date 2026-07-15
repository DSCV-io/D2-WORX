// -----------------------------------------------------------------------
// <copyright file="AuthEndpointGuardStartupFilterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Startup;

using AwesomeAssertions;
using D2.Shared.AspNetCore;
using D2.Shared.Auth.Grpc.Endpoints;
using D2.Shared.Auth.Http.Endpoints;
using D2.Shared.Auth.Startup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

/// <summary>
/// Unit tests for <see cref="AuthEndpointGuardStartupFilter"/> — the
/// deny-by-default boot guard that fails host startup when any mapped
/// <see cref="RouteEndpoint"/> lacks a declared auth intent.
/// Uses a synthetic <see cref="EndpointDataSource"/> that returns a
/// hand-built endpoint list; no real ASP.NET host is started.
/// </summary>
public sealed class AuthEndpointGuardStartupFilterTests
{
    // ── Theory data (static — must precede instance members per SA1204) ───

    /// <summary>
    /// Drives <see cref="Configure_InfrastructurePath_SkippedEvenWithNoMetadata"/>
    /// from <see cref="D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS"/> so the
    /// test stays in sync with the constant rather than duplicating literal strings.
    /// Sub-paths (e.g. <c>/health/db</c>) are also covered: the
    /// <c>InfrastructurePathMatcher</c> matches on prefix.
    /// </summary>
    public static TheoryData<string> InfrastructurePaths()
    {
        var data = new TheoryData<string>();
        foreach (var prefix in D2AspNetCoreConstants.DEFAULT_INFRASTRUCTURE_PATHS)
        {
            data.Add(prefix);

            // Sub-path: verify prefix-match behavior (e.g. /health/db under /health).
            data.Add(prefix.TrimEnd('/') + "/sub-check");
        }

        return data;
    }

    // ── IStartupFilter shape ──────────────────────────────────────────────

    [Fact]
    public void AuthEndpointGuardStartupFilter_IsIStartupFilter()
    {
        // Structural contract: the guard MUST be an IStartupFilter so it runs
        // during HTTP-pipeline construction — AFTER WebApplication DataSources
        // are merged into the routing composite — rather than as a hosted
        // service that starts too late in the IHostedService order.
        typeof(AuthEndpointGuardStartupFilter)
            .GetInterfaces()
            .Should().Contain(typeof(IStartupFilter));
    }

    // ── Constructor guards ────────────────────────────────────────────────

    [Fact]
    public void Ctor_NullLogger_Throws()
    {
        var act = () => new AuthEndpointGuardStartupFilter(
            null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ── Pass cases (Configure(next) calls next without throwing) ─────────

    [Fact]
    public void Configure_NoEndpoints_CallsNext()
    {
        var filter = BuildFilter();
        var nextCalled = false;
        var appBuilder = BuildApplicationBuilder([]);

        var pipeline = filter.Configure(_ => nextCalled = true);
        pipeline(appBuilder);

        nextCalled.Should().BeTrue(
            "Configure must call next when no endpoints are present");
    }

    [Fact]
    public void Configure_DeclaredHttp_EndpointScopeMetadata_CallsNext()
    {
        var endpoint = BuildRoute(
            "/api/files/{id}",
            [EndpointScopeMetadata.ForScopes(["files.read"], D2.Shared.Auth.Abstractions.ScopeMatch.Any)]);

        var nextCalled = false;
        var appBuilder = BuildApplicationBuilder([endpoint]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => nextCalled = true);
        pipeline(appBuilder);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void Configure_DeclaredHttp_HarmlessEndpointMetadata_CallsNext()
    {
        var endpoint = BuildRoute(
            "/internal/status",
            [EndpointScopeMetadata.HarmlessEndpoint]);

        var nextCalled = false;
        var appBuilder = BuildApplicationBuilder([endpoint]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => nextCalled = true);
        pipeline(appBuilder);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void Configure_DeclaredGrpc_MethodScopeMetadata_CallsNext()
    {
        var endpoint = BuildRoute(
            "d2.files.FilesService/GetFile",
            [MethodScopeMetadata.ForScopes(["files.read"], D2.Shared.Auth.Abstractions.ScopeMatch.Any)]);

        var nextCalled = false;
        var appBuilder = BuildApplicationBuilder([endpoint]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => nextCalled = true);
        pipeline(appBuilder);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void Configure_DeclaredGrpc_HarmlessMethodScopeMetadata_CallsNext()
    {
        var endpoint = BuildRoute(
            "d2.health.HealthService/Health",
            [MethodScopeMetadata.HarmlessEndpoint]);

        var nextCalled = false;
        var appBuilder = BuildApplicationBuilder([endpoint]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => nextCalled = true);
        pipeline(appBuilder);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void Configure_DeclaredGrpc_D2RequireAnyScopeAttribute_CallsNext()
    {
        var endpoint = BuildRoute(
            "d2.files.FilesService/Upload",
            [new D2RequireAnyScopeAttribute("files.write")]);

        var nextCalled = false;
        var appBuilder = BuildApplicationBuilder([endpoint]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => nextCalled = true);
        pipeline(appBuilder);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void Configure_DeclaredGrpc_D2RequireAllScopesAttribute_CallsNext()
    {
        var endpoint = BuildRoute(
            "d2.files.FilesService/Delete",
            [new D2RequireAllScopesAttribute("files.read", "files.delete")]);

        var nextCalled = false;
        var appBuilder = BuildApplicationBuilder([endpoint]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => nextCalled = true);
        pipeline(appBuilder);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public void Configure_DeclaredGrpc_D2HarmlessEndpointAttribute_CallsNext()
    {
        var endpoint = BuildRoute(
            "d2.health.HealthService/Probe",
            [new D2HarmlessEndpointAttribute()]);

        var nextCalled = false;
        var appBuilder = BuildApplicationBuilder([endpoint]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => nextCalled = true);
        pipeline(appBuilder);

        nextCalled.Should().BeTrue();
    }

    // ── Infrastructure-path skip ──────────────────────────────────────────

    [Theory]
    [MemberData(nameof(InfrastructurePaths))]
    public void Configure_InfrastructurePath_SkippedEvenWithNoMetadata(string path)
    {
        // Infra endpoints have no auth declarations — guard must skip them.
        var endpoint = BuildRoute(path, []);

        var nextCalled = false;
        var appBuilder = BuildApplicationBuilder([endpoint]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => nextCalled = true);
        pipeline(appBuilder);

        nextCalled.Should().BeTrue(
            "infrastructure paths must be skipped — no auth metadata required");
    }

    // ── Non-RouteEndpoint (base Endpoint) ─────────────────────────────────

    [Fact]
    public void Configure_NonRouteEndpoint_SkippedEvenWithNoMetadata()
    {
        // A plain Endpoint (not RouteEndpoint) has no route pattern and
        // can't carry a declared intent by convention. Guard must skip it.
        var nonRoute = new Endpoint(
            requestDelegate: _ => Task.CompletedTask,
            metadata: new EndpointMetadataCollection(),
            displayName: "non-route");

        var nextCalled = false;
        var services = new ServiceCollection();
        services.AddSingleton<EndpointDataSource>(
            new FakeEndpointDataSource([nonRoute]));
        var appBuilder = new FakeApplicationBuilder(services.BuildServiceProvider());

        var filter = new AuthEndpointGuardStartupFilter(
            NullLogger<AuthEndpointGuardStartupFilter>.Instance);

        var pipeline = filter.Configure(_ => nextCalled = true);
        pipeline(appBuilder);

        nextCalled.Should().BeTrue(
            "non-RouteEndpoints must be skipped — no route identity to guard");
    }

    // ── Fail cases (Configure(next) throws InvalidOperationException) ─────

    [Fact]
    public void Configure_UndeclaredEndpoint_Throws()
    {
        var endpoint = BuildRoute("/files/{id}", []);

        var appBuilder = BuildApplicationBuilder([endpoint]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => { });
        var act = () => pipeline(appBuilder);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Configure_UndeclaredEndpoint_ExceptionMessageNamesRoute()
    {
        const string route = "/api/users/{userId}/profile";
        var endpoint = BuildRoute(route, []);

        var appBuilder = BuildApplicationBuilder([endpoint]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => { });
        var act = () => pipeline(appBuilder);

        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain(route);
    }

    [Fact]
    public void Configure_MixedList_OnlyOffenderNamed()
    {
        // One declared (passes) + one undeclared (fails).
        // The message must name the undeclared one only.
        var declared = BuildRoute(
            "/api/health",
            [EndpointScopeMetadata.HarmlessEndpoint]);
        var undeclared = BuildRoute("/api/data/{id}", []);

        var appBuilder = BuildApplicationBuilder([declared, undeclared]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => { });
        var act = () => pipeline(appBuilder);

        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("/api/data/{id}");
        ex.Message.Should().NotContain("/api/health");
    }

    [Fact]
    public void Configure_UndeclaredEndpoint_MessageIsRouteTemplateOnly_NoPiiValues()
    {
        // The message must contain only the route TEMPLATE (structural shape),
        // never route values or query parameters — PII discipline.
        const string route = "/users/{userId}";
        var endpoint = BuildRoute(route, []);

        var appBuilder = BuildApplicationBuilder([endpoint]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => { });
        var act = () => pipeline(appBuilder);

        var ex = act.Should().Throw<InvalidOperationException>().Which;

        // Message contains the template placeholder, not a user-data value.
        ex.Message.Should().Contain(route);

        // Remediation hints present.
        ex.Message.Should().ContainAll("RequireAnyScope", "MarkAsD2HarmlessEndpoint");
    }

    [Fact]
    public void Configure_InfraPathAndUndeclaredEndpoint_OnlyUndeclaredThrows()
    {
        // /health is infra (skipped); /private is not declared.
        var infra = BuildRoute("/health", []);
        var undeclared = BuildRoute("/private", []);

        var appBuilder = BuildApplicationBuilder([infra, undeclared]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => { });
        var act = () => pipeline(appBuilder);

        var ex = act.Should().Throw<InvalidOperationException>().Which;
        ex.Message.Should().Contain("/private");
        ex.Message.Should().NotContain("/health");
    }

    [Fact]
    public void Configure_GrpcCatchAllEndpoint_SkippedEvenWithNoAuthMetadata()
    {
        // Pins IsGrpcCatchAllEndpoint at the unit level: a RouteEndpoint whose
        // route-pattern parameters include the grpcunimplemented constraint must
        // be skipped by the guard without throwing, even with no auth metadata.
        //
        // RoutePatternFactory.Parse understands inline constraints
        // ({param:constraint}), so parsing
        // "{unimplementedService}/{unimplementedMethod:grpcunimplemented}"
        // produces a RoutePattern with a parameter whose ParameterPolicies
        // collection contains a policy with Content == "grpcunimplemented" —
        // exactly the discriminator IsGrpcCatchAllEndpoint checks.
        var catchAllPattern = "{unimplementedService}/{unimplementedMethod:grpcunimplemented}";
        var endpoint = BuildRoute(catchAllPattern, []);

        var nextCalled = false;
        var appBuilder = BuildApplicationBuilder([endpoint]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => nextCalled = true);
        pipeline(appBuilder);

        nextCalled.Should().BeTrue(
            "gRPC infrastructure catch-all endpoints (identified by the "
            + "grpcunimplemented route constraint) must be skipped — "
            + "they carry no auth metadata and are not real callable methods");
    }

    [Fact]
    public void Configure_MultipleUndeclaredEndpoints_AllNamed()
    {
        // Both endpoints lack auth declarations — the exception message must
        // name both so the operator can fix all offenders in one pass.
        const string route1 = "/api/orders/{orderId}";
        const string route2 = "/api/invoices/{invoiceId}";

        var undeclared1 = BuildRoute(route1, []);
        var undeclared2 = BuildRoute(route2, []);

        var appBuilder = BuildApplicationBuilder([undeclared1, undeclared2]);
        var filter = BuildFilter();

        var pipeline = filter.Configure(_ => { });
        var act = () => pipeline(appBuilder);

        var ex = act.Should().Throw<InvalidOperationException>().Which;

        ex.Message.Should().Contain(
            route1,
            "the first undeclared route must appear in the exception message");
        ex.Message.Should().Contain(
            route2,
            "the second undeclared route must appear in the exception message");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static AuthEndpointGuardStartupFilter BuildFilter()
        => new(NullLogger<AuthEndpointGuardStartupFilter>.Instance);

    private static IApplicationBuilder BuildApplicationBuilder(
        IEnumerable<Endpoint> endpoints)
    {
        var services = new ServiceCollection();
        services.AddSingleton<EndpointDataSource>(
            new FakeEndpointDataSource(endpoints.ToList()));
        return new FakeApplicationBuilder(services.BuildServiceProvider());
    }

    private static RouteEndpoint BuildRoute(
        string rawPattern,
        IReadOnlyList<object> metadata)
    {
        var pattern = RoutePatternFactory.Parse(rawPattern);
        return new RouteEndpoint(
            requestDelegate: _ => Task.CompletedTask,
            routePattern: pattern,
            order: 0,
            metadata: new EndpointMetadataCollection(metadata),
            displayName: rawPattern);
    }

    /// <summary>
    /// Minimal <see cref="EndpointDataSource"/> that returns a
    /// caller-supplied list.
    /// </summary>
    private sealed class FakeEndpointDataSource : EndpointDataSource
    {
        private readonly IReadOnlyList<Endpoint> r_endpoints;

        public FakeEndpointDataSource(IReadOnlyList<Endpoint> endpoints)
            => r_endpoints = endpoints;

        public override IReadOnlyList<Endpoint> Endpoints => r_endpoints;

        public override IChangeToken GetChangeToken()
            => new CancellationChangeToken(CancellationToken.None);
    }

    /// <summary>
    /// Minimal <see cref="IApplicationBuilder"/> whose
    /// <see cref="IApplicationBuilder.ApplicationServices"/> is backed by a
    /// caller-supplied <see cref="IServiceProvider"/>. Used to drive
    /// <see cref="AuthEndpointGuardStartupFilter.Configure"/> in unit tests
    /// without starting a real host.
    /// </summary>
    private sealed class FakeApplicationBuilder : IApplicationBuilder
    {
        public FakeApplicationBuilder(IServiceProvider serviceProvider)
            => ApplicationServices = serviceProvider;

        public IServiceProvider ApplicationServices { get; set; }

        public IFeatureCollection ServerFeatures
            => new FeatureCollection();

        public IDictionary<string, object?> Properties { get; }
            = new Dictionary<string, object?>();

        public IApplicationBuilder Use(
            Func<RequestDelegate, RequestDelegate> middleware)
            => this;

        public IApplicationBuilder New()
            => new FakeApplicationBuilder(ApplicationServices);

        public RequestDelegate Build()
            => _ => Task.CompletedTask;
    }
}
