// -----------------------------------------------------------------------
// <copyright file="ServiceKeyEndpointFilterTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Gateway;

using D2.Shared.Handler;
using D2.Shared.RequestEnrichment.Default;
using D2.Shared.ServiceKey.Default;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

/// <summary>
/// Unit tests for the <see cref="ServiceKeyEndpointFilter"/>.
/// </summary>
public class ServiceKeyEndpointFilterTests
{
    /// <summary>
    /// When IsTrustedService is true, the request passes through.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WhenTrustedService_PassesThrough()
    {
        var filter = new ServiceKeyEndpointFilter();
        var context = CreateContext(isTrusted: true);
        var nextCalled = false;

        await filter.InvokeAsync(context, _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>("ok");
        });

        nextCalled.Should().BeTrue();
    }

    /// <summary>
    /// When IsTrustedService is false, returns 401.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WhenNotTrustedService_Returns401()
    {
        var filter = new ServiceKeyEndpointFilter();
        var context = CreateContext(isTrusted: false);

        var result = await filter.InvokeAsync(context, _ =>
            ValueTask.FromResult<object?>("should not reach"));

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// When IRequestContext is not present in features, returns 401.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WhenNoRequestInfo_Returns401()
    {
        var filter = new ServiceKeyEndpointFilter();
        var httpContext = new DefaultHttpContext();
        var context = new DefaultEndpointFilterInvocationContext(httpContext);

        var result = await filter.InvokeAsync(context, _ =>
            ValueTask.FromResult<object?>("should not reach"));

        result.Should().BeOfType<ProblemHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    #region Helpers

    private static EndpointFilterInvocationContext CreateContext(bool isTrusted)
    {
        var httpContext = new DefaultHttpContext();
        var requestContext = new MutableRequestContext
        {
            ClientIp = "192.0.2.1",
            ServerFingerprint = "abc123",
            DeviceFingerprint = "device-fp-test",
            IsTrustedService = isTrusted,
        };
        httpContext.Features.Set<IRequestContext>(requestContext);

        return new DefaultEndpointFilterInvocationContext(httpContext);
    }

    #endregion
}
