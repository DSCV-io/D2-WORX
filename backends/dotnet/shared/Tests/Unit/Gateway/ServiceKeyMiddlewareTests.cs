// -----------------------------------------------------------------------
// <copyright file="ServiceKeyMiddlewareTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Gateway;

using D2.Shared.Handler;
using D2.Shared.RequestEnrichment.Default;
using D2.Shared.ServiceKey.Default;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

/// <summary>
/// Unit tests for the <see cref="ServiceKeyMiddleware"/>.
/// </summary>
public class ServiceKeyMiddlewareTests
{
    private const string _HEADER = "X-Api-Key";
    private const string _VALID_KEY = "d2.sveltekit.service.key";

    private readonly Mock<ILogger<ServiceKeyMiddleware>> r_mockLogger = new();

    /// <summary>
    /// When no X-Api-Key header is present, passes through and IsTrustedService remains false.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithNoKey_PassesThroughAndRemainsUntrusted()
    {
        var context = CreateContext(apiKey: null);
        var requestContext = SetupRequestInfo(context);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        requestContext.IsTrustedService.Should().BeFalse();
    }

    /// <summary>
    /// When a valid key is present, sets IsTrustedService to true and continues pipeline.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithValidKey_SetsTrustedAndContinues()
    {
        var context = CreateContext(_VALID_KEY);
        var requestContext = SetupRequestInfo(context);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        requestContext.IsTrustedService.Should().BeTrue();
    }

    /// <summary>
    /// When an invalid key is present, returns 401 and does not call next middleware.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithInvalidKey_Returns401()
    {
        var context = CreateContext("wrong-key");
        context.Response.Body = new MemoryStream();
        SetupRequestInfo(context);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(401);
    }

    /// <summary>
    /// When IRequestContext is not in features, still continues pipeline without throwing.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithValidKeyButNoRequestInfo_ContinuesWithoutThrowing()
    {
        // Do NOT set up IRequestContext in features.
        var context = CreateContext(_VALID_KEY);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
    }

    /// <summary>
    /// When an empty key string is sent, treats it as "no key" and passes through.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithEmptyKey_PassesThroughAsNoKey()
    {
        var context = CreateContext(string.Empty);
        var requestContext = SetupRequestInfo(context);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        requestContext.IsTrustedService.Should().BeFalse();
    }

    /// <summary>
    /// When an invalid key is present, the response body contains the INVALID_SERVICE_KEY error code.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithInvalidKey_ReturnsD2ResultWithErrorCode()
    {
        var context = CreateContext("wrong-key");
        context.Response.Body = new MemoryStream();
        SetupRequestInfo(context);
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("INVALID_SERVICE_KEY");
    }

    #region Helpers

    private static DefaultHttpContext CreateContext(string? apiKey)
    {
        var context = new DefaultHttpContext();

        if (apiKey is not null)
        {
            context.Request.Headers[_HEADER] = apiKey;
        }

        return context;
    }

    private static MutableRequestContext SetupRequestInfo(DefaultHttpContext context)
    {
        var requestContext = new MutableRequestContext
        {
            ClientIp = "192.0.2.1",
            ServerFingerprint = "abc123",
            DeviceFingerprint = "device-fp-test",
        };
        context.Features.Set<IRequestContext>(requestContext);
        return requestContext;
    }

    private ServiceKeyMiddleware CreateMiddleware(
        RequestDelegate next,
        params string[] validKeys)
    {
        var keys = validKeys.Length > 0 ? validKeys : [_VALID_KEY];
        var options = Options.Create(new ServiceKeyOptions
        {
            ValidKeys = [.. keys],
        });

        return new ServiceKeyMiddleware(next, options, r_mockLogger.Object);
    }

    #endregion
}
