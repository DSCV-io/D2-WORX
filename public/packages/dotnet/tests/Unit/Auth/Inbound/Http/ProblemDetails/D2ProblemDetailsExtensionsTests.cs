// -----------------------------------------------------------------------
// <copyright file="D2ProblemDetailsExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Http.ProblemDetails;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Errors;
using DcsvIo.D2.Auth.Http.ProblemDetails;
using DcsvIo.D2.Auth.Telemetry;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.I18n;
using DcsvIo.D2.ProblemDetails;
using DcsvIo.D2.Result;
using Microsoft.AspNetCore.Http;
using Xunit;

[Collection("AuthTelemetrySerial")]
public sealed class D2ProblemDetailsExtensionsTests
{
    [Fact]
    public void ToProblemDetails_BearerMissing_HasUnauthorizedStatusAndCorrectErrorCode()
    {
        var ctx = MakeContext("/api/files/abc");

        var problem = AuthFailures.BearerMissing().ToProblemDetails(ctx);

        problem.Status.Should().Be(401);
        problem.Title.Should().Be(D2ProblemDetailsKeys.TITLE_UNAUTHORIZED);

        // Root URI prefix — no /auth/ segment (spec value pinned by the
        // emitter tests; cross-language parity enforced by the .parity test).
        problem.Type.Should().Be(
            "https://problems.d2.dcsv.io/auth-bearer-missing");
        problem.Extensions[D2ProblemDetailsKeys.EXTENSION_ERROR_CODE]
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
        problem.Instance.Should().Be("GET /api/files/abc");
    }

    [Fact]
    public void ToProblemDetails_DetailFieldDeliberatelyOmitted()
    {
        // Detail field is an info-leak vector for auth errors — telling the
        // attacker which check failed (signature vs expired vs claim missing)
        // is exactly what we hide. Granular d2_error_code carries the
        // operator-facing taxonomy without echoing it to user-visible prose.
        var ctx = MakeContext("/api/x");

        var problem = AuthFailures.JwtExpired().ToProblemDetails(ctx);

        problem.Detail.Should().BeNull();
    }

    [Fact]
    public void ToProblemDetails_InstanceShape_IsMethodSpacePath_NoQueryString()
    {
        // Query strings carry referrers / search terms / sometimes
        // session-binding params. Stay path-only (excluding the query) so
        // they're not leaked onto ProblemDetails. Method is included for
        // operator diagnosability and cross-path parity with the Customizer.
        var ctx = MakeContext("/api/x");
        ctx.Request.Method = "POST";
        ctx.Request.QueryString = new QueryString("?secret=abc&user=alice");

        var problem = AuthFailures.BearerMissing().ToProblemDetails(ctx);

        problem.Instance.Should().Be("POST /api/x");
        problem.Instance.Should().NotContain("secret");
    }

    [Theory]
    [InlineData(nameof(AuthFailures.BearerMissing), AuthErrorCodes.AUTH_BEARER_MISSING)]
    [InlineData(nameof(AuthFailures.BearerMalformed), AuthErrorCodes.AUTH_BEARER_MALFORMED)]
    [InlineData(
        nameof(AuthFailures.JwtSignatureInvalid),
        AuthErrorCodes.AUTH_JWT_SIGNATURE_INVALID)]
    [InlineData(nameof(AuthFailures.JwtExpired), AuthErrorCodes.AUTH_JWT_EXPIRED)]
    [InlineData(nameof(AuthFailures.JwtNotYetValid), AuthErrorCodes.AUTH_JWT_NOT_YET_VALID)]
    [InlineData(nameof(AuthFailures.JwtIssuerMismatch), AuthErrorCodes.AUTH_JWT_ISSUER_MISMATCH)]
    [InlineData(
        nameof(AuthFailures.JwtAudienceMismatch),
        AuthErrorCodes.AUTH_JWT_AUDIENCE_MISMATCH)]
    [InlineData(nameof(AuthFailures.JwtClaimMissing), AuthErrorCodes.AUTH_JWT_CLAIM_MISSING)]
    [InlineData(
        nameof(AuthFailures.JwtActChainMalformed),
        AuthErrorCodes.AUTH_JWT_ACT_CHAIN_MALFORMED)]
    [InlineData(nameof(AuthFailures.JwtKidNotFound), AuthErrorCodes.AUTH_JWT_KID_NOT_FOUND)]
    [InlineData(nameof(AuthFailures.SessionRevoked), AuthErrorCodes.AUTH_SESSION_REVOKED)]
    [InlineData(nameof(AuthFailures.ScopeInsufficient), AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT)]
    [InlineData(
        nameof(AuthFailures.RequestOriginUnestablished),
        AuthErrorCodes.AUTH_REQUEST_ORIGIN_UNESTABLISHED)]
    public void ToProblemDetails_Every401Failure_Produces401WithCorrectErrorCodeAndType(
        string methodName,
        string expectedErrorCode)
    {
        // The delegating factory carries a single optional `messages` override;
        // pass null to exercise the default-omitted (spec-TK) path.
        var failure = (D2Result)typeof(AuthFailures)
            .GetMethod(methodName)!
            .Invoke(null, [null])!;
        var ctx = MakeContext("/api/x");

        var problem = failure.ToProblemDetails(ctx);

        problem.Status.Should().Be(401);
        problem.Title.Should().Be(D2ProblemDetailsKeys.TITLE_UNAUTHORIZED);
        problem.Extensions[D2ProblemDetailsKeys.EXTENSION_ERROR_CODE]
            .Should().Be(expectedErrorCode);
        problem.Type.Should().StartWith(D2ProblemDetailsKeys.TYPE_URI_PREFIX);
    }

    [Fact]
    public void ToProblemDetails_JwksUnavailable_Produces503()
    {
        var ctx = MakeContext("/api/x");

        var problem = AuthFailures.JwksUnavailable().ToProblemDetails(ctx);

        problem.Status.Should().Be(503);
        problem.Title.Should().Be(D2ProblemDetailsKeys.TITLE_SERVICE_UNAVAILABLE);
        problem.Extensions[D2ProblemDetailsKeys.EXTENSION_ERROR_CODE]
            .Should().Be(AuthErrorCodes.AUTH_JWKS_UNAVAILABLE);
    }

    [Fact]
    public void ToProblemDetails_SessionLivenessUnavailable_Produces503()
    {
        var ctx = MakeContext("/api/x");

        var problem = AuthFailures.SessionLivenessUnavailable().ToProblemDetails(ctx);

        problem.Status.Should().Be(503);
        problem.Title.Should().Be(D2ProblemDetailsKeys.TITLE_SERVICE_UNAVAILABLE);
        problem.Extensions[D2ProblemDetailsKeys.EXTENSION_ERROR_CODE]
            .Should().Be(AuthErrorCodes.AUTH_SESSION_LIVENESS_UNAVAILABLE);
    }

    [Fact]
    public void ToProblemDetails_MessagesExtensionCarriesOriginalTKMessages()
    {
        var ctx = MakeContext("/api/x");

        var problem = AuthFailures.BearerMissing().ToProblemDetails(ctx);

        var messages = problem.Extensions[D2ProblemDetailsKeys.EXTENSION_MESSAGES];
        messages.Should().BeAssignableTo<IReadOnlyList<TKMessage>>();
        var asList = (IReadOnlyList<TKMessage>)messages;
        asList.Should().ContainSingle()
            .Which.Should().Be(TK.Auth.Errors.UNAUTHORIZED);
    }

    [Fact]
    public void ToProblemDetails_InputErrorsEmpty_OmitsInputErrorsExtension()
    {
        var ctx = MakeContext("/api/x");

        var problem = AuthFailures.BearerMissing().ToProblemDetails(ctx);

        problem.Extensions
            .Should().NotContainKey(D2ProblemDetailsKeys.EXTENSION_INPUT_ERRORS);
    }

    [Fact]
    public void ToProblemDetails_InputErrorsPresent_PopulatesInputErrorsExtension()
    {
        var ctx = MakeContext("/api/x");
        var inputErrors = new[]
        {
            new InputError("email", [TK.Auth.Errors.UNAUTHORIZED]),
        };
        var failure = D2Result.Fail(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            inputErrors: inputErrors,
            errorCode: AuthErrorCodes.AUTH_BEARER_MISSING,
            statusCode: HttpStatusCode.BadRequest);

        var problem = failure.ToProblemDetails(ctx);

        var emitted = problem.Extensions[D2ProblemDetailsKeys.EXTENSION_INPUT_ERRORS];
        emitted.Should().BeAssignableTo<IReadOnlyList<InputError>>();
        ((IReadOnlyList<InputError>)emitted)
            .Should().ContainSingle()
            .Which.Field.Should().Be("email");
    }

    [Fact]
    public void ToProblemDetails_CategoryExtensionCarriesWireString()
    {
        // BearerMissing carries ErrorCategory.ValidationFailure → the HTTP
        // body must surface the snake_case wire string under d2_category,
        // mirroring the D2Result envelope + gRPC envelope (cross-transport
        // parity).
        var ctx = MakeContext("/api/x");

        var problem = AuthFailures.BearerMissing().ToProblemDetails(ctx);

        problem.Extensions[D2ProblemDetailsKeys.EXTENSION_CATEGORY]
            .Should().Be(ErrorCategory.ValidationFailure.ToWire());
    }

    [Fact]
    public void ToProblemDetails_InfrastructureCategory_EmitsInfrastructureUnavailableWire()
    {
        var ctx = MakeContext("/api/x");

        var problem = AuthFailures.JwksUnavailable().ToProblemDetails(ctx);

        problem.Extensions[D2ProblemDetailsKeys.EXTENSION_CATEGORY]
            .Should().Be(ErrorCategory.InfrastructureUnavailable.ToWire());
    }

    [Fact]
    public void ToProblemDetails_NullCategory_OmitsCategoryExtension()
    {
        // A manually-built failure with no category → the extension is
        // omitted (never surfaced as null), matching the inputErrors /
        // traceId omit-when-absent discipline.
        var ctx = MakeContext("/api/x");
        var failure = D2Result.Fail(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            errorCode: "NO_CATEGORY",
            statusCode: HttpStatusCode.BadRequest);

        var problem = failure.ToProblemDetails(ctx);

        problem.Extensions
            .Should().NotContainKey(D2ProblemDetailsKeys.EXTENSION_CATEGORY);
    }

    [Fact]
    public void ToProblemDetails_ActivityCurrent_PopulatesTraceIdExtension()
    {
        using var source = new ActivitySource($"test-source-{Guid.NewGuid():N}");
        using var listener = MakeAllSampledListener();
        ActivitySource.AddActivityListener(listener);
        using var activity = source.StartActivity();
        activity.Should().NotBeNull();
        var ctx = MakeContext("/api/x");

        var problem = AuthFailures.BearerMissing().ToProblemDetails(ctx);

        problem.Extensions[D2ProblemDetailsKeys.EXTENSION_TRACE_ID]
            .Should().Be(activity.TraceId.ToString());
    }

    [Fact]
    public void ToProblemDetails_NoActivityCurrent_OmitsTraceIdExtension()
    {
        // Defensive — ensure no leftover ambient Activity from another test.
        Activity.Current?.Stop();
        Activity.Current = null;
        var ctx = MakeContext("/api/x");

        var problem = AuthFailures.BearerMissing().ToProblemDetails(ctx);

        problem.Extensions
            .Should().NotContainKey(D2ProblemDetailsKeys.EXTENSION_TRACE_ID);
    }

    [Theory]
    [InlineData(nameof(AuthFailures.BearerMissing), AuthErrorCodes.AUTH_BEARER_MISSING)]
    [InlineData(nameof(AuthFailures.BearerMalformed), AuthErrorCodes.AUTH_BEARER_MALFORMED)]
    [InlineData(
        nameof(AuthFailures.JwtSignatureInvalid),
        AuthErrorCodes.AUTH_JWT_SIGNATURE_INVALID)]
    [InlineData(nameof(AuthFailures.JwtExpired), AuthErrorCodes.AUTH_JWT_EXPIRED)]
    [InlineData(nameof(AuthFailures.JwtNotYetValid), AuthErrorCodes.AUTH_JWT_NOT_YET_VALID)]
    [InlineData(nameof(AuthFailures.JwtIssuerMismatch), AuthErrorCodes.AUTH_JWT_ISSUER_MISMATCH)]
    [InlineData(
        nameof(AuthFailures.JwtAudienceMismatch),
        AuthErrorCodes.AUTH_JWT_AUDIENCE_MISMATCH)]
    [InlineData(nameof(AuthFailures.JwtClaimMissing), AuthErrorCodes.AUTH_JWT_CLAIM_MISSING)]
    [InlineData(
        nameof(AuthFailures.JwtActChainMalformed),
        AuthErrorCodes.AUTH_JWT_ACT_CHAIN_MALFORMED)]
    [InlineData(nameof(AuthFailures.JwtKidNotFound), AuthErrorCodes.AUTH_JWT_KID_NOT_FOUND)]
    [InlineData(nameof(AuthFailures.JwksUnavailable), AuthErrorCodes.AUTH_JWKS_UNAVAILABLE)]
    [InlineData(nameof(AuthFailures.SessionRevoked), AuthErrorCodes.AUTH_SESSION_REVOKED)]
    [InlineData(
        nameof(AuthFailures.SessionLivenessUnavailable),
        AuthErrorCodes.AUTH_SESSION_LIVENESS_UNAVAILABLE)]
    [InlineData(nameof(AuthFailures.ScopeInsufficient), AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT)]
    [InlineData(
        nameof(AuthFailures.RequestOriginUnestablished),
        AuthErrorCodes.AUTH_REQUEST_ORIGIN_UNESTABLISHED)]
    public void ToProblemDetails_IncrementsProblemEmittedCounter(
        string methodName,
        string expectedCode)
    {
        var emitted = new List<KeyValuePair<string, object?>>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == AuthTelemetry.METER_NAME
                && instrument.Name == "d2.auth.problem.emitted")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>(
            (_, _, tags, _) =>
            {
                foreach (var tag in tags)
                    emitted.Add(new KeyValuePair<string, object?>(tag.Key, tag.Value));
            });
        listener.Start();
        var ctx = MakeContext("/api/x");
        var factory = typeof(AuthFailures)
            .GetMethods()
            .First(m => m.Name == methodName && !m.IsGenericMethod);
        var failure = (D2Result)factory.Invoke(null, [null])!;

        failure.ToProblemDetails(ctx);

        emitted.Should().Contain(kv =>
            kv.Key == "d2_error_code"
                && (string?)kv.Value == expectedCode);
    }

    [Fact]
    public void ToProblemDetails_SuccessResult_Throws()
    {
        var ctx = MakeContext("/api/x");

        var act = () => D2Result.Ok().ToProblemDetails(ctx);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToProblemDetails_NullResult_Throws()
    {
        var ctx = MakeContext("/api/x");
        D2Result? result = null;

        var act = () => result!.ToProblemDetails(ctx);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToProblemDetails_NullContext_Throws()
    {
        var act = () => AuthFailures.BearerMissing().ToProblemDetails(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToProblemDetails_StatusCodeMappedFromResultStatusCode()
    {
        // Defense against accidental remap: the middleware MUST propagate
        // D2Result.StatusCode verbatim. A fabricated 418 should land on the
        // wire as 418 (with the "Request Failed" fallback Title).
        var ctx = MakeContext("/api/x");
        var weirdFailure = D2Result.Fail(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            errorCode: "WEIRD_THING",
            statusCode: (HttpStatusCode)418);

        var problem = weirdFailure.ToProblemDetails(ctx);

        problem.Status.Should().Be(418);
        problem.Title.Should().Be(D2ProblemDetailsKeys.TITLE_REQUEST_FAILED);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(206)]
    [InlineData(301)]
    [InlineData(399)]
    public void ToProblemDetails_Non4xx5xxStatus_ThrowsInvalidOperationException(int status)
    {
        // RFC 7807 frames ProblemDetails around 4xx/5xx error responses. A
        // 2xx partial-success (e.g. SomeFound / 206) belongs on the D2Result
        // envelope, not the ProblemDetails body. The guard converts a silent
        // semantic mismatch into a loud runtime exception so this never
        // ships unnoticed.
        var ctx = MakeContext("/api/x");
        var nonErrorFailure = D2Result.Fail(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            errorCode: "OOPS",
            statusCode: (HttpStatusCode)status);

        var act = () => nonErrorFailure.ToProblemDetails(ctx);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*non-error status code*");
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public void ToProblemDetails_4xx5xxStatus_DoesNotThrowGuard(int status)
    {
        var ctx = MakeContext("/api/x");
        var failure = D2Result.Fail(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            errorCode: "ANY",
            statusCode: (HttpStatusCode)status);

        var problem = failure.ToProblemDetails(ctx);

        problem.Status.Should().Be(status);
    }

    private static DefaultHttpContext MakeContext(string path)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        ctx.Request.Path = path;
        return ctx;
    }

    private static ActivityListener MakeAllSampledListener()
    {
        // Helper avoids a `ref` lambda inline (which inspectcode flags as
        // having a redundant explicit type spec) AND keeps the listener
        // configuration readable.
        var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
        };
        listener.Sample = SampleAll;
        return listener;
    }

    private static ActivitySamplingResult SampleAll(
        ref ActivityCreationOptions<ActivityContext> options)
        => ActivitySamplingResult.AllDataAndRecorded;
}
