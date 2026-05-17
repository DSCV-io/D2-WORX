// -----------------------------------------------------------------------
// <copyright file="InstanceShapeConsistencyTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AspNetCore;

using System.Net;
using AwesomeAssertions;
using D2.Shared.AspNetCore;
using D2.Shared.AspNetCore.Internal;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Http.ProblemDetails;
using D2.Shared.Result;
using Microsoft.AspNetCore.Http;
using Xunit;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>
/// Cross-path regression: path A
/// (<see cref="D2ProblemDetailsExtensions.ToProblemDetails"/> in
/// <c>D2.Shared.Auth.Http</c>) and path B
/// (<see cref="D2ProblemDetailsCustomizer"/> in
/// <c>D2.Shared.AspNetCore</c>) MUST emit the same <c>instance</c> shape
/// (<c>"{Method} {Path}"</c>) for the same request. Otherwise operators
/// querying logs by <c>instance</c> get two shapes depending on which
/// failure path fired.
/// </summary>
public sealed class InstanceShapeConsistencyTests
{
    [Theory]
    [InlineData("GET", "/api/files/abc")]
    [InlineData("POST", "/api/auth/login")]
    [InlineData("DELETE", "/api/notes/xyz")]
    public void PathA_PathB_EmitIdenticalInstanceShape(string method, string path)
    {
        var pathAContext = new DefaultHttpContext();
        pathAContext.Request.Method = method;
        pathAContext.Request.Path = path;

        // Path A: D2ProblemDetailsExtensions.ToProblemDetails (auth-http).
        var failure = AuthFailures.BearerMissing();
        var pathABody = failure.ToProblemDetails(pathAContext);

        // Path B: D2ProblemDetailsCustomizer.Apply (aspnetcore).
        var pathBContext = new DefaultHttpContext();
        pathBContext.Request.Method = method;
        pathBContext.Request.Path = path;
        var customizerCtx = new ProblemDetailsContext
        {
            HttpContext = pathBContext,
            ProblemDetails = new MvcProblemDetails(),
        };
        pathBContext.SetD2Result(failure);
        D2ProblemDetailsCustomizer.Apply(customizerCtx, new D2ProblemDetailsOptions());

        pathABody.Instance.Should().Be(customizerCtx.ProblemDetails.Instance);
        pathABody.Instance.Should().Be($"{method} {path}");
    }

    [Fact]
    public void PathA_PathB_EmitIdenticalTypeUri()
    {
        var failure = D2Result.Fail(
            messages: [D2.Shared.I18n.TK.Auth.Errors.UNAUTHORIZED],
            errorCode: AuthErrorCodes.AUTH_BEARER_MISSING,
            statusCode: HttpStatusCode.Unauthorized);

        var pathAContext = new DefaultHttpContext();
        pathAContext.Request.Method = "GET";
        pathAContext.Request.Path = "/x";
        var pathABody = failure.ToProblemDetails(pathAContext);

        var pathBContext = new DefaultHttpContext();
        pathBContext.Request.Method = "GET";
        pathBContext.Request.Path = "/x";
        var customizerCtx = new ProblemDetailsContext
        {
            HttpContext = pathBContext,
            ProblemDetails = new MvcProblemDetails(),
        };
        pathBContext.SetD2Result(failure);
        D2ProblemDetailsCustomizer.Apply(customizerCtx, new D2ProblemDetailsOptions());

        pathABody.Type.Should().Be(customizerCtx.ProblemDetails.Type);
    }

    [Fact]
    public void PathA_PathB_EmitIdenticalTitleForKnownStatusCodes()
    {
        var failure = D2Result.Fail(
            messages: [D2.Shared.I18n.TK.Auth.Errors.UNAUTHORIZED],
            errorCode: "OOPS",
            statusCode: HttpStatusCode.ServiceUnavailable);

        var pathAContext = new DefaultHttpContext();
        pathAContext.Request.Method = "GET";
        pathAContext.Request.Path = "/x";
        var pathABody = failure.ToProblemDetails(pathAContext);

        var pathBContext = new DefaultHttpContext();
        pathBContext.Request.Method = "GET";
        pathBContext.Request.Path = "/x";
        var customizerCtx = new ProblemDetailsContext
        {
            HttpContext = pathBContext,
            ProblemDetails = new MvcProblemDetails(),
        };
        pathBContext.SetD2Result(failure);
        D2ProblemDetailsCustomizer.Apply(customizerCtx, new D2ProblemDetailsOptions());

        pathABody.Title.Should().Be(customizerCtx.ProblemDetails.Title);
    }
}
