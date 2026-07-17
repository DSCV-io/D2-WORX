// -----------------------------------------------------------------------
// <copyright file="D2RpcStatusExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Status;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Errors;
using DcsvIo.D2.Auth.Grpc.Status;
using DcsvIo.D2.Auth.Telemetry;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using global::Grpc.Core;
using Xunit;
using GrpcStatusCode = global::Grpc.Core.StatusCode;

[Collection("AuthTelemetrySerial")]
public sealed class D2RpcStatusExtensionsTests
{
    [Fact]
    public void ToRpcException_BearerMissing_HasUnauthenticatedStatusAndCorrectErrorCode()
    {
        var rpc = AuthFailures.BearerMissing().ToRpcException();

        rpc.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        var errorCode = ReadTrailerString(
            rpc.Trailers, D2GrpcTrailers.ERROR_CODE);
        errorCode.Should().Be(AuthErrorCodes.AUTH_BEARER_MISSING);
    }

    [Fact]
    public void ToRpcException_StatusDetailDeliberatelyEmpty()
    {
        // Detail is an info-leak vector for auth errors (telling an attacker
        // which check failed). We deliberately leave it empty — the granular
        // d2_error_code trailer carries the operator-facing taxonomy.
        var rpc = AuthFailures.JwtExpired().ToRpcException();

        rpc.Status.Detail.Should().BeEmpty();
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
    public void ToRpcException_Every401Failure_ProducesUnauthenticatedWithCorrectErrorCode(
        string methodName,
        string expectedErrorCode)
    {
        // The delegating factory carries a single optional `messages` override;
        // pass null to exercise the default-omitted (spec-TK) path.
        var failure = (D2Result)typeof(AuthFailures)
            .GetMethod(methodName)!
            .Invoke(null, [null])!;

        var rpc = failure.ToRpcException();

        rpc.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
        ReadTrailerString(rpc.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(expectedErrorCode);
    }

    [Fact]
    public void ToRpcException_JwksUnavailable_ProducesUnavailable()
    {
        var rpc = AuthFailures.JwksUnavailable().ToRpcException();

        rpc.StatusCode.Should().Be(GrpcStatusCode.Unavailable);
        ReadTrailerString(rpc.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_JWKS_UNAVAILABLE);
    }

    [Fact]
    public void ToRpcException_SessionLivenessUnavailable_ProducesUnavailable()
    {
        var rpc = AuthFailures.SessionLivenessUnavailable().ToRpcException();

        rpc.StatusCode.Should().Be(GrpcStatusCode.Unavailable);
        ReadTrailerString(rpc.Trailers, D2GrpcTrailers.ERROR_CODE)
            .Should().Be(AuthErrorCodes.AUTH_SESSION_LIVENESS_UNAVAILABLE);
    }

    [Fact]
    public void ToRpcException_NoPermissionDeniedMappingForScopeInsufficient()
    {
        // Defense-in-depth: scope-insufficient MUST map to Unauthenticated (16),
        // never PermissionDenied (7) — same uniform 401-shape policy as the
        // HTTP middleware's no-403 rule.
        var rpc = AuthFailures.ScopeInsufficient().ToRpcException();

        rpc.StatusCode.Should().NotBe(GrpcStatusCode.PermissionDenied);
        rpc.StatusCode.Should().Be(GrpcStatusCode.Unauthenticated);
    }

    [Fact]
    public void ToRpcException_DefensiveFallback_FabricatedNonAuthFailure_MapsToInternal()
    {
        // Fabricated failure with an off-spectrum HTTP status — confirms
        // the defensive Internal fallback rather than a silent
        // Unauthenticated map.
        var weird = D2Result.Fail(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            errorCode: "WEIRD_THING",
            statusCode: (HttpStatusCode)418);

        var rpc = weird.ToRpcException();

        rpc.StatusCode.Should().Be(GrpcStatusCode.Internal);
    }

    [Fact]
    public void ToRpcException_TrailersCarryMessagesJson()
    {
        var rpc = AuthFailures.BearerMissing().ToRpcException();

        var json = ReadTrailerString(rpc.Trailers, D2GrpcTrailers.MESSAGES);
        json.Should().NotBeEmpty();

        // JSON shape: array of TKMessage objects: [{ "key": "auth_errors_UNAUTHORIZED" }]
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().Be(1);
        doc.RootElement[0].GetProperty("key").GetString()
            .Should().Be(TK.Auth.Errors.UNAUTHORIZED.Key);
    }

    [Fact]
    public void ToRpcException_ActivityCurrent_PopulatesTraceIdTrailer()
    {
        using var source = new ActivitySource($"test-source-{Guid.NewGuid():N}");
        using var listener = MakeAllSampledListener();
        ActivitySource.AddActivityListener(listener);
        using var activity = source.StartActivity();
        activity.Should().NotBeNull();

        var rpc = AuthFailures.BearerMissing().ToRpcException();

        var traceId = ReadTrailerString(
            rpc.Trailers, D2GrpcTrailers.TRACE_ID);
        traceId.Should().Be(activity.TraceId.ToString());
    }

    [Fact]
    public void ToRpcException_NoActivityCurrent_OmitsTraceIdTrailer()
    {
        // Defensive — ensure no leftover ambient Activity from another test.
        Activity.Current?.Stop();
        Activity.Current = null;

        var rpc = AuthFailures.BearerMissing().ToRpcException();

        rpc.Trailers.Should()
            .NotContain(e => e.Key == D2GrpcTrailers.TRACE_ID);
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
    public void ToRpcException_IncrementsProblemEmittedCounter(
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
        var factory = typeof(AuthFailures)
            .GetMethods()
            .First(m => m.Name == methodName && !m.IsGenericMethod);
        var failure = (D2Result)factory.Invoke(null, [null])!;

        failure.ToRpcException();

        emitted.Should().Contain(kv =>
            kv.Key == "d2_error_code"
                && (string?)kv.Value == expectedCode);
    }

    [Fact]
    public void ToRpcException_SuccessResult_Throws()
    {
        var act = () => D2Result.Ok().ToRpcException();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ToRpcException_NullResult_Throws()
    {
        D2Result? result = null;

        var act = () => result!.ToRpcException();

        act.Should().Throw<ArgumentNullException>();
    }

    private static string? ReadTrailerString(Metadata trailers, string key)
    {
        foreach (var entry in trailers)
        {
            if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase)
                && !entry.IsBinary)
            {
                return entry.Value;
            }
        }

        return null;
    }

    private static ActivityListener MakeAllSampledListener()
    {
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
