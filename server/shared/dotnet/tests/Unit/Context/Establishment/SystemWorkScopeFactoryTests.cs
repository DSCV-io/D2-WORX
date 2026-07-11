// -----------------------------------------------------------------------
// <copyright file="SystemWorkScopeFactoryTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Context.Establishment;

using System;
using System.Threading;
using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Http;
using D2.Shared.Caching;
using D2.Shared.Caching.Local.Default;
using D2.Shared.Context.Abstractions;
using D2.Shared.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;
using Xunit;
using IClock = D2.Shared.Time.IClock;
using TestClock = D2.Shared.Time.TestClock;

/// <summary>
/// Unit matrix for the platform System work plane:
/// <see cref="ISystemWorkScopeFactory"/> / <c>AddD2SystemWorkPlane</c> / dual-path auth.
/// </summary>
/// <remarks>
/// <para>
/// <b>§1.22 adversarial matrix (D7 surfaces)</b> — Surface × Category × Test:
/// </para>
/// <list type="table">
/// <listheader>
/// <term>Surface</term>
/// <description>Category → test</description>
/// </listheader>
/// <item>
/// <term><c>ISystemWorkScopeFactory.BeginAsync</c></term>
/// <description>
/// Happy → <see cref="BeginAsync_EstablishesSystemOriginAndHostIdentity"/>,
/// <see cref="BeginAsync_ResolvesIRequestContext_SameAsMutable"/>;
/// blank host id → <see cref="BeginAsync_BlankHostServiceId_Throws"/>;
/// cancel → <see cref="BeginAsync_CanceledToken_Throws"/>;
/// double dispose → <see cref="BeginAsync_DisposeAsync_Twice_DoesNotThrow"/>;
/// concurrent scopes → <see cref="BeginAsync_ConcurrentScopes_AreIndependent"/>;
/// establish-fail dispose → <see cref="BeginAsync_EstablishFailure_DisposesOpenedScope"/>.
/// </description>
/// </item>
/// <item>
/// <term>Dual-path (production <c>AddD2AuthHttp</c>)</term>
/// <description>
/// Fall-through / composition →
/// <see cref="BeginAsync_AfterRealAddD2AuthHttp_EstablishesSystemOrigin"/>
/// (no hand-copied dual-path lambda).
/// </description>
/// </item>
/// <item>
/// <term><c>AddD2SystemWorkPlane</c></term>
/// <description>
/// Null services → <see cref="AddD2SystemWorkPlane_NullServices_Throws"/>;
/// idempotent → <see cref="AddD2SystemWorkPlane_IsIdempotent"/>.
/// </description>
/// </item>
/// </list>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class SystemWorkScopeFactoryTests
{
    private static readonly Instant sr_now = Instant.FromUtc(2026, 7, 11, 12, 0, 0);

    [Fact]
    public async Task BeginAsync_EstablishesSystemOriginAndHostIdentity()
    {
        await using var provider = BuildProvider(serviceId: "edge");
        var factory = provider.GetRequiredService<ISystemWorkScopeFactory>();

        await using var work = await factory.BeginAsync();

        var ctx = work.Services.GetRequiredService<IRequestContext>();
        ctx.Origin.Should().Be(RequestOrigin.System);
        ctx.ImmediateCaller.Should().Be("edge");
        ctx.CallPath.Should().ContainSingle();
        ctx.CallPath[0].Id.Should().Be("edge");
        ctx.CallPath[0].Kind.Should().Be(CallPathKind.System);
        ctx.CallPath[0].Timestamp.Should().Be(sr_now.ToDateTimeOffset());
    }

    [Fact]
    public async Task BeginAsync_ResolvesIRequestContext_SameAsMutable()
    {
        await using var provider = BuildProvider(serviceId: "edge");
        var factory = provider.GetRequiredService<ISystemWorkScopeFactory>();

        await using var work = await factory.BeginAsync();

        var mutable = work.Services.GetRequiredService<MutableRequestContext>();
        var ctx = work.Services.GetRequiredService<IRequestContext>();
        ctx.Should().BeSameAs(mutable);
    }

    [Fact]
    public async Task BeginAsync_BlankHostServiceId_Throws()
    {
        await using var provider = BuildProvider(serviceId: "   ");
        var factory = provider.GetRequiredService<ISystemWorkScopeFactory>();

        var act = async () =>
        {
            await using var work = await factory.BeginAsync();
        };

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task BeginAsync_CanceledToken_Throws()
    {
        await using var provider = BuildProvider(serviceId: "edge");
        var factory = provider.GetRequiredService<ISystemWorkScopeFactory>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Capture token (struct) — not the disposable CTS — for the assertion lambda.
        var canceled = cts.Token;
        var act = async () =>
        {
            await using var work = await factory.BeginAsync(canceled);
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task BeginAsync_DisposeAsync_Twice_DoesNotThrow()
    {
        await using var provider = BuildProvider(serviceId: "edge");
        var factory = provider.GetRequiredService<ISystemWorkScopeFactory>();

        var work = await factory.BeginAsync();

        await work.DisposeAsync();

        var act = async () => await work.DisposeAsync();

        await act.Should().NotThrowAsync(
            because: "ISystemWorkScope must be safe for double DisposeAsync");
    }

    [Fact]
    public async Task BeginAsync_ConcurrentScopes_AreIndependent()
    {
        await using var provider = BuildProvider(serviceId: "edge");
        var factory = provider.GetRequiredService<ISystemWorkScopeFactory>();

        var work1 = await factory.BeginAsync();
        var work2 = await factory.BeginAsync();

        await using (work1)
        await using (work2)
        {
            var ctx1 = work1.Services.GetRequiredService<MutableRequestContext>();
            var ctx2 = work2.Services.GetRequiredService<MutableRequestContext>();

            ctx1.Should().NotBeSameAs(
                ctx2,
                because: "each BeginAsync must open an independent DI scope");
            ctx1.Origin.Should().Be(RequestOrigin.System);
            ctx2.Origin.Should().Be(RequestOrigin.System);

            // Mutating one scope must not bleed into the other.
            ctx1.ImmediateCaller = "mutated-scope-1";
            ctx2.ImmediateCaller.Should().Be("edge");
        }
    }

    [Fact]
    public async Task BeginAsync_EstablishFailure_DisposesOpenedScope()
    {
        // Force EstablishSystemContext to throw after CreateAsyncScope by
        // omitting MutableRequestContext. A tracking scope factory proves the
        // factory's catch path disposes the opened scope before rethrow.
        using var tracking = new DisposeTrackingScopeFactory();
        var options = Options.Create(new D2WorkloadIdentityOptions { ServiceId = "edge" });
        ISystemWorkScopeFactory factory = new SystemWorkScopeFactory(
            tracking,
            options,
            new TestClock(sr_now));

        var act = async () =>
        {
            await using var work = await factory.BeginAsync();
        };

        await act.Should().ThrowAsync<InvalidOperationException>();
        tracking.DisposedCount.Should().Be(
            1,
            because: "BeginAsync must dispose the opened scope when establishment fails");
        tracking.CreatedCount.Should().Be(1);
    }

    [Fact]
    public async Task BeginAsync_AfterRealAddD2AuthHttp_EstablishesSystemOrigin()
    {
        // Composition regression (FR-R4-A-1): production dual-path from real
        // AddD2Auth + AddD2AuthHttp replaces the plane's plain Mutable default;
        // BeginAsync must still establish Origin=System with no HttpContext and
        // never hit a throw-only resolver. Not descriptor-only; not a hand-copied
        // dual-path lambda.
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<D2WorkloadIdentityOptions>(o => o.ServiceId = "edge");
        services.AddSingleton<IClock>(new TestClock(sr_now));
        services.AddD2SystemWorkPlane();
        services.AddD2LocalCache();
        services.AddSingleton<ITieredCache, FakeTieredCacheStub>();
        services.AddD2Auth(opts =>
        {
            opts.Issuer = new Uri("https://edge.internal");
            opts.Audience = "files";
        });
        services.AddD2AuthHttp();

        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ISystemWorkScopeFactory>();

        await using var work = await factory.BeginAsync();
        var ctx = work.Services.GetRequiredService<IRequestContext>();

        ctx.Origin.Should().Be(RequestOrigin.System);
        ctx.ImmediateCaller.Should().Be("edge");
        ctx.Should().BeOfType<MutableRequestContext>(
            because: "no HttpContext.Items slot → dual-path falls through to scoped Mutable");
    }

    [Fact]
    public void AddD2SystemWorkPlane_NullServices_Throws()
    {
        IServiceCollection? services = null;

        var act = () => services!.AddD2SystemWorkPlane();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddD2SystemWorkPlane_IsIdempotent()
    {
        var services = new ServiceCollection();
        services.Configure<D2WorkloadIdentityOptions>(o => o.ServiceId = "edge");
        services.AddD2SystemWorkPlane();
        services.AddD2SystemWorkPlane();

        services.Count(d => d.ServiceType == typeof(ISystemWorkScopeFactory))
            .Should().Be(1);
        services.Count(d => d.ServiceType == typeof(MutableRequestContext))
            .Should().Be(1);
        services.Count(d => d.ServiceType == typeof(IRequestContext))
            .Should().Be(1);
    }

    private static ServiceProvider BuildProvider(string serviceId)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<D2WorkloadIdentityOptions>(o => o.ServiceId = serviceId);
        services.AddSingleton<IClock>(new TestClock(sr_now));
        services.AddD2SystemWorkPlane();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Tracks CreateScope + Dispose so factory catch-path disposal is observable.
    /// Empty root provider (no MutableRequestContext) forces EstablishSystemContext
    /// to throw after the scope is opened.
    /// </summary>
    private sealed class DisposeTrackingScopeFactory : IServiceScopeFactory, IDisposable
    {
        private readonly ServiceProvider r_root =
            new ServiceCollection().BuildServiceProvider();

        private int _createdCount;
        private int _disposedCount;

        public int CreatedCount => _createdCount;

        public int DisposedCount => _disposedCount;

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _createdCount);
            return new TrackingScope(r_root.CreateScope(), OnDisposed);
        }

        public void Dispose() => r_root.Dispose();

        private void OnDisposed() => Interlocked.Increment(ref _disposedCount);

        private sealed class TrackingScope(IServiceScope inner, Action onDisposed)
            : IServiceScope, IAsyncDisposable
        {
            private int _disposed;

            public IServiceProvider ServiceProvider => inner.ServiceProvider;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    return;

                inner.Dispose();
                onDisposed();
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    /// <summary>
    /// Minimal in-memory <see cref="ITieredCache"/> stub — satisfies
    /// <c>TieredCacheSessionLivenessTracker</c> at composition (not invoked).
    /// </summary>
    private sealed class FakeTieredCacheStub : ITieredCache
    {
        public ValueTask<D2Result<bool>> ExistsAsync(
            string key, CancellationToken ct = default)
            => new(D2Result<bool>.Ok());

        public ValueTask<D2Result<T?>> GetAsync<T>(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<IReadOnlyDictionary<string, T?>>> GetManyAsync<T>(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetAsync<T>(
            string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetManyAsync<T>(
            IReadOnlyDictionary<string, T> entries,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveManyAsync(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<TimeSpan?>> GetTtlAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<long>> IncrementAsync(
            string key,
            long delta = 1,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<bool>> SetNxAsync<T>(
            string key,
            T value,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result<bool>> AcquireLockAsync(
            string key, string token, TimeSpan ttl, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> ReleaseLockAsync(
            string key, string token, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetAndBroadcastAsync<T>(
            string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> SetManyAndBroadcastAsync<T>(
            IReadOnlyDictionary<string, T> entries,
            TimeSpan? ttl = null,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveAndBroadcastAsync(
            string key, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<D2Result> RemoveManyAndBroadcastAsync(
            IReadOnlyCollection<string> keys, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
