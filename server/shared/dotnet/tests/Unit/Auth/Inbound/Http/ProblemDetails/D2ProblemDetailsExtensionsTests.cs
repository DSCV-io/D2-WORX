// -----------------------------------------------------------------------
// <copyright file="D2ProblemDetailsExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Http.ProblemDetails;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using AwesomeAssertions;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Http.ProblemDetails;
using D2.Shared.Auth.Telemetry;
using D2.Shared.I18n;
using D2.Shared.Result;
using Microsoft.AspNetCore.Http;
using Xunit;

public sealed class D2ProblemDetailsExtensionsTests
{
    [Fact]
    public void ToProblemDetails_BearerMissing_HasUnauthorizedStatusAndCorrectErrorCode()
    {
        var ctx = MakeContext("/api/files/abc");

        var problem = AuthFailures.BearerMissing().ToProblemDetails(ctx);

        problem.Status.Should().Be(401);
        problem.Title.Should().Be(D2ProblemDetailsExtensions.TITLE_UNAUTHORIZED);
        problem.Type.Should().Be(
            "https://problems.d2-worx.com/auth/auth-bearer-missing");
        problem.Extensions[D2ProblemDetailsExtensions.EXTENSION_ERROR_CODE]
            .Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
        problem.Instance.Should().Be("/api/files/abc");
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
    public void ToProblemDetails_InstanceIsPathOnly_NoQueryString()
    {
        // Query strings carry referrers / search terms / sometimes
        // session-binding params. Stay path-only to avoid leaking them onto
        // ProblemDetails (which may end up in caller logs).
        var ctx = MakeContext("/api/x");
        ctx.Request.QueryString = new QueryString("?secret=abc&user=alice");

        var problem = AuthFailures.BearerMissing().ToProblemDetails(ctx);

        problem.Instance.Should().Be("/api/x");
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
    [InlineData(nameof(AuthFailures.JwtKidNotFound), AuthErrorCodes.AUTH_JWT_KID_NOT_FOUND)]
    [InlineData(nameof(AuthFailures.SessionRevoked), AuthErrorCodes.AUTH_SESSION_REVOKED)]
    [InlineData(nameof(AuthFailures.ScopeInsufficient), AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT)]
    public void ToProblemDetails_Every401Failure_Produces401WithCorrectErrorCodeAndType(
        string methodName,
        string expectedErrorCode)
    {
        var failure = (D2Result)typeof(AuthFailures)
            .GetMethod(methodName)!
            .Invoke(null, null)!;
        var ctx = MakeContext("/api/x");

        var problem = failure.ToProblemDetails(ctx);

        problem.Status.Should().Be(401);
        problem.Title.Should().Be(D2ProblemDetailsExtensions.TITLE_UNAUTHORIZED);
        problem.Extensions[D2ProblemDetailsExtensions.EXTENSION_ERROR_CODE]
            .Should().Be(expectedErrorCode);
        problem.Type.Should().StartWith(D2ProblemDetailsExtensions.TYPE_URI_PREFIX);
    }

    [Fact]
    public void ToProblemDetails_JwksUnavailable_Produces503()
    {
        var ctx = MakeContext("/api/x");

        var problem = AuthFailures.JwksUnavailable().ToProblemDetails(ctx);

        problem.Status.Should().Be(503);
        problem.Title.Should().Be(D2ProblemDetailsExtensions.TITLE_SERVICE_UNAVAILABLE);
        problem.Extensions[D2ProblemDetailsExtensions.EXTENSION_ERROR_CODE]
            .Should().Be(AuthErrorCodes.AUTH_JWKS_UNAVAILABLE);
    }

    [Fact]
    public void ToProblemDetails_SessionLivenessUnavailable_Produces503()
    {
        var ctx = MakeContext("/api/x");

        var problem = AuthFailures.SessionLivenessUnavailable().ToProblemDetails(ctx);

        problem.Status.Should().Be(503);
        problem.Title.Should().Be(D2ProblemDetailsExtensions.TITLE_SERVICE_UNAVAILABLE);
        problem.Extensions[D2ProblemDetailsExtensions.EXTENSION_ERROR_CODE]
            .Should().Be(AuthErrorCodes.AUTH_SESSION_LIVENESS_UNAVAILABLE);
    }

    [Fact]
    public void ToProblemDetails_MessagesExtensionCarriesOriginalTKMessages()
    {
        var ctx = MakeContext("/api/x");

        var problem = AuthFailures.BearerMissing().ToProblemDetails(ctx);

        var messages = problem.Extensions[D2ProblemDetailsExtensions.EXTENSION_MESSAGES];
        messages.Should().BeAssignableTo<IReadOnlyList<TKMessage>>();
        var asList = (IReadOnlyList<TKMessage>)messages;
        asList.Should().ContainSingle()
            .Which.Should().Be(TK.Auth.Errors.UNAUTHORIZED);
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

        problem.Extensions[D2ProblemDetailsExtensions.EXTENSION_TRACE_ID]
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
            .Should().NotContainKey(D2ProblemDetailsExtensions.EXTENSION_TRACE_ID);
    }

    [Fact]
    public void ToProblemDetails_IncrementsProblemEmittedCounter()
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

        AuthFailures.BearerMissing().ToProblemDetails(ctx);

        emitted.Should().Contain(kv =>
            kv.Key == "d2_error_code"
                && (string?)kv.Value == AuthErrorCodes.AUTH_BEARER_MISSING);
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
        problem.Title.Should().Be(D2ProblemDetailsExtensions.TITLE_REQUEST_FAILED);
    }

    private static DefaultHttpContext MakeContext(string path)
    {
        var ctx = new DefaultHttpContext();
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
