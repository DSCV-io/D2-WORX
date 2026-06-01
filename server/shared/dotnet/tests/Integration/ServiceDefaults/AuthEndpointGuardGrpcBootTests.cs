// -----------------------------------------------------------------------
// <copyright file="AuthEndpointGuardGrpcBootTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.ServiceDefaults;

using AwesomeAssertions;
using D2.Shared.Auth.Grpc.Endpoints;
using D2.Shared.Auth.Http.Endpoints;
using D2.Shared.Auth.Startup;
using D2.Shared.Tests.Unit.Auth.Inbound.Grpc.Protos;
using global::Grpc.Core;
using global::Microsoft.AspNetCore.Builder;
using global::Microsoft.AspNetCore.Hosting;
using global::Microsoft.AspNetCore.Http;
using global::Microsoft.AspNetCore.Routing;
using global::Microsoft.AspNetCore.TestHost;
using global::Microsoft.Extensions.DependencyInjection;
using global::Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// Boot-guard integration tests for the gRPC transport path: verifies that
/// <see cref="AuthEndpointGuardStartupFilter"/> correctly classifies real gRPC
/// method endpoints and gRPC infrastructure catch-all endpoints registered by
/// <c>MapGrpcService&lt;T&gt;()</c>.
/// </summary>
/// <remarks>
/// All tests build a minimal <c>HostBuilder</c> +
/// <c>UseTestServer</c> + <c>AddGrpc()</c> + real
/// <c>MapGrpcService&lt;T&gt;()</c> calls so the full gRPC endpoint-registration
/// pipeline runs — including the infrastructure catch-all endpoints that
/// triggered the false-positive boot failure this test class regression-pins.
/// </remarks>
public sealed class AuthEndpointGuardGrpcBootTests
{
    // ── gRPC fluent path (regression: was false-positive before fix) ──────

    /// <summary>
    /// A gRPC service declared via fluent <c>.RequireAnyScope("test.scope")</c>
    /// must NOT cause a boot failure. This is the primary regression test for
    /// the gRPC infrastructure catch-all false-positive: before the fix,
    /// <c>MapGrpcService&lt;T&gt;()</c>'s unimplemented-route catch-alls had no
    /// auth metadata and the guard threw on them.
    /// </summary>
    [Fact]
    public async Task GrpcFluent_RequireAnyScope_HostStartsCleanly()
    {
        using var host = await BuildHostAsync(endpoints =>
        {
            endpoints.MapGrpcService<EchoService>()
                .RequireAnyScope("test.scope");
        });

        host.Should().NotBeNull(
            "a gRPC service with fluent .RequireAnyScope must not block host start");
    }

    // ── gRPC attribute path (end-to-end proof guard sees projected attr) ──

    /// <summary>
    /// A gRPC service declared via class-level <c>[D2RequireAnyScope("test.scope")]</c>
    /// must NOT cause a boot failure. This proves the guard reads the attribute
    /// projected onto endpoint metadata by <c>MapGrpcService&lt;T&gt;()</c>.
    /// </summary>
    [Fact]
    public async Task GrpcAttribute_ClassLevel_D2RequireAnyScope_HostStartsCleanly()
    {
        using var host = await BuildHostAsync(endpoints =>
        {
            endpoints.MapGrpcService<AttributeEchoService>();
        });

        host.Should().NotBeNull(
            "a gRPC service with class-level [D2RequireAnyScope] must not block host start");
    }

    // ── gRPC harmless endpoint (method-level attribute) ───────────────────

    /// <summary>
    /// A gRPC service method declared via method-level <c>[D2HarmlessEndpoint]</c>
    /// must NOT cause a boot failure.
    /// </summary>
    [Fact]
    public async Task GrpcAttribute_MethodLevel_D2HarmlessEndpoint_HostStartsCleanly()
    {
        using var host = await BuildHostAsync(endpoints =>
        {
            endpoints.MapGrpcService<HarmlessHealthService>();
        });

        host.Should().NotBeNull(
            "a gRPC service with method-level [D2HarmlessEndpoint] must not block host start");
    }

    // ── gRPC undeclared (guard still catches genuinely missing intent) ─────

    /// <summary>
    /// A gRPC service with NO fluent declaration, NO class attribute, and NO
    /// method attribute must cause a boot failure naming that service's route.
    /// Proves that the catch-all skip does not also skip real undeclared gRPC
    /// method endpoints.
    /// </summary>
    [Fact]
    public async Task GrpcUndeclared_NoAuthIntent_ThrowsOnStart()
    {
        var act = async () =>
        {
            using var host = await BuildHostAsync(endpoints =>
            {
                // No fluent or attribute declaration — guard must reject this.
                endpoints.MapGrpcService<UndeclaredEchoService>();
            });
        };

        await act.Should().ThrowAsync<InvalidOperationException>(
            "a gRPC service with no declared auth intent must block host start");
    }

    /// <summary>
    /// A gRPC service with NO declared auth intent: the exception message names
    /// the undeclared gRPC method route so operators can triage the offending
    /// service.
    /// </summary>
    [Fact]
    public async Task GrpcUndeclared_NoAuthIntent_ExceptionMessageNamesGrpcRoute()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var host = await BuildHostAsync(endpoints =>
            {
                endpoints.MapGrpcService<UndeclaredEchoService>();
            });
        });

        // The guard must name the gRPC method route in the exception message.
        ex.Message.Should().Contain(
            "d2.test.auth.TestEcho",
            "the guard exception must name the undeclared gRPC service route for operator triage");
    }

    // ── Mixed HTTP + gRPC (only HTTP offender named) ──────────────────────

    // ── Mixed HTTP + gRPC all three categories simultaneously ─────────────

    /// <summary>
    /// A host with three endpoint categories simultaneously:
    /// <list type="bullet">
    ///   <item>A gRPC service declared via class-level
    ///     <c>[D2RequireAnyScope]</c> (attribute path).</item>
    ///   <item>An HTTP endpoint declared via fluent
    ///     <c>.RequireAnyScope(...)</c> (fluent path).</item>
    ///   <item>An HTTP endpoint with NO declaration (the offender).</item>
    /// </list>
    /// The guard must throw and the exception message must name ONLY the
    /// undeclared HTTP endpoint — not the declared gRPC service or the
    /// declared HTTP endpoint. This proves the guard simultaneously
    /// distinguishes all three declaration categories in one host.
    /// </summary>
    [Fact]
    public async Task MixedHost_GrpcAttribute_HttpFluent_HttpUndeclared_ThrowsNamingUndeclaredOnly()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var host = await BuildHostAsync(endpoints =>
            {
                // gRPC declared via class-level attribute — must NOT appear in error.
                endpoints.MapGrpcService<AttributeEchoService>();

                // HTTP endpoint declared via fluent call — must NOT appear in error.
                endpoints.MapGet(
                    "/api/declared-http",
                    () => Results.Text("ok"))
                    .RequireAnyScope("files.read");

                // HTTP endpoint with no declaration — the sole offender.
                endpoints.MapGet(
                    "/api/undeclared-http",
                    () => Results.Text("should not start"));
            });
        });

        ex.Message.Should().Contain(
            "/api/undeclared-http",
            "the guard must name the undeclared HTTP route");

        ex.Message.Should().NotContain(
            "d2.test.auth.TestEcho",
            "the gRPC service declared via attribute must not appear in the error");

        ex.Message.Should().NotContain(
            "/api/declared-http",
            "the HTTP endpoint declared via fluent call must not appear in the error");
    }

    // ── Mixed HTTP + gRPC (only HTTP offender named) ──────────────────────

    /// <summary>
    /// A host with a correctly-declared gRPC service AND an undeclared HTTP
    /// endpoint: the guard names only the HTTP endpoint (the gRPC service is
    /// compliant; the catch-alls are skipped).
    /// </summary>
    [Fact]
    public async Task MixedHost_GrpcDeclared_HttpUndeclared_ThrowsNamingHttpOnly()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var host = await BuildHostAsync(endpoints =>
            {
                // gRPC declared correctly — should NOT appear in the error.
                endpoints.MapGrpcService<EchoService>()
                    .RequireAnyScope("test.scope");

                // HTTP endpoint with no auth declaration — the offender.
                endpoints.MapGet(
                    "/undeclared-http",
                    () => Results.Text("should not start"));
            });
        });

        ex.Message.Should().Contain(
            "/undeclared-http",
            "the guard must name the undeclared HTTP route");

        ex.Message.Should().NotContain(
            "d2.test.auth.TestEcho",
            "the correctly-declared gRPC service must not appear in the error message");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static Task<IHost> BuildHostAsync(
        Action<IEndpointRouteBuilder> configureEndpoints)
    {
        // The guard is registered as an IStartupFilter inside ConfigureWebHost.
        // IStartupFilter.Configure(next) runs during HTTP-pipeline construction
        // in GenericWebHostService.StartAsync — after UseEndpoints has wired
        // all endpoints into the routing composite. Registration order relative
        // to ConfigureWebHost vs outer ConfigureServices does NOT matter for
        // IStartupFilter (the former IHostedService required post-pipeline
        // registration to avoid seeing an empty EndpointDataSource).
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddLogging();
                        services.AddGrpc();
                        services.AddD2AuthEndpointGuard();
                    })
                    .Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(configureEndpoints);
                    });
            });

        return hostBuilder.StartAsync();
    }

    // ── Test service implementations ──────────────────────────────────────

    /// <summary>
    /// Bare service — no attribute declarations. Used with fluent
    /// <c>.RequireAnyScope(...)</c> on the builder.
    /// </summary>
    private sealed class EchoService : TestEcho.TestEchoBase
    {
        public override Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
            => Task.FromResult(new EchoReply { Echoed = request.Payload });
    }

    /// <summary>
    /// Bare service — no attribute declarations. Wired with no fluent or
    /// attribute declaration; used by the "undeclared" failure tests.
    /// </summary>
    private sealed class UndeclaredEchoService : TestEcho.TestEchoBase
    {
        public override Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
            => Task.FromResult(new EchoReply { Echoed = request.Payload });
    }

    /// <summary>
    /// Class decorated with <c>[D2RequireAnyScope("test.scope")]</c> — attribute
    /// path declared intent.
    /// </summary>
    [D2RequireAnyScope("test.scope")]
    private sealed class AttributeEchoService : TestEcho.TestEchoBase
    {
        public override Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
            => Task.FromResult(new EchoReply { Echoed = request.Payload });
    }

    /// <summary>
    /// Method decorated with <c>[D2HarmlessEndpoint]</c> — method-level attribute
    /// declared intent.
    /// </summary>
    private sealed class HarmlessHealthService : TestHealth.TestHealthBase
    {
        [D2HarmlessEndpoint]
        public override Task<HealthReply> Health(HealthRequest request, ServerCallContext context)
            => Task.FromResult(new HealthReply { Status = "ok" });
    }
}
