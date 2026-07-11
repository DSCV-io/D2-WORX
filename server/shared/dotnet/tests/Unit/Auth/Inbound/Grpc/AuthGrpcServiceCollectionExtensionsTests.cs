// -----------------------------------------------------------------------
// <copyright file="AuthGrpcServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Grpc;

using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Grpc;
using D2.Shared.Auth.Grpc.Ambient;
using D2.Shared.Auth.Grpc.Interceptors;
using D2.Shared.Auth.Http;
using D2.Shared.Caching;
using D2.Shared.Caching.Local.Default;
using D2.Shared.Context.Abstractions;
using global::Grpc.AspNetCore.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "AccessToDisposedClosure",
    Justification = "Lambdas execute within the test method's using-scope; "
        + "the captured scope outlives the lambda's invocation.")]
public sealed class AuthGrpcServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2AuthGrpc_RegistersHttpContextAccessor()
    {
        var sp = BuildProvider();

        sp.GetService<IHttpContextAccessor>().Should().NotBeNull();
    }

    [Fact]
    public void AddD2AuthGrpc_RegistersInterceptorAsSingleton()
    {
        var services = BuildServices();
        services.AddD2AuthGrpc();
        var sp = services.BuildServiceProvider();

        var first = sp.GetRequiredService<JwtAuthInterceptor>();
        var second = sp.GetRequiredService<JwtAuthInterceptor>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddD2AuthGrpc_AttachesInterceptorToGrpcServiceOptions()
    {
        var sp = BuildProvider();

        var grpcOpts = sp.GetRequiredService<IOptions<GrpcServiceOptions>>().Value;

        grpcOpts.Interceptors.Should().Contain(d => d.Type == typeof(JwtAuthInterceptor));
    }

    [Fact]
    public void AddD2AuthGrpc_CalledTwice_DoesNotDoubleRegisterInterceptor()
    {
        var services = BuildServices();
        services.AddD2AuthGrpc();
        services.AddD2AuthGrpc();
        var sp = services.BuildServiceProvider();

        var grpcOpts = sp.GetRequiredService<IOptions<GrpcServiceOptions>>().Value;
        var matches = grpcOpts.Interceptors
            .Where(d => d.Type == typeof(JwtAuthInterceptor))
            .ToList();

        matches.Should().HaveCount(1);
    }

    [Fact]
    public void AddD2AuthGrpc_RegistersScopedRequestContext()
    {
        var sp = BuildProvider();
        using var scope = sp.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

        // Cross-transport resolver reads from HttpContext.Items[REQUEST_CONTEXT]
        // — the gRPC interceptor writes the validated context to that slot
        // (alongside its ServerCallContext.UserState write) so a single
        // resolver lambda works across both HTTP and gRPC transports.
        var httpContext = new DefaultHttpContext();
        var requestContext = new MutableRequestContext { IsAuthenticated = true };
        httpContext.Items[D2HttpContextItems.REQUEST_CONTEXT] = requestContext;
        accessor.HttpContext = httpContext;

        var resolved = scope.ServiceProvider.GetRequiredService<IRequestContext>();

        resolved.Should().BeSameAs(requestContext);
    }

    [Fact]
    public void AddD2AuthGrpc_RegistersAmbientRequestScopeAccessor()
    {
        var sp = BuildProvider();

        // §1.3 — actual resolution, not descriptor-presence. The outbound
        // forwarding credential's channel build does a hard
        // GetRequiredService<IAmbientRequestScopeAccessor>(), so the port MUST
        // resolve on a gRPC-inbound forwarding host.
        var accessor = sp.GetRequiredService<IAmbientRequestScopeAccessor>();

        accessor.Should().BeOfType<GrpcHttpContextAmbientRequestScopeAccessor>();
    }

    [Fact]
    public void AddD2AuthGrpc_RegistersAmbientRequestScopeAccessorAsSingleton()
    {
        var sp = BuildProvider();

        var first = sp.GetRequiredService<IAmbientRequestScopeAccessor>();
        var second = sp.GetRequiredService<IAmbientRequestScopeAccessor>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddD2AuthGrpc_AmbientRequestScopeAccessor_ReadsRequestScope()
    {
        // Integration of the adapter with its DI registration: the resolved port
        // reads the current call's HttpContext.RequestServices end-to-end. Set a
        // DefaultHttpContext (under gRPC this is the per-call gRPC context) with a
        // known RequestServices on the resolved IHttpContextAccessor, then assert
        // the resolved IAmbientRequestScopeAccessor.Current surfaces that scope.
        var sp = BuildProvider();
        var requestScope = new ServiceCollection().BuildServiceProvider();
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = new DefaultHttpContext { RequestServices = requestScope };

        var accessor = sp.GetRequiredService<IAmbientRequestScopeAccessor>();

        accessor.Current.Should().BeSameAs(requestScope);
    }

    [Fact]
    public void AddD2AuthGrpc_CalledTwice_AmbientRequestScopeAccessorStillResolvesOnce()
    {
        var services = BuildServices();
        services.AddD2AuthGrpc();
        services.AddD2AuthGrpc();
        var sp = services.BuildServiceProvider();

        // Idempotent: TryAddSingleton means the second call does not change
        // resolvability and there is exactly one registration.
        var accessor = sp.GetRequiredService<IAmbientRequestScopeAccessor>();
        accessor.Should().BeOfType<GrpcHttpContextAmbientRequestScopeAccessor>();

        var descriptors = services
            .Where(d => d.ServiceType == typeof(IAmbientRequestScopeAccessor))
            .ToList();
        descriptors.Should().HaveCount(1);
    }

    [Fact]
    public void DualTransport_HttpThenGrpc_AmbientPortResolvesAndReadsScope()
    {
        // A dual-transport host (HTTP endpoints + gRPC services on one Kestrel
        // host) calls BOTH AddD2AuthHttp() and AddD2AuthGrpc(). TryAddSingleton is
        // first-wins, so the HTTP adapter wins here — harmless because both impls
        // read IHttpContextAccessor.HttpContext.RequestServices identically. The
        // port must still resolve AND behave. This is the new cross-extension seam
        // the §1.3 "every seam" discipline demands now the port has TWO registrars.
        var services = BuildServices();
        services.AddD2AuthHttp();
        services.AddD2AuthGrpc();
        var sp = services.BuildServiceProvider();

        var requestScope = new ServiceCollection().BuildServiceProvider();
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = new DefaultHttpContext { RequestServices = requestScope };

        var accessor = sp.GetRequiredService<IAmbientRequestScopeAccessor>();
        accessor.Current.Should().BeSameAs(requestScope);

        services
            .Where(d => d.ServiceType == typeof(IAmbientRequestScopeAccessor))
            .Should().HaveCount(1, "TryAddSingleton first-wins — a single registration across both transports");
    }

    [Fact]
    public void DualTransport_GrpcThenHttp_AmbientPortResolvesAndReadsScope()
    {
        // Reverse registration order — the gRPC adapter wins. Identical behavior:
        // the port resolves, reads the request scope, and is registered exactly
        // once. Proves the dual-transport host is correct regardless of order.
        var services = BuildServices();
        services.AddD2AuthGrpc();
        services.AddD2AuthHttp();
        var sp = services.BuildServiceProvider();

        var requestScope = new ServiceCollection().BuildServiceProvider();
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
        httpContextAccessor.HttpContext = new DefaultHttpContext { RequestServices = requestScope };

        var accessor = sp.GetRequiredService<IAmbientRequestScopeAccessor>();
        accessor.Current.Should().BeSameAs(requestScope);
        accessor.Should().BeOfType<GrpcHttpContextAmbientRequestScopeAccessor>(
            "gRPC registered first, so TryAddSingleton keeps the gRPC adapter");

        services
            .Where(d => d.ServiceType == typeof(IAmbientRequestScopeAccessor))
            .Should().HaveCount(1, "TryAddSingleton first-wins — a single registration across both transports");
    }

    [Fact]
    public void RequestContextResolution_SlotHoldsWrongType_FallsThroughToMutable()
    {
        var sp = BuildProvider();
        using var scope = sp.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Items[D2HttpContextItems.REQUEST_CONTEXT] = "not-a-request-context";
        accessor.HttpContext = httpContext;

        var resolved = scope.ServiceProvider.GetRequiredService<IRequestContext>();
        var mutable = scope.ServiceProvider.GetRequiredService<MutableRequestContext>();

        resolved.Should().BeSameAs(mutable);
    }

    [Fact]
    public void RequestContextResolution_BeforeInterceptorRan_ReturnsUnestablishedMutable()
    {
        var sp = BuildProvider();
        using var scope = sp.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        accessor.HttpContext = new DefaultHttpContext();

        var resolved = scope.ServiceProvider.GetRequiredService<IRequestContext>();
        var mutable = scope.ServiceProvider.GetRequiredService<MutableRequestContext>();

        resolved.Should().BeSameAs(mutable);
        resolved.Origin.Should().Be(RequestOrigin.Unestablished);
    }

    [Fact]
    public void RequestContextResolution_NoActiveHttpContext_ReturnsUnestablishedMutable()
    {
        // Regression: throw-only path broke System workers on dual-transport hosts.
        var sp = BuildProvider();
        using var scope = sp.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IRequestContext>();
        var mutable = scope.ServiceProvider.GetRequiredService<MutableRequestContext>();

        resolved.Should().BeSameAs(mutable);
        resolved.Origin.Should().Be(RequestOrigin.Unestablished);
    }

    [Fact]
    public void AddD2AuthGrpc_WithoutAddD2Auth_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddD2AuthGrpc();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AddD2Auth*");
    }

    [Fact]
    public void AddD2AuthGrpc_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2AuthGrpc();

        act.Should().Throw<ArgumentNullException>();
    }

    private static ServiceProvider BuildProvider()
    {
        var services = BuildServices();
        services.AddD2AuthGrpc();
        return services.BuildServiceProvider();
    }

    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddD2LocalCache();
        services.AddSingleton<ITieredCache, FakeTieredCacheStub>();
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri("https://edge.internal");
            opts.Audience = "files";
        });
        return services;
    }

    /// <summary>
    /// Minimal in-memory <see cref="ITieredCache"/> stub — required to satisfy
    /// <c>TieredCacheSessionLivenessTracker</c>'s constructor at composition.
    /// Only the interface contract is needed; no method should be called by
    /// these tests.
    /// </summary>
    private sealed class FakeTieredCacheStub : ITieredCache
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
