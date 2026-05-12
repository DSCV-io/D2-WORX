// -----------------------------------------------------------------------
// <copyright file="RequestContextResolverParityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound;

using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Grpc;
using D2.Shared.Auth.Http;
using D2.Shared.Caching;
using D2.Shared.Caching.Local.Default;
using D2.Shared.Context.Abstractions;
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
    public void HttpAndGrpc_EmptySlot_BothThrow()
    {
        var httpAct = () => ResolveUnder(c => c.AddD2AuthHttp(), seedSlot: null);
        var grpcAct = () => ResolveUnder(c => c.AddD2AuthGrpc(), seedSlot: null);

        httpAct.Should().Throw<InvalidOperationException>();
        grpcAct.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void HttpAndGrpc_AbsentHttpContext_BothThrow()
    {
        var httpAct = () => ResolveWithoutHttpContext(c => c.AddD2AuthHttp());
        var grpcAct = () => ResolveWithoutHttpContext(c => c.AddD2AuthGrpc());

        httpAct.Should().Throw<InvalidOperationException>();
        grpcAct.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void HttpAndGrpc_WrongTypeInSlot_BothThrow()
    {
        var httpAct = () => ResolveUnder(
            c => c.AddD2AuthHttp(), seedSlot: "not-a-request-context");
        var grpcAct = () => ResolveUnder(
            c => c.AddD2AuthGrpc(), seedSlot: "not-a-request-context");

        httpAct.Should().Throw<InvalidOperationException>();
        grpcAct.Should().Throw<InvalidOperationException>();
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
