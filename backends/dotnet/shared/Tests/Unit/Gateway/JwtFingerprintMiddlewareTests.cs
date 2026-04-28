// -----------------------------------------------------------------------
// <copyright file="JwtFingerprintMiddlewareTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Gateway;

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using D2.Shared.Handler;
using D2.Shared.JwtAuth.Default;
using D2.Shared.RequestEnrichment.Default;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// Unit tests for the <see cref="JwtFingerprintMiddleware"/>.
/// </summary>
public class JwtFingerprintMiddlewareTests
{
    private readonly Mock<ILogger<JwtFingerprintMiddleware>> r_mockLogger = new();

    /// <summary>
    /// Tests that a matching fingerprint allows the request through.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithMatchingFingerprint_PassesThrough()
    {
        // Arrange
        const string user_agent = "Mozilla/5.0";
        const string accept = "text/html";
        var fingerprint = ComputeExpectedFingerprint(user_agent, accept);

        var context = CreateAuthenticatedContext(user_agent, accept, fingerprint);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    /// <summary>
    /// Tests that a mismatched fingerprint returns 401.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithMismatchedFingerprint_Returns401()
    {
        // Arrange
        var context = CreateAuthenticatedContext("Chrome/120", "text/html", "wrong-fingerprint-hash");
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(401);
    }

    /// <summary>
    /// Tests that missing fp claim returns 401 for non-trusted requests (fingerprint is required).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithNoFpClaim_Returns401()
    {
        // Arrange — authenticated but no fp claim, not a trusted service.
        var context = CreateAuthenticatedContext("Chrome/120", "text/html", fpClaim: null);
        context.Response.Body = new MemoryStream();
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(401);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("MISSING_FINGERPRINT");
    }

    /// <summary>
    /// Tests that unauthenticated requests pass through (auth middleware handles them).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithNoAuthentication_PassesThrough()
    {
        // Arrange — no authenticated identity.
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    /// <summary>
    /// Tests that fingerprint comparison is case-insensitive.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_FingerprintComparisonIsCaseInsensitive()
    {
        // Arrange
        const string user_agent = "Mozilla/5.0";
        const string accept = "text/html";
        var fingerprint = ComputeExpectedFingerprint(user_agent, accept).ToUpperInvariant();

        var context = CreateAuthenticatedContext(user_agent, accept, fingerprint);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert — uppercase fp claim should still match lowercase computed.
        nextCalled.Should().BeTrue();
    }

    /// <summary>
    /// Tests that the 401 response body contains a D2Result error structure.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_OnMismatch_ReturnsD2ResultErrorBody()
    {
        // Arrange
        var context = CreateAuthenticatedContext("Chrome/120", "text/html", "wrong-hash");
        context.Response.Body = new MemoryStream();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(401);
        context.Response.ContentType.Should().Contain("application/json");
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        body.Should().Contain("JWT_FINGERPRINT_MISMATCH");
    }

    /// <summary>
    /// Tests that empty fp claim returns 401 for non-trusted requests (fingerprint is required).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithEmptyFpClaim_Returns401()
    {
        // Arrange
        var context = CreateAuthenticatedContext("Chrome/120", "text/html", fpClaim: string.Empty);
        context.Response.Body = new MemoryStream();
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(401);
    }

    /// <summary>
    /// Tests that a short fp claim (less than 8 chars) does not crash the middleware.
    /// The middleware should still return 401 on mismatch without IndexOutOfRangeException.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithShortFpClaim_DoesNotCrash()
    {
        // Arrange — fp claim is only 3 chars, which would crash with [..8] slicing.
        var context = CreateAuthenticatedContext("Chrome/120", "text/html", "abc");
        context.Response.Body = new MemoryStream();
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert — should return 401 without throwing.
        nextCalled.Should().BeFalse();
        context.Response.StatusCode.Should().Be(401);
    }

    /// <summary>
    /// Tests that trusted services skip fingerprint validation entirely, even without fp claim.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithNoFpClaim_TrustedService_PassesThrough()
    {
        // Arrange — trusted service, authenticated, no fp claim.
        var context = CreateAuthenticatedContext("Chrome/120", "text/html", fpClaim: null);
        SetTrustedService(context);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert — trusted services bypass fingerprint check.
        nextCalled.Should().BeTrue();
    }

    /// <summary>
    /// Tests that trusted services skip fingerprint validation even when fp claim is present.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithFpClaim_TrustedService_SkipsFingerprintCheck()
    {
        // Arrange — trusted service with a MISMATCHED fp claim (should still pass).
        var context = CreateAuthenticatedContext("Chrome/120", "text/html", "wrong-fingerprint");
        SetTrustedService(context);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert — trusted services skip fingerprint entirely, even with mismatched claim.
        nextCalled.Should().BeTrue();
    }

    #region Auth State Tests

    /// <summary>
    /// Tests that matching fingerprint sets IsAuthenticated and UserId on requestContext.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithMatchingFingerprint_SetsAuthStateOnRequestInfo()
    {
        // Arrange
        const string user_agent = "Mozilla/5.0";
        const string accept = "text/html";
        const string user_id = "user-abc-123";
        var fingerprint = ComputeExpectedFingerprint(user_agent, accept);

        var context = CreateAuthenticatedContext(user_agent, accept, fingerprint, user_id);
        var requestContext = SetRequestInfo(context);
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        requestContext.IsAuthenticated.Should().BeTrue();
        requestContext.UserIdRaw.Should().Be(user_id);
    }

    /// <summary>
    /// Tests that trusted service sets IsAuthenticated and UserId on requestContext.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_TrustedService_SetsAuthStateOnRequestInfo()
    {
        // Arrange
        const string user_id = "user-trusted-456";
        var context = CreateAuthenticatedContext("Chrome/120", "text/html", fpClaim: null, user_id);
        SetTrustedService(context);
        var requestContext = (MutableRequestContext)context.Features.Get<IRequestContext>()!;
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        requestContext.IsAuthenticated.Should().BeTrue();
        requestContext.UserIdRaw.Should().Be(user_id);
    }

    /// <summary>
    /// Tests that unauthenticated requests do not modify requestContext auth state.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_Unauthenticated_DoesNotSetAuthState()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var requestContext = SetRequestInfo(context);
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert — IsAuthenticated stays null (unknown) because the middleware
        // doesn't set auth state for unauthenticated requests.
        requestContext.IsAuthenticated.Should().BeNull();
        requestContext.UserIdRaw.Should().BeNull();
    }

    /// <summary>
    /// Tests that fingerprint mismatch does not set auth state (request rejected with 401).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_FingerprintMismatch_DoesNotSetAuthState()
    {
        // Arrange
        var context = CreateAuthenticatedContext("Chrome/120", "text/html", "wrong-hash", "user-789");
        context.Response.Body = new MemoryStream();
        var requestContext = SetRequestInfo(context);
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert — IsAuthenticated stays null (unknown) because the middleware
        // rejects with 401 before reaching SetAuthState.
        requestContext.IsAuthenticated.Should().BeNull();
        requestContext.UserIdRaw.Should().BeNull();
    }

    /// <summary>
    /// Tests that missing fp claim does not set auth state (request rejected with 401).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_MissingFpClaim_DoesNotSetAuthState()
    {
        // Arrange
        var context = CreateAuthenticatedContext("Chrome/120", "text/html", fpClaim: null, "user-no-fp");
        context.Response.Body = new MemoryStream();
        var requestContext = SetRequestInfo(context);
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert — IsAuthenticated stays null (unknown) because the middleware
        // rejects with 401 before reaching SetAuthState.
        requestContext.IsAuthenticated.Should().BeNull();
        requestContext.UserIdRaw.Should().BeNull();
    }

    /// <summary>
    /// Tests that auth state is set even when requestContext feature is not present (no crash).
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test.</returns>
    [Fact]
    public async Task InvokeAsync_WithNoRequestInfoFeature_DoesNotCrash()
    {
        // Arrange — authenticated + matching fingerprint, but no IRequestContext on features.
        const string user_agent = "Mozilla/5.0";
        const string accept = "text/html";
        var fingerprint = ComputeExpectedFingerprint(user_agent, accept);
        var context = CreateAuthenticatedContext(user_agent, accept, fingerprint);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert — should pass through without throwing.
        nextCalled.Should().BeTrue();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates an HttpContext with authenticated user and optional fp claim.
    /// </summary>
    private static DefaultHttpContext CreateAuthenticatedContext(
        string userAgent,
        string accept,
        string? fpClaim,
        string? userId = null)
    {
        var subValue = userId ?? Guid.NewGuid().ToString();
        var claims = new List<Claim> { new("sub", subValue) };
        if (fpClaim is not null)
        {
            claims.Add(new Claim("fp", fpClaim));
        }

        var identity = new ClaimsIdentity(claims, "Bearer");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };
        context.Request.Headers.UserAgent = userAgent;
        context.Request.Headers.Accept = accept;

        return context;
    }

    /// <summary>
    /// Sets the IsTrustedService flag on the HttpContext features.
    /// </summary>
    private static void SetTrustedService(DefaultHttpContext context)
    {
        var requestContext = new MutableRequestContext
        {
            ClientIp = "10.0.0.1",
            ServerFingerprint = "abc123",
            DeviceFingerprint = "device-fp-trusted",
            IsTrustedService = true,
        };
        context.Features.Set<IRequestContext>(requestContext);
    }

    /// <summary>
    /// Computes the expected fingerprint using the same formula as the validator.
    /// </summary>
    private static string ComputeExpectedFingerprint(string userAgent, string accept)
    {
        var input = $"{userAgent}|{accept}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Adds a non-trusted <see cref="IRequestContext"/> to the context features and returns it.
    /// </summary>
    private static MutableRequestContext SetRequestInfo(DefaultHttpContext context)
    {
        var requestContext = new MutableRequestContext
        {
            ClientIp = "10.0.0.1",
            ServerFingerprint = "test-fingerprint",
            DeviceFingerprint = "device-fp-test",
        };
        context.Features.Set<IRequestContext>(requestContext);
        return requestContext;
    }

    /// <summary>
    /// Creates a <see cref="JwtFingerprintMiddleware"/> with the given next delegate.
    /// </summary>
    private JwtFingerprintMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new JwtFingerprintMiddleware(next, r_mockLogger.Object);
    }

    #endregion
}
