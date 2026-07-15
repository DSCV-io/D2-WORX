// -----------------------------------------------------------------------
// <copyright file="AuthEndpointGuardWebApplicationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ServiceDefaults;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Grpc.Endpoints;
using DcsvIo.D2.Auth.Http.Endpoints;
using DcsvIo.D2.Auth.Startup;
using DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Protos;
using global::Grpc.Core;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.Http;
using global::Microsoft.Extensions.DependencyInjection;
using global::Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// WebApplication-model integration tests for the deny-by-default auth endpoint
/// guard. These tests exercise the PRODUCTION code path:
/// <c>WebApplication.CreateBuilder()</c> → <c>services.AddD2AuthEndpointGuard()</c>
/// → <c>builder.Build()</c> → <c>app.MapXxx()</c> after build → host
/// <c>StartAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why these tests exist</strong>: the former <c>IHostedService</c>
/// implementation was a production bug — in the <c>WebApplication</c> model,
/// <c>app.MapXxx()</c> calls happen AFTER <c>builder.Build()</c> and write into
/// <c>WebApplication.DataSources</c>. The DI-resolved <c>EndpointDataSource</c>
/// singleton captured by the hosted service was a SEPARATE composite that was
/// still EMPTY when <c>StartAsync</c> ran. Result: undeclared endpoint ⇒ host
/// starts clean (a NO-OP guard).
/// </para>
/// <para>
/// The fix registers the guard as an <c>IStartupFilter</c> instead.
/// <c>IStartupFilter.Configure(next)</c> calls <c>next(app)</c> first (which
/// triggers <c>UseRouting()</c> and merges <c>WebApplication.DataSources</c>
/// into the DI <c>EndpointDataSource</c> composite), then walks the now-
/// populated endpoint set. Any violation throws before Kestrel starts accepting
/// connections.
/// </para>
/// <para>
/// The tests use <c>IHost.StartAsync</c> + <c>IHost.StopAsync</c> rather than
/// <c>app.RunAsync()</c> so the assertion path is not blocked by an infinite
/// serve loop.
/// </para>
/// </remarks>
public sealed class AuthEndpointGuardWebApplicationTests
{
    // ── HTTP endpoints — WebApplication model ────────────────────────────

    /// <summary>
    /// Undeclared HTTP endpoint in the WebApplication model MUST block host start.
    /// This test FAILS on the former IHostedService implementation (the bug) and
    /// PASSES after the IStartupFilter fix.
    /// </summary>
    [Fact]
    public async Task WebApp_UndeclaredHttpEndpoint_ThrowsOnStart()
    {
        var act = async () =>
        {
            // Registered AFTER Build() — this is the production pattern
            // that exposed the IHostedService bug: MapXxx writes into
            // WebApplication.DataSources, which IHostedService missed.
            // Route uses a literal path to avoid ASP0018 (unused route param).
            await using var host = BuildWebApp(app =>
                app.MapGet("/files/get", () => Results.Text("should not start")));

            await host.StartAsync();
        };

        await act.Should().ThrowAsync<InvalidOperationException>(
            "undeclared HTTP endpoint in WebApplication model must block host start — "
            + "this proves the IStartupFilter fix resolves the former IHostedService bug");
    }

    /// <summary>
    /// Undeclared HTTP endpoint — the exception message must name the route.
    /// </summary>
    [Fact]
    public async Task WebApp_UndeclaredHttpEndpoint_ExceptionMessageNamesRoute()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var host = BuildWebApp(app =>
                app.MapGet("/files/get", () => Results.Text("should not start")));

            await host.StartAsync();
        });

        ex.Message.Should().Contain(
            "/files/get",
            "the exception message must name the undeclared route for operator triage");
    }

    /// <summary>
    /// Declared HTTP endpoint in the WebApplication model must start cleanly.
    /// </summary>
    [Fact]
    public async Task WebApp_DeclaredHttpEndpoint_RequireAnyScope_StartsCleanly()
    {
        await using var host = BuildWebApp(app =>
            app.MapGet(
                    "/files/{id:guid}",
                    (Guid id) => Results.Text($"ok {id}"))
                .WithMetadata(EndpointScopeMetadata.ForScopes(
                    ["files.read"],
                    DcsvIo.D2.Auth.Abstractions.ScopeMatch.Any)));

        await host.StartAsync();

        host.Should().NotBeNull(
            "declared HTTP endpoint with RequireAnyScope must allow host start");

        await host.StopAsync();
    }

    /// <summary>
    /// Infrastructure paths (no auth metadata) in the WebApplication model must
    /// start cleanly — the infra-path skip applies in the production model.
    /// </summary>
    [Fact]
    public async Task WebApp_InfraPath_Health_StartsCleanly()
    {
        // /health carries no auth metadata — infra-path skip must fire.
        await using var host = BuildWebApp(app =>
            app.MapGet("/health", () => Results.Text("ok")));

        await host.StartAsync();

        host.Should().NotBeNull(
            "infrastructure paths must be skipped even with no auth metadata");

        await host.StopAsync();
    }

    /// <summary>
    /// HarmlessEndpoint metadata in the WebApplication model must start cleanly.
    /// </summary>
    [Fact]
    public async Task WebApp_DeclaredHttpEndpoint_HarmlessEndpoint_StartsCleanly()
    {
        await using var host = BuildWebApp(app =>
            app.MapGet("/internal/status", () => Results.Text("ok"))
               .WithMetadata(EndpointScopeMetadata.HarmlessEndpoint));

        await host.StartAsync();

        host.Should().NotBeNull(
            "HTTP endpoint with HarmlessEndpoint metadata must allow host start");

        await host.StopAsync();
    }

    // ── gRPC endpoints — WebApplication model ────────────────────────────

    /// <summary>
    /// gRPC service with fluent scope metadata in the WebApplication model
    /// must start cleanly — the gRPC catch-all skip and declared-intent check both
    /// apply in the production model.
    /// </summary>
    [Fact]
    public async Task WebApp_GrpcFluent_RequireAnyScope_StartsCleanly()
    {
        await using var host = BuildWebAppWithGrpc(app =>
            app.MapGrpcService<EchoService>()
               .WithMetadata(MethodScopeMetadata.ForScopes(
                   ["test.scope"],
                   DcsvIo.D2.Auth.Abstractions.ScopeMatch.Any)));

        await host.StartAsync();

        host.Should().NotBeNull(
            "gRPC service with fluent scope metadata must allow host start in WebApp model");

        await host.StopAsync();
    }

    /// <summary>
    /// gRPC service with class-level <c>[D2RequireAnyScope]</c> in the WebApplication
    /// model must start cleanly — attribute projection onto endpoint metadata applies.
    /// </summary>
    [Fact]
    public async Task WebApp_GrpcAttribute_ClassLevel_D2RequireAnyScope_StartsCleanly()
    {
        await using var host = BuildWebAppWithGrpc(app =>
            app.MapGrpcService<AttributeEchoService>());

        await host.StartAsync();

        host.Should().NotBeNull(
            "gRPC service with class-level [D2RequireAnyScope] must allow host start in WebApp model");

        await host.StopAsync();
    }

    /// <summary>
    /// Undeclared gRPC service in the WebApplication model MUST block host start.
    /// Proves the catch-all skip does NOT swallow real undeclared gRPC method endpoints.
    /// </summary>
    [Fact]
    public async Task WebApp_GrpcUndeclared_NoAuthIntent_ThrowsOnStart()
    {
        var act = async () =>
        {
            await using var host = BuildWebAppWithGrpc(app =>
            {
                // No fluent or attribute declaration.
                app.MapGrpcService<UndeclaredEchoService>();
            });

            await host.StartAsync();
        };

        await act.Should().ThrowAsync<InvalidOperationException>(
            "undeclared gRPC service in WebApplication model must block host start");
    }

    /// <summary>
    /// Undeclared gRPC service in the WebApplication model: exception names the
    /// gRPC method route.
    /// </summary>
    [Fact]
    public async Task WebApp_GrpcUndeclared_ExceptionMessageNamesGrpcRoute()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await using var host = BuildWebAppWithGrpc(app =>
                app.MapGrpcService<UndeclaredEchoService>());

            await host.StartAsync();
        });

        ex.Message.Should().Contain(
            "d2.test.auth.TestEcho",
            "the guard exception must name the undeclared gRPC service route in WebApp model");
    }

    // ── Guard opt-out path ────────────────────────────────────────────────

    /// <summary>
    /// When the guard is NOT registered, undeclared endpoints must NOT block host
    /// start — mirrors the SkipAuthEndpointGuard=true path.
    /// </summary>
    [Fact]
    public async Task WebApp_GuardNotRegistered_UndeclaredEndpoint_StartsCleanly()
    {
        await using var host = BuildWebApp(
            app => app.MapGet("/undeclared-allowed", () => Results.Text("ok")),
            registerGuard: false);

        await host.StartAsync();

        host.Should().NotBeNull(
            "unregistered guard must not block host start on undeclared endpoints");

        await host.StopAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <c>WebApplication</c> with the guard registered. Endpoints are
    /// mapped AFTER <c>Build()</c> via the <paramref name="configureApp"/>
    /// callback — exactly the production pattern where the former IHostedService
    /// bug manifested.
    /// </summary>
    private static WebApplication BuildWebApp(
        Action<WebApplication> configureApp,
        bool registerGuard = true)
    {
        // Ephemeral port so parallel test invocations don't collide.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = ["--urls", "http://127.0.0.1:0"],
        });

        builder.Logging.ClearProviders();
        builder.Services.AddRouting();

        if (registerGuard)
            builder.Services.AddD2AuthEndpointGuard();

        var app = builder.Build();

        // Map endpoints AFTER Build() — this is what the IHostedService
        // implementation missed: MapXxx writes into WebApplication.DataSources,
        // which is a separate collection from the DI-resolved EndpointDataSource
        // singleton that the hosted service injected at construction time.
        configureApp(app);

        return app;
    }

    /// <summary>
    /// Builds a <c>WebApplication</c> with gRPC + the guard registered.
    /// Endpoints are mapped AFTER <c>Build()</c>.
    /// </summary>
    private static WebApplication BuildWebAppWithGrpc(
        Action<WebApplication> configureApp)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = ["--urls", "http://127.0.0.1:0"],
        });

        builder.Logging.ClearProviders();
        builder.Services.AddRouting();
        builder.Services.AddGrpc();
        builder.Services.AddD2AuthEndpointGuard();

        var app = builder.Build();
        configureApp(app);

        return app;
    }

    // ── Test service implementations ──────────────────────────────────────

    /// <summary>Bare service — used with fluent scope metadata.</summary>
    private sealed class EchoService : TestEcho.TestEchoBase
    {
        public override Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
            => Task.FromResult(new EchoReply { Echoed = request.Payload });
    }

    /// <summary>Bare service — no declarations; used by undeclared failure tests.</summary>
    private sealed class UndeclaredEchoService : TestEcho.TestEchoBase
    {
        public override Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
            => Task.FromResult(new EchoReply { Echoed = request.Payload });
    }

    /// <summary>Class-level <c>[D2RequireAnyScope]</c> — attribute declared intent.</summary>
    [D2RequireAnyScope("test.scope")]
    private sealed class AttributeEchoService : TestEcho.TestEchoBase
    {
        public override Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
            => Task.FromResult(new EchoReply { Echoed = request.Payload });
    }
}
