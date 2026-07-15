// -----------------------------------------------------------------------
// <copyright file="HttpContextAmbientRequestScopeAccessorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Http.Ambient;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Http.Ambient;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Coverage for <see cref="HttpContextAmbientRequestScopeAccessor"/> — the
/// <see cref="IHttpContextAccessor"/>-backed adapter for the framework-free
/// <see cref="IAmbientRequestScopeAccessor"/> port. Asserts it surfaces the
/// current request's <see cref="HttpContext.RequestServices"/> when a context is
/// on the ambient accessor, and <see langword="null"/> when none is (the
/// system-initiated / no-inbound-request case the forwarding credential hard-fails
/// on).
/// </summary>
[Trait("Category", "Unit")]
public sealed class HttpContextAmbientRequestScopeAccessorTests
{
    [Fact]
    public void Current_ReturnsRequestServices_WhenHttpContextPresent()
    {
        var requestServices = new ServiceCollection().BuildServiceProvider();
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { RequestServices = requestServices },
        };
        var accessor = new HttpContextAmbientRequestScopeAccessor(httpContextAccessor);

        accessor.Current.Should().BeSameAs(requestServices);
    }

    [Fact]
    public void Current_ReturnsNull_WhenNoHttpContext()
    {
        var httpContextAccessor = new HttpContextAccessor { HttpContext = null };
        var accessor = new HttpContextAmbientRequestScopeAccessor(httpContextAccessor);

        accessor.Current.Should().BeNull();
    }

    [Fact]
    public void Current_ReflectsAmbientSwap_OnSharedAdapter()
    {
        // One adapter instance (singleton, as registered). Swapping the ambient
        // HttpContext is exactly what AsyncLocal does per concurrent request — the
        // adapter must observe the swap, not a cached value.
        var scopeA = new ServiceCollection().BuildServiceProvider();
        var scopeB = new ServiceCollection().BuildServiceProvider();
        var httpContextAccessor = new HttpContextAccessor();
        var accessor = new HttpContextAmbientRequestScopeAccessor(httpContextAccessor);

        httpContextAccessor.HttpContext = new DefaultHttpContext { RequestServices = scopeA };
        accessor.Current.Should().BeSameAs(scopeA);

        httpContextAccessor.HttpContext = new DefaultHttpContext { RequestServices = scopeB };
        accessor.Current.Should().BeSameAs(scopeB);

        httpContextAccessor.HttpContext = null;
        accessor.Current.Should().BeNull();
    }
}
