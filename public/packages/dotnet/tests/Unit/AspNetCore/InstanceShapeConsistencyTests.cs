// -----------------------------------------------------------------------
// <copyright file="InstanceShapeConsistencyTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AspNetCore;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.AspNetCore;
using DcsvIo.D2.AspNetCore.Internal;
using DcsvIo.D2.Auth.Errors;
using DcsvIo.D2.Auth.Http.ProblemDetails;
using DcsvIo.D2.Result;
using Microsoft.AspNetCore.Http;
using Xunit;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

/// <summary>
/// Cross-path regression: path A
/// (<see cref="D2ProblemDetailsExtensions.ToProblemDetails"/> in
/// <c>DcsvIo.D2.Auth.Http</c>) and path B
/// (<see cref="D2ProblemDetailsCustomizer"/> in
/// <c>DcsvIo.D2.AspNetCore</c>) MUST emit the same <c>instance</c> shape
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
            messages: [DcsvIo.D2.I18n.TK.Auth.Errors.UNAUTHORIZED],
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
            messages: [DcsvIo.D2.I18n.TK.Auth.Errors.UNAUTHORIZED],
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
