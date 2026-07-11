// -----------------------------------------------------------------------
// <copyright file="AuthHttpServiceCollectionExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Http;

using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Abstractions.Http;
using D2.Shared.Auth.Http;
using D2.Shared.Auth.Http.Ambient;
using D2.Shared.Caching;
using D2.Shared.Caching.Local.Default;
using D2.Shared.Context.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "ReSharper",
    "AccessToDisposedClosure",
    Justification = "Lambdas execute within the test method's using-scope; "
        + "the captured scope outlives the lambda's invocation.")]
public sealed class AuthHttpServiceCollectionExtensionsTests
{
    [Fact]
    public void AddD2AuthHttp_RegistersHttpContextAccessor()
    {
        var sp = BuildProvider();

        sp.GetService<IHttpContextAccessor>().Should().NotBeNull();
    }

    [Fact]
    public void AddD2AuthHttp_RegistersScopedRequestContext()
    {
        var sp = BuildProvider();
        using var scope = sp.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var ctx = new DefaultHttpContext();
        var requestContext = new MutableRequestContext { IsAuthenticated = true };
        ctx.Items[D2HttpContextItems.REQUEST_CONTEXT] = requestContext;
        accessor.HttpContext = ctx;

        var resolved = scope.ServiceProvider.GetRequiredService<IRequestContext>();

        resolved.Should().BeSameAs(requestContext);
    }

    [Fact]
    public void RequestContextResolution_BeforeMiddlewareRan_ReturnsUnestablishedMutable()
    {
        // Dual-path: missing Items slot falls through to scoped Mutable
        // (Unestablished — authority rules fail-closed). No throw-only path
        // that would also break System workers on the same host.
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
        // Regression: historical throw-only path broke multiproc System seed
        // workers that resolve IRequestContext outside any HttpContext.
        var sp = BuildProvider();
        using var scope = sp.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IRequestContext>();
        var mutable = scope.ServiceProvider.GetRequiredService<MutableRequestContext>();

        resolved.Should().BeSameAs(mutable);
        resolved.Origin.Should().Be(RequestOrigin.Unestablished);
    }

    [Fact]
    public void RequestContextResolution_SlotHoldsWrongType_FallsThroughToMutable()
    {
        var sp = BuildProvider();
        using var scope = sp.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var ctx = new DefaultHttpContext();
        ctx.Items[D2HttpContextItems.REQUEST_CONTEXT] = "not-a-request-context";
        accessor.HttpContext = ctx;

        var resolved = scope.ServiceProvider.GetRequiredService<IRequestContext>();
        var mutable = scope.ServiceProvider.GetRequiredService<MutableRequestContext>();

        resolved.Should().BeSameAs(mutable);
    }

    [Fact]
    public void AddD2AuthHttp_RegistersAmbientScopeAccessor_ResolvableAsHttpContextAdapter()
    {
        // Regression (M-2, §1.3): descriptor-presence ≠ resolvability.
        // GetRequiredService<> proves the full DI graph resolves without throwing.
        var sp = BuildProvider();

        var resolved = sp.GetRequiredService<IAmbientRequestScopeAccessor>();

        resolved.Should().BeOfType<HttpContextAmbientRequestScopeAccessor>();
    }

    [Fact]
    public void AddD2AuthHttp_AmbientScopeAccessor_IsSingleton()
    {
        // Singleton invariant: two resolves from the ROOT provider must return
        // the same instance (stateless adapter — per-request state flows through
        // IHttpContextAccessor's AsyncLocal, not through the adapter itself).
        var sp = BuildProvider();

        var first = sp.GetRequiredService<IAmbientRequestScopeAccessor>();
        var second = sp.GetRequiredService<IAmbientRequestScopeAccessor>();

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void AddD2AuthHttp_WithoutAddD2Auth_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        var act = () => services.AddD2AuthHttp();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AddD2Auth*");
    }

    [Fact]
    public void AddD2AuthHttp_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2AuthHttp();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2AuthHttp_CalledTwice_IsIdempotent()
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
        services.AddD2AuthHttp();
        services.AddD2AuthHttp();

        var sp = services.BuildServiceProvider();

        sp.GetService<IHttpContextAccessor>().Should().NotBeNull();
    }

    private static ServiceProvider BuildProvider()
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
        services.AddD2AuthHttp();
        return services.BuildServiceProvider();
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
