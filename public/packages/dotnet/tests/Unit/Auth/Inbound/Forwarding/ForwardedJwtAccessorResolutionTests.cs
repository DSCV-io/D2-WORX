// -----------------------------------------------------------------------
// <copyright file="ForwardedJwtAccessorResolutionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Forwarding;

using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Grpc;
using D2.Shared.Auth.Http;
using D2.Shared.Caching;
using D2.Shared.Caching.Local.Default;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// DI-resolution + scoped-lifetime + cross-transport parity tests for the
/// request-scoped <see cref="IForwardedJwtAccessor"/> holder registered by
/// <c>AddD2AuthHttp()</c> and <c>AddD2AuthGrpc()</c>. Resolving (not just
/// asserting descriptor presence) every registered seam is the contract — a
/// registration that does not resolve is a latent first-request failure.
/// </summary>
public sealed class ForwardedJwtAccessorResolutionTests
{
    [Fact]
    public void AddD2AuthHttp_RegistersResolvableForwardedJwtAccessor()
    {
        using var sp = BuildProvider(c => c.AddD2AuthHttp());

        using var scope = sp.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>();

        accessor.Should().NotBeNull();
        accessor.Should().BeOfType<MutableForwardedJwtAccessor>();
        accessor.Current.Should().BeNull();
    }

    [Fact]
    public void AddD2AuthGrpc_RegistersResolvableForwardedJwtAccessor()
    {
        using var sp = BuildProvider(c => c.AddD2AuthGrpc());

        using var scope = sp.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>();

        accessor.Should().NotBeNull();
        accessor.Should().BeOfType<MutableForwardedJwtAccessor>();
        accessor.Current.Should().BeNull();
    }

    [Fact]
    public void Http_ForwardedJwtAccessor_IsScoped_DistinctInstancesPerScope()
    {
        using var sp = BuildProvider(c => c.AddD2AuthHttp());

        IForwardedJwtAccessor first;
        IForwardedJwtAccessor second;
        using (var scope1 = sp.CreateScope())
        {
            first = scope1.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>();
        }

        using (var scope2 = sp.CreateScope())
        {
            second = scope2.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>();
        }

        first.Should().NotBeSameAs(second);
    }

    [Fact]
    public void Grpc_ForwardedJwtAccessor_IsScoped_DistinctInstancesPerScope()
    {
        using var sp = BuildProvider(c => c.AddD2AuthGrpc());

        IForwardedJwtAccessor first;
        IForwardedJwtAccessor second;
        using (var scope1 = sp.CreateScope())
        {
            first = scope1.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>();
        }

        using (var scope2 = sp.CreateScope())
        {
            second = scope2.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>();
        }

        first.Should().NotBeSameAs(second);
    }

    [Fact]
    public void ForwardedJwtAccessor_SameWithinOneScope()
    {
        using var sp = BuildProvider(c => c.AddD2AuthHttp());
        using var scope = sp.CreateScope();

        var a = scope.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>();
        var b = scope.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>();

        a.Should().BeSameAs(b);
    }

    [Fact]
    public void CrossRequestBleed_FreshScopeStartsNull_AfterPriorScopeCaptured()
    {
        // The real isolation guarantee: a token captured in one request scope
        // does NOT bleed into the next. Capture in scope 1, assert scope 2's
        // freshly-resolved holder sees nothing.
        using var sp = BuildProvider(c => c.AddD2AuthHttp());

        using (var scope1 = sp.CreateScope())
        {
            var holder1 = scope1.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>();
            holder1.Capture("header.payload.signature");
            holder1.Current.Should().NotBeNull();
        }

        using var scope2 = sp.CreateScope();
        var holder2 = scope2.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>();
        holder2.Current.Should().BeNull();
    }

    [Fact]
    public void HttpAndGrpc_RegisterSameHolderImplType_Parity()
    {
        // Mirrors RequestContextResolverParityTests: both transport extensions
        // must register the SAME holder impl so a dual-transport host gets one
        // consistent forwarded-JWT holder under either transport.
        using var httpProvider = BuildProvider(c => c.AddD2AuthHttp());
        using var grpcProvider = BuildProvider(c => c.AddD2AuthGrpc());

        using var httpScope = httpProvider.CreateScope();
        using var grpcScope = grpcProvider.CreateScope();
        var httpAccessor = httpScope.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>();
        var grpcAccessor = grpcScope.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>();

        httpAccessor.GetType().Should().Be(grpcAccessor.GetType());
        httpAccessor.Should().BeOfType<MutableForwardedJwtAccessor>();
    }

    [Fact]
    public void DualTransport_BothExtensions_ResolveSingleHolderType()
    {
        // A host that wires BOTH transports (HTTP endpoints + gRPC on one
        // Kestrel host) — TryAddScoped first-wins is harmless because both
        // register the identical impl.
        using var sp = BuildProvider(c =>
        {
            c.AddD2AuthHttp();
            c.AddD2AuthGrpc();
        });

        using var scope = sp.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IForwardedJwtAccessor>();

        accessor.Should().BeOfType<MutableForwardedJwtAccessor>();
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> register)
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
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Minimal in-memory <see cref="ITieredCache"/> stub — required to satisfy
    /// the session-liveness tracker's constructor at composition. Mirrors the
    /// stub in <c>RequestContextResolverParityTests</c>.
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
