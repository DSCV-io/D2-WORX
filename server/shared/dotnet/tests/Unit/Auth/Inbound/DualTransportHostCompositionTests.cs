// -----------------------------------------------------------------------
// <copyright file="DualTransportHostCompositionTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound;

using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Grpc;
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

/// <summary>
/// Exercises the dual-transport host scenario: a single
/// <see cref="IServiceCollection"/> calls BOTH <c>AddD2AuthHttp()</c> AND
/// <c>AddD2AuthGrpc()</c>. Verifies that:
/// <list type="number">
///   <item>Composition succeeds in either registration order.</item>
///   <item>The interceptor is registered exactly once on
///     <see cref="GrpcServiceOptions.Interceptors"/>.</item>
///   <item>The scoped <see cref="IRequestContext"/> resolves correctly under
///     either transport since both extensions register an identical resolver
///     lambda reading from the shared <see cref="HttpContext.Items"/> slot.
///     <c>TryAddScoped</c>'s first-wins is harmless because the lambdas are
///     behaviorally identical.</item>
/// </list>
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "AccessToDisposedClosure",
    Justification = "Lambdas execute within the test method's using-scope; "
        + "the captured scope outlives the lambda's invocation.")]
public sealed class DualTransportHostCompositionTests
{
    [Fact]
    public void DualTransport_HttpThenGrpc_ResolvesContextFromSharedSlot()
    {
        var sp = BuildProvider(httpFirst: true);
        using var scope = sp.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var requestContext = new MutableRequestContext { IsAuthenticated = true };
        var ctx = new DefaultHttpContext();
        ctx.Items[D2HttpContextItems.REQUEST_CONTEXT] = requestContext;
        accessor.HttpContext = ctx;

        var resolved = scope.ServiceProvider.GetRequiredService<IRequestContext>();

        resolved.Should().BeSameAs(requestContext);
    }

    [Fact]
    public void DualTransport_GrpcThenHttp_ResolvesContextFromSharedSlot()
    {
        var sp = BuildProvider(httpFirst: false);
        using var scope = sp.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var requestContext = new MutableRequestContext { IsAuthenticated = true };
        var ctx = new DefaultHttpContext();
        ctx.Items[D2HttpContextItems.REQUEST_CONTEXT] = requestContext;
        accessor.HttpContext = ctx;

        var resolved = scope.ServiceProvider.GetRequiredService<IRequestContext>();

        resolved.Should().BeSameAs(requestContext);
    }

    [Fact]
    public void DualTransport_RegistersInterceptorExactlyOnce()
    {
        var sp = BuildProvider(httpFirst: true);

        var grpcOpts = sp.GetRequiredService<IOptions<GrpcServiceOptions>>().Value;
        var matches = grpcOpts.Interceptors
            .Where(d => d.Type == typeof(JwtAuthInterceptor))
            .ToList();

        matches.Should().HaveCount(1);
    }

    [Fact]
    public void DualTransport_HttpAccessor_RegisteredOnce()
    {
        // AddHttpContextAccessor() called by both extensions but is idempotent
        // by BCL contract — verify the resolved accessor is the same instance.
        var sp = BuildProvider(httpFirst: true);

        var first = sp.GetRequiredService<IHttpContextAccessor>();
        var second = sp.GetRequiredService<IHttpContextAccessor>();

        first.Should().BeSameAs(second);
    }

    private static ServiceProvider BuildProvider(bool httpFirst)
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

        if (httpFirst)
        {
            services.AddD2AuthHttp();
            services.AddD2AuthGrpc();
        }
        else
        {
            services.AddD2AuthGrpc();
            services.AddD2AuthHttp();
        }

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Minimal in-memory <see cref="ITieredCache"/> stub — required to satisfy
    /// <c>TieredCacheSessionLivenessTracker</c>'s constructor at composition.
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
