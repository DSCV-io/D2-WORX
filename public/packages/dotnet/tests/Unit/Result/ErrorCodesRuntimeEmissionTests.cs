// -----------------------------------------------------------------------
// <copyright file="ErrorCodesRuntimeEmissionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Runtime-emission pin tests for the <see cref="ErrorCodes"/> closed-set
/// value catalog. Spec-driving the error-code NAMES (via the ErrorCodes
/// source-gen + <see cref="ErrorCodesTests"/> name-pin coverage) closes the
/// name-level drift surface; this suite closes the value-level drift surface
/// — each enumerated code is shipped through a production
/// <see cref="D2Result"/> factory (or raw <c>Fail</c> for codes lacking a
/// semantic factory) and verified to land in the wire-serialized D2Result
/// envelope's <c>errorCode</c> field byte-for-byte. A refactor wiring
/// <see cref="D2Result.Conflict"/> to a different literal would NOT fail any
/// name-only test — but breaks BFF client switch statements, audit-log
/// queries, and alerting rules that filter on the wire <c>errorCode</c> value.
/// Complements the partial coverage in <see cref="D2ResultJsonShapeTests"/>
/// (NOT_FOUND + VALIDATION_FAILED) by covering the remaining 13 catalog
/// values that production factories emit without dedicated wire-value
/// runtime pins.
/// </summary>
public sealed class ErrorCodesRuntimeEmissionTests
{
    [Fact]
    public void Forbidden_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.Forbidden();

        ReadErrorCode(result).Should().Be("FORBIDDEN");
    }

    [Fact]
    public void Unauthorized_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.Unauthorized();

        ReadErrorCode(result).Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public void Conflict_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.Conflict();

        ReadErrorCode(result).Should().Be("CONFLICT");
    }

    [Fact]
    public void UnhandledException_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.UnhandledException();

        ReadErrorCode(result).Should().Be("UNHANDLED_EXCEPTION");
    }

    [Fact]
    public void ServiceUnavailable_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.ServiceUnavailable();

        ReadErrorCode(result).Should().Be("SERVICE_UNAVAILABLE");
    }

    [Fact]
    public void SomeFound_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.SomeFound();

        ReadErrorCode(result).Should().Be("SOME_FOUND");
    }

    [Fact]
    public void PartialSuccess_ErrorCodeLandsInSerializedEnvelope()
    {
        // PartialSuccess is generic-only — semantic factory lives on
        // D2Result<TData>. Success=true on this code (multi-target write
        // partially succeeded).
        var result = D2Result<string>.PartialSuccess(data: "ok");

        ReadErrorCode(result).Should().Be("PARTIAL_SUCCESS");
    }

    [Fact]
    public void RateLimited_ErrorCodeLandsInSerializedEnvelope()
    {
        // TooManyRequests is the semantic factory; RATE_LIMITED is the
        // default error-code value it emits (overridable for per-domain
        // discrimination).
        var result = D2Result.TooManyRequests();

        ReadErrorCode(result).Should().Be("RATE_LIMITED");
    }

    [Fact]
    public void PayloadTooLarge_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.PayloadTooLarge();

        ReadErrorCode(result).Should().Be("PAYLOAD_TOO_LARGE");
    }

    [Fact]
    public void Canceled_ErrorCodeLandsInSerializedEnvelope()
    {
        var result = D2Result.Canceled();

        ReadErrorCode(result).Should().Be("CANCELED");
    }

    [Fact]
    public void CouldNotBeSerialized_ErrorCodeLandsInSerializedEnvelope()
    {
        // No semantic factory for COULD_NOT_BE_SERIALIZED — production
        // emit paths use raw Fail with the catalog constant. Pin the same
        // emission path here so the wire value is runtime-verified.
        var result = D2Result.Fail(
            statusCode: HttpStatusCode.InternalServerError,
            errorCode: ErrorCodes.COULD_NOT_BE_SERIALIZED);

        ReadErrorCode(result).Should().Be("COULD_NOT_BE_SERIALIZED");
    }

    [Fact]
    public void CouldNotBeDeserialized_ErrorCodeLandsInSerializedEnvelope()
    {
        // No semantic factory for COULD_NOT_BE_DESERIALIZED — production
        // emit paths use raw Fail with the catalog constant.
        var result = D2Result.Fail(
            statusCode: HttpStatusCode.InternalServerError,
            errorCode: ErrorCodes.COULD_NOT_BE_DESERIALIZED);

        ReadErrorCode(result).Should().Be("COULD_NOT_BE_DESERIALIZED");
    }

    [Fact]
    public void IdempotencyInFlight_ErrorCodeLandsInSerializedEnvelope()
    {
        // No semantic factory for IDEMPOTENCY_IN_FLIGHT — the idempotency
        // middleware emits via raw Fail with the catalog constant on the
        // 409 dedup-collision branch.
        var result = D2Result.Fail(
            statusCode: HttpStatusCode.Conflict,
            errorCode: ErrorCodes.IDEMPOTENCY_IN_FLIGHT);

        ReadErrorCode(result).Should().Be("IDEMPOTENCY_IN_FLIGHT");
    }

    [Theory]
    [InlineData("NOT_FOUND")]
    [InlineData("FORBIDDEN")]
    [InlineData("UNAUTHORIZED")]
    [InlineData("VALIDATION_FAILED")]
    [InlineData("CONFLICT")]
    [InlineData("UNHANDLED_EXCEPTION")]
    [InlineData("COULD_NOT_BE_SERIALIZED")]
    [InlineData("COULD_NOT_BE_DESERIALIZED")]
    [InlineData("SERVICE_UNAVAILABLE")]
    [InlineData("SOME_FOUND")]
    [InlineData("PARTIAL_SUCCESS")]
    [InlineData("RATE_LIMITED")]
    [InlineData("IDEMPOTENCY_IN_FLIGHT")]
    [InlineData("PAYLOAD_TOO_LARGE")]
    [InlineData("CANCELED")]
    public void EveryCatalogValue_RoundTripsThroughEnvelopeUnchanged(string catalogValue)
    {
        // Catalog completeness: every value enumerated in
        // contracts/error-codes/error-codes.spec.json round-trips through
        // D2Result.Fail → JsonSerializer.Serialize → JSON parse → wire
        // value, byte-for-byte. Drift here would break BFF client switch
        // statements + audit-log queries + alerting rules that filter on
        // the wire errorCode value.
        var result = D2Result.Fail(errorCode: catalogValue);

        ReadErrorCode(result).Should().Be(catalogValue);
    }

    private static string ReadErrorCode(D2Result result)
    {
        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();
        return obj[D2ResultEnvelopeFieldNames.ERROR_CODE]!.GetValue<string>();
    }

    private static string ReadErrorCode<TData>(D2Result<TData> result)
    {
        var json = JsonSerializer.Serialize<object>(result);
        var obj = JsonNode.Parse(json)!.AsObject();
        return obj[D2ResultEnvelopeFieldNames.ERROR_CODE]!.GetValue<string>();
    }
}
