// -----------------------------------------------------------------------
// <copyright file="GrpcHttpContextAmbientRequestScopeAccessorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Ambient;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Grpc.Ambient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Coverage for <see cref="GrpcHttpContextAmbientRequestScopeAccessor"/> — the
/// gRPC-inbound sibling of the HTTP adapter, an
/// <see cref="IHttpContextAccessor"/>-backed adapter for the framework-free
/// <see cref="IAmbientRequestScopeAccessor"/> port. Asserts it surfaces the current
/// call's <see cref="HttpContext.RequestServices"/> when a context is on the ambient
/// accessor (under gRPC the per-call gRPC <see cref="HttpContext"/>), and
/// <see langword="null"/> when none is (the system-initiated / no-inbound-call case
/// the forwarding credential hard-fails on). Mirrors
/// <c>HttpContextAmbientRequestScopeAccessorTests</c> branch-for-branch — the gRPC
/// adapter is a distinct type and gets its own direct unit coverage.
/// </summary>
[Trait("Category", "Unit")]
public sealed class GrpcHttpContextAmbientRequestScopeAccessorTests
{
    [Fact]
    public void Current_ReturnsRequestServices_WhenHttpContextPresent()
    {
        var requestServices = new ServiceCollection().BuildServiceProvider();
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { RequestServices = requestServices },
        };
        var accessor = new GrpcHttpContextAmbientRequestScopeAccessor(httpContextAccessor);

        accessor.Current.Should().BeSameAs(requestServices);
    }

    [Fact]
    public void Current_ReturnsNull_WhenNoHttpContext()
    {
        var httpContextAccessor = new HttpContextAccessor { HttpContext = null };
        var accessor = new GrpcHttpContextAmbientRequestScopeAccessor(httpContextAccessor);

        accessor.Current.Should().BeNull();
    }

    [Fact]
    public void Current_ReflectsAmbientSwap_OnSharedAdapter()
    {
        // One adapter instance (singleton, as registered). Swapping the ambient
        // HttpContext is exactly what AsyncLocal does per concurrent call — the
        // adapter must observe the swap, not a cached value. This is the
        // concurrency property the forwarding credential relies on: two concurrent
        // gRPC calls on a shared outbound channel each read their own scope.
        var scopeA = new ServiceCollection().BuildServiceProvider();
        var scopeB = new ServiceCollection().BuildServiceProvider();
        var httpContextAccessor = new HttpContextAccessor();
        var accessor = new GrpcHttpContextAmbientRequestScopeAccessor(httpContextAccessor);

        httpContextAccessor.HttpContext = new DefaultHttpContext { RequestServices = scopeA };
        accessor.Current.Should().BeSameAs(scopeA);

        httpContextAccessor.HttpContext = new DefaultHttpContext { RequestServices = scopeB };
        accessor.Current.Should().BeSameAs(scopeB);

        httpContextAccessor.HttpContext = null;
        accessor.Current.Should().BeNull();
    }

    [Fact]
    public void NullAccessor_ConstructsButThrowsOnFirstRead()
    {
        // Parity decision pinned: the adapter mirrors the HTTP sibling exactly,
        // which takes a primary-constructor IHttpContextAccessor with NO explicit
        // null-guard. DI never passes null (the accessor is always registered via
        // AddHttpContextAccessor()), and a null would surface immediately on the
        // first .Current read. Construction therefore does NOT throw; the first read
        // NREs. This test documents that the gRPC sibling does not diverge from the
        // HTTP sibling's guard posture.
        var accessor = new GrpcHttpContextAmbientRequestScopeAccessor(null!);

        var act = () => accessor.Current;

        act.Should().Throw<NullReferenceException>();
    }
}
