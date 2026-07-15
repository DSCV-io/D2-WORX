// -----------------------------------------------------------------------
// <copyright file="RequireD2GrpcScopeExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Endpoints;

using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Grpc.Endpoints;
using DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Protos;
using global::Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

/// <summary>
/// Verifies that the <see cref="RequireD2GrpcScopeExtensions"/> fluent methods
/// attach the correct <see cref="MethodScopeMetadata"/> onto the endpoint
/// metadata when called on the <see cref="GrpcServiceEndpointConventionBuilder"/>
/// returned by <c>MapGrpcService&lt;T&gt;()</c>.
///
/// <para>
/// <strong>Test approach</strong>: <see cref="GrpcServiceEndpointConventionBuilder"/>
/// has an internal constructor and cannot be directly instantiated in tests.
/// Each test builds a minimal in-process ASP.NET Core test host via
/// <see cref="HostBuilder"/>, wires the fluent call on the
/// <c>MapGrpcService&lt;T&gt;()</c> result, resolves the
/// <see cref="EndpointDataSource"/>, and asserts the metadata attached to the
/// resulting real gRPC method endpoints. This validates the full
/// builder-to-endpoint projection path that production uses.
/// </para>
/// <para>
/// Error-path tests (null or empty scope arguments) are exercised by calling
/// the extension directly on a real builder instance — the guard throws before
/// any metadata is attached.
/// </para>
/// </summary>
public sealed class RequireD2GrpcScopeExtensionsTests
{
    // ---- RequireAnyScope ----

    [Fact]
    public async Task RequireAnyScope_SingleScope_AttachesMetadataWithMatchAny()
    {
        var meta = await GetFirstMethodMetadataAsync(
            builder => builder.RequireAnyScope("files.read"));

        meta.Should().NotBeNull();
        meta.IsHarmlessEndpoint.Should().BeFalse();
        meta.Match.Should().Be(ScopeMatch.Any);
        meta.Scopes.Should().BeEquivalentTo(new[] { "files.read" });
    }

    [Fact]
    public async Task RequireAnyScope_MultipleScopes_AttachesAllWithMatchAny()
    {
        var meta = await GetFirstMethodMetadataAsync(
            builder => builder.RequireAnyScope("files.read", "files.admin"));

        meta!.Match.Should().Be(ScopeMatch.Any);
        meta.Scopes.Should().BeEquivalentTo(new[] { "files.read", "files.admin" });
    }

    [Fact]
    public async Task RequireAnyScope_EmptyScope_Throws()
    {
        await AssertFluentThrowsAsync<ArgumentException>(
            builder => builder.RequireAnyScope(string.Empty));
    }

    [Fact]
    public async Task RequireAnyScope_NullScope_Throws()
    {
        await AssertFluentThrowsAsync<ArgumentNullException>(
            builder => builder.RequireAnyScope(null!));
    }

    [Fact]
    public async Task RequireAnyScope_WhitespaceScope_Throws()
    {
        await AssertFluentThrowsAsync<ArgumentException>(
            builder => builder.RequireAnyScope("   "));
    }

    [Fact]
    public async Task RequireAnyScope_AdditionalScopeWhitespace_Throws()
    {
        await AssertFluentThrowsAsync<ArgumentException>(
            builder => builder.RequireAnyScope("files.read", "   "));
    }

    [Fact]
    public void RequireAnyScope_NullBuilder_Throws()
    {
        GrpcServiceEndpointConventionBuilder? builder = null;

        var act = () => builder!.RequireAnyScope("files.read");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RequireAnyScope_NullAdditionalScopesArray_Throws()
    {
        await AssertFluentThrowsAsync<ArgumentNullException>(
            builder => builder.RequireAnyScope("files.read", null!));
    }

    [Fact]
    public async Task RequireAnyScope_FluentReturn_IsSameBuilder()
    {
        GrpcServiceEndpointConventionBuilder? captured = null;
        GrpcServiceEndpointConventionBuilder? returned = null;

        await BuildHostAsync(grpcBuilder =>
        {
            captured = grpcBuilder;
            returned = grpcBuilder.RequireAnyScope("files.read");
        });

        returned.Should().BeSameAs(captured);
    }

    // ---- RequireAllScopes ----

    [Fact]
    public async Task RequireAllScopes_SingleScope_AttachesMetadataWithMatchAll()
    {
        var meta = await GetFirstMethodMetadataAsync(
            builder => builder.RequireAllScopes("files.read"));

        meta.Should().NotBeNull();
        meta.IsHarmlessEndpoint.Should().BeFalse();
        meta.Match.Should().Be(ScopeMatch.All);
        meta.Scopes.Should().BeEquivalentTo(new[] { "files.read" });
    }

    [Fact]
    public async Task RequireAllScopes_MultipleScopes_AttachesAllWithMatchAll()
    {
        var meta = await GetFirstMethodMetadataAsync(
            builder => builder.RequireAllScopes("files.read", "files.write"));

        meta!.Match.Should().Be(ScopeMatch.All);
        meta.Scopes.Should().BeEquivalentTo(new[] { "files.read", "files.write" });
    }

    [Fact]
    public async Task RequireAllScopes_EmptyScope_Throws()
    {
        await AssertFluentThrowsAsync<ArgumentException>(
            builder => builder.RequireAllScopes(string.Empty));
    }

    [Fact]
    public async Task RequireAllScopes_NullScope_Throws()
    {
        await AssertFluentThrowsAsync<ArgumentNullException>(
            builder => builder.RequireAllScopes(null!));
    }

    [Fact]
    public async Task RequireAllScopes_WhitespaceScope_Throws()
    {
        await AssertFluentThrowsAsync<ArgumentException>(
            builder => builder.RequireAllScopes("   "));
    }

    [Fact]
    public async Task RequireAllScopes_AdditionalScopeWhitespace_Throws()
    {
        await AssertFluentThrowsAsync<ArgumentException>(
            builder => builder.RequireAllScopes("files.read", "   "));
    }

    [Fact]
    public void RequireAllScopes_NullBuilder_Throws()
    {
        GrpcServiceEndpointConventionBuilder? builder = null;

        var act = () => builder!.RequireAllScopes("files.read");

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task RequireAllScopes_NullAdditionalScopesArray_Throws()
    {
        await AssertFluentThrowsAsync<ArgumentNullException>(
            builder => builder.RequireAllScopes("files.read", null!));
    }

    [Fact]
    public async Task RequireAllScopes_FluentReturn_IsSameBuilder()
    {
        GrpcServiceEndpointConventionBuilder? captured = null;
        GrpcServiceEndpointConventionBuilder? returned = null;

        await BuildHostAsync(grpcBuilder =>
        {
            captured = grpcBuilder;
            returned = grpcBuilder.RequireAllScopes("files.read");
        });

        returned.Should().BeSameAs(captured);
    }

    // ---- MarkAsD2HarmlessEndpoint ----

    [Fact]
    public async Task MarkAsD2HarmlessEndpoint_AttachesHarmlessEndpointSingleton()
    {
        var meta = await GetFirstMethodMetadataAsync(
            builder => builder.MarkAsD2HarmlessEndpoint());

        meta.Should().BeSameAs(MethodScopeMetadata.HarmlessEndpoint);
    }

    [Fact]
    public void MarkAsD2HarmlessEndpoint_NullBuilder_Throws()
    {
        GrpcServiceEndpointConventionBuilder? builder = null;

        var act = () => builder!.MarkAsD2HarmlessEndpoint();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task MarkAsD2HarmlessEndpoint_FluentReturn_IsSameBuilder()
    {
        GrpcServiceEndpointConventionBuilder? captured = null;
        GrpcServiceEndpointConventionBuilder? returned = null;

        await BuildHostAsync(grpcBuilder =>
        {
            captured = grpcBuilder;
            returned = grpcBuilder.MarkAsD2HarmlessEndpoint();
        });

        returned.Should().BeSameAs(captured);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal in-process host that wires <c>TestEchoService</c> via
    /// <c>MapGrpcService&lt;TestEchoService&gt;()</c>, applies
    /// <paramref name="configure"/> to the resulting builder, then starts
    /// and returns the host. The caller is responsible for disposing it.
    /// </summary>
    private static async Task<IHost> BuildHostAsync(
        Action<GrpcServiceEndpointConventionBuilder> configure)
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
                            configure(endpoints.MapGrpcService<MinimalEchoService>());
                        });
                    });
            });

        return await hostBuilder.StartAsync();
    }

    /// <summary>
    /// Builds the host, resolves the <see cref="EndpointDataSource"/>, and
    /// returns the <see cref="MethodScopeMetadata"/> from the first real gRPC
    /// method endpoint (route pattern starts with
    /// <c>/d2.test.auth.TestEcho/</c>). Returns <see langword="null"/> when no
    /// such metadata is present.
    /// </summary>
    private static async Task<MethodScopeMetadata?> GetFirstMethodMetadataAsync(
        Action<GrpcServiceEndpointConventionBuilder> configure)
    {
        using var host = await BuildHostAsync(configure);
        var endpointDataSource = host.Services.GetRequiredService<EndpointDataSource>();

        var methodEndpoint = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .FirstOrDefault(e =>
                e.RoutePattern.RawText?.StartsWith(
                    "/d2.test.auth.TestEcho/",
                    StringComparison.OrdinalIgnoreCase) == true);

        return methodEndpoint?.Metadata.GetMetadata<MethodScopeMetadata>();
    }

    /// <summary>
    /// Builds the host, wires <paramref name="configure"/> (which is expected
    /// to throw), and asserts a <typeparamref name="TException"/> is thrown
    /// synchronously by the fluent call before the host finishes startup.
    /// </summary>
    private static async Task AssertFluentThrowsAsync<TException>(
        Action<GrpcServiceEndpointConventionBuilder> configure)
        where TException : Exception
    {
        // The fluent methods throw synchronously during the Configure callback
        // (before any endpoint is served), so the exception propagates out of
        // StartAsync.
        var act = async () => await BuildHostAsync(configure);

        await act.Should().ThrowAsync<TException>();
    }

    /// <summary>
    /// Minimal gRPC service implementation used only to get a real
    /// <see cref="GrpcServiceEndpointConventionBuilder"/> from
    /// <c>MapGrpcService&lt;T&gt;()</c>. No actual RPC methods are called
    /// during these tests.
    /// </summary>
    private sealed class MinimalEchoService : TestEcho.TestEchoBase
    {
        public override Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
            => Task.FromResult(new EchoReply { Echoed = request.Payload });
    }
}
