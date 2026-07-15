// -----------------------------------------------------------------------
// <copyright file="RequestContextResolverParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound;

using AwesomeAssertions;
using DcsvIo.D2.Auth;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Abstractions.Http;
using DcsvIo.D2.Auth.Grpc;
using DcsvIo.D2.Auth.Http;
using DcsvIo.D2.Caching;
using DcsvIo.D2.Caching.Local.Default;
using DcsvIo.D2.Context.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Pins that the two scoped <see cref="IRequestContext"/> resolver lambdas
/// (one registered by <c>AddD2AuthHttp()</c>, one by <c>AddD2AuthGrpc()</c>)
/// return EQUIVALENT results given identical <see cref="HttpContext"/> state.
/// Defends against future drift between the two duplicated inline lambdas
/// — the duplication is deliberate (Flavor A: avoid an inter-csproj
/// ProjectReference) but its safety relies on the two lambdas being
/// behaviorally identical.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "AccessToDisposedClosure",
    Justification = "Lambdas execute within the test method's using-scope; "
        + "the captured scope outlives the lambda's invocation.")]
public sealed class RequestContextResolverParityTests
{
    [Fact]
    public void HttpAndGrpc_PopulatedSlot_ReturnSameContext()
    {
        var requestContext = new MutableRequestContext { IsAuthenticated = true };

        var httpResolved = ResolveUnder(c => c.AddD2AuthHttp(), seedSlot: requestContext);
        var grpcResolved = ResolveUnder(c => c.AddD2AuthGrpc(), seedSlot: requestContext);

        httpResolved.Should().BeSameAs(requestContext);
        grpcResolved.Should().BeSameAs(requestContext);
        httpResolved.Should().BeSameAs(grpcResolved);
    }

    [Fact]
    public void HttpAndGrpc_EmptySlot_BothFallThroughToUnestablishedMutable()
    {
        // Dual-path: missing Items slot → scoped Mutable (Unestablished).
        // Both transports must stay parity-equivalent (no throw-only path).
        var httpResolved = ResolveUnder(c => c.AddD2AuthHttp(), seedSlot: null);
        var grpcResolved = ResolveUnder(c => c.AddD2AuthGrpc(), seedSlot: null);

        httpResolved.Should().BeOfType<MutableRequestContext>();
        grpcResolved.Should().BeOfType<MutableRequestContext>();
        httpResolved.Origin.Should().Be(RequestOrigin.Unestablished);
        grpcResolved.Origin.Should().Be(RequestOrigin.Unestablished);
        grpcResolved.Origin.Should().Be(httpResolved.Origin);
    }

    [Fact]
    public void HttpAndGrpc_AbsentHttpContext_BothFallThroughToUnestablishedMutable()
    {
        // Dual-path: no HttpContext (System workers / pre-pipeline) → scoped
        // Mutable. Both transports must stay parity-equivalent.
        var httpResolved = ResolveWithoutHttpContext(c => c.AddD2AuthHttp());
        var grpcResolved = ResolveWithoutHttpContext(c => c.AddD2AuthGrpc());

        httpResolved.Should().BeOfType<MutableRequestContext>();
        grpcResolved.Should().BeOfType<MutableRequestContext>();
        httpResolved.Origin.Should().Be(RequestOrigin.Unestablished);
        grpcResolved.Origin.Should().Be(RequestOrigin.Unestablished);
        grpcResolved.Origin.Should().Be(httpResolved.Origin);
    }

    [Fact]
    public void HttpAndGrpc_WrongTypeInSlot_BothFallThroughToUnestablishedMutable()
    {
        // Dual-path: wrong type in Items slot is ignored → scoped Mutable.
        // Both transports must stay parity-equivalent.
        var httpResolved = ResolveUnder(
            c => c.AddD2AuthHttp(), seedSlot: "not-a-request-context");
        var grpcResolved = ResolveUnder(
            c => c.AddD2AuthGrpc(), seedSlot: "not-a-request-context");

        httpResolved.Should().BeOfType<MutableRequestContext>();
        grpcResolved.Should().BeOfType<MutableRequestContext>();
        httpResolved.Origin.Should().Be(RequestOrigin.Unestablished);
        grpcResolved.Origin.Should().Be(RequestOrigin.Unestablished);
        grpcResolved.Origin.Should().Be(httpResolved.Origin);
    }

    private static IRequestContext ResolveUnder(
        Action<IServiceCollection> register, object? seedSlot)
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
        register(services);
        var sp = services.BuildServiceProvider();

        using var scope = sp.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var ctx = new DefaultHttpContext();
        if (seedSlot is not null)
            ctx.Items[D2HttpContextItems.REQUEST_CONTEXT] = seedSlot;
        accessor.HttpContext = ctx;

        return scope.ServiceProvider.GetRequiredService<IRequestContext>();
    }

    private static IRequestContext ResolveWithoutHttpContext(
        Action<IServiceCollection> register)
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
        register(services);
        var sp = services.BuildServiceProvider();

        using var scope = sp.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IRequestContext>();
    }

    /// <summary>
    /// Minimal in-memory <see cref="ITieredCache"/> stub — required to satisfy
    /// <c>TieredCacheSessionLivenessTracker</c>'s constructor at composition.
    /// </summary>
    private sealed class FakeTieredCacheStub : ITieredCache
    {
        public ValueTask<DcsvIo.D2.Result.D2Result<bool>> ExistsAsync(
            string key, CancellationToken ct = default)
            => new(DcsvIo.D2.Result.D2Result<bool>.Ok());

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
