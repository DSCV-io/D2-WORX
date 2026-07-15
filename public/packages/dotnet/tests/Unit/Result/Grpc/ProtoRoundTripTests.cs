// -----------------------------------------------------------------------
// <copyright file="ProtoRoundTripTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result.Grpc;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using DcsvIo.D2.Result.Grpc;
using global::D2.Services.Protos.Common.V1;
using Xunit;

/// <summary>
/// Round-trip fidelity tests: every <see cref="D2Result"/> shape survives
/// the in-memory → <see cref="D2ResultProto"/> → in-memory cycle with
/// EQUAL field values on every carried property.
/// </summary>
public sealed class ProtoRoundTripTests
{
    // ── Ok (no data) ─────────────────────────────────────────────────────

    [Fact]
    public void Ok_RoundTrips_EqualFields()
    {
        var source = D2Result.Ok();
        var proto = source.ToProto();
        var rebuilt = proto.ToD2Result<string?>();

        rebuilt.Success.Should().BeTrue();
        rebuilt.StatusCode.Should().Be(HttpStatusCode.OK);
        rebuilt.ErrorCode.Should().BeNull();
        rebuilt.TraceId.Should().BeNull();
        rebuilt.Category.Should().BeNull();
        rebuilt.Messages.Should().BeEmpty();
        rebuilt.InputErrors.Should().BeEmpty();
    }

    // ── Ok<T> with data ──────────────────────────────────────────────────

    [Fact]
    public void OkGeneric_WithData_RoundTrips_DataAndFields()
    {
        const string expected_data = "hello-world";
        var source = D2Result<string>.Ok(expected_data);
        var proto = source.ToProto();

        // data travels in the sibling field, not inside D2ResultProto
        var rebuilt = proto.ToD2Result(expected_data);

        rebuilt.Success.Should().BeTrue();
        rebuilt.Data.Should().Be(expected_data);
        rebuilt.StatusCode.Should().Be(HttpStatusCode.OK);
        rebuilt.Category.Should().BeNull();
    }

    // ── NotFound ─────────────────────────────────────────────────────────

    [Fact]
    public void NotFound_RoundTrips_ExactStatus404AndCategory()
    {
        var source = D2Result.NotFound();
        var proto = source.ToProto();
        var rebuilt = proto.ToD2Result<string?>();

        rebuilt.Success.Should().BeFalse();
        rebuilt.StatusCode.Should().Be(HttpStatusCode.NotFound);
        rebuilt.Category.Should().Be(ErrorCategory.NotFound);
        rebuilt.ErrorCode.Should().NotBeNullOrEmpty();
        rebuilt.Messages.Should().NotBeEmpty();
    }

    // ── Conflict ─────────────────────────────────────────────────────────

    [Fact]
    public void Conflict_RoundTrips_ExactStatus409()
    {
        var source = D2Result.Conflict();
        var proto = source.ToProto();
        var rebuilt = proto.ToD2Result<string?>();

        rebuilt.StatusCode.Should().Be(HttpStatusCode.Conflict);
        rebuilt.Category.Should().Be(ErrorCategory.Conflict);
    }

    // ── Unauthorized ─────────────────────────────────────────────────────

    [Fact]
    public void Unauthorized_RoundTrips_ExactStatus401()
    {
        var source = D2Result.Unauthorized();
        var proto = source.ToProto();
        var rebuilt = proto.ToD2Result<string?>();

        rebuilt.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        rebuilt.Category.Should().Be(ErrorCategory.PolicyDenied);
    }

    // ── ServiceUnavailable ───────────────────────────────────────────────

    [Fact]
    public void ServiceUnavailable_RoundTrips_ExactStatus503()
    {
        var source = D2Result.ServiceUnavailable();
        var proto = source.ToProto();
        var rebuilt = proto.ToD2Result<string?>();

        rebuilt.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        rebuilt.Category.Should().Be(ErrorCategory.InfrastructureUnavailable);
    }

    // ── ValidationFailed with multi-field, multi-message inputErrors ─────

    [Fact]
    public void ValidationFailed_WithInputErrors_RoundTrips_AllFieldsAndMessages()
    {
        var inputErrors = new List<InputError>
        {
            new("email", [TK.Common.Errors.NOT_FOUND, TK.Common.Errors.SERVICE_UNAVAILABLE]),
            new("phone", [TK.Common.Errors.CONFLICT.With("field", "phone")]),
        };
        var source = new D2Result(false, [], inputErrors, HttpStatusCode.UnprocessableEntity, "VALIDATION", "trace-abc");
        var proto = source.ToProto();
        var rebuilt = proto.ToD2Result<string?>();

        rebuilt.Success.Should().BeFalse();
        rebuilt.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        rebuilt.ErrorCode.Should().Be("VALIDATION");
        rebuilt.TraceId.Should().Be("trace-abc");
        rebuilt.InputErrors.Should().HaveCount(2);

        var emailErr = rebuilt.InputErrors[0];
        emailErr.Field.Should().Be("email");
        emailErr.Errors.Should().HaveCount(2);
        emailErr.Errors[0].Key.Should().Be(TK.Common.Errors.NOT_FOUND.Key);
        emailErr.Errors[1].Key.Should().Be(TK.Common.Errors.SERVICE_UNAVAILABLE.Key);

        var phoneErr = rebuilt.InputErrors[1];
        phoneErr.Field.Should().Be("phone");
        phoneErr.Errors[0].Key.Should().Be(TK.Common.Errors.CONFLICT.Key);
        phoneErr.Errors[0].Parameters.Should().ContainKey("field");
        phoneErr.Errors[0].Parameters!["field"].Should().Be("phone");
    }

    // ── TKMessage key + params survive the round-trip ────────────────────

    [Fact]
    public void Messages_WithParams_RoundTrip_KeyAndParams()
    {
        var msg = TK.Common.Errors.CONFLICT.With("detail", "already-exists").With("count", "3");
        var source = new D2Result(false, [msg]);
        var proto = source.ToProto();
        var rebuilt = proto.ToD2Result<string?>();

        rebuilt.Messages.Should().HaveCount(1);
        var roundTripped = rebuilt.Messages[0];
        roundTripped.Key.Should().Be(msg.Key);
        roundTripped.Parameters.Should().NotBeNull();
        roundTripped.Parameters!["detail"].Should().Be("already-exists");
        roundTripped.Parameters["count"].Should().Be("3");
    }

    // ── SomeFound<T> — success=false, data present ───────────────────────

    [Fact]
    public void SomeFound_Generic_RoundTrips_DataAndPartialSuccessCategory()
    {
        const string expected_data = "partial-payload";
        var source = D2Result<string>.SomeFound(expected_data);
        var proto = source.ToProto();
        var rebuilt = proto.ToD2Result(expected_data);

        rebuilt.Success.Should().BeFalse("SomeFound is partial-success, not success");
        rebuilt.Data.Should().Be(expected_data, "data round-trips through sibling field");
        rebuilt.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        rebuilt.Category.Should().Be(ErrorCategory.PartialSuccess);
    }

    // ── Typed failure (no data) ───────────────────────────────────────────

    [Fact]
    public void TypedFailure_NoData_RoundTrips()
    {
        var source = D2Result<int[]>.NotFound();
        var proto = source.ToProto();
        var rebuilt = proto.ToD2Result<int[]?>();

        rebuilt.Success.Should().BeFalse();
        rebuilt.Data.Should().BeNull();
        rebuilt.Category.Should().Be(ErrorCategory.NotFound);
    }

    // ── Non-catalog Fail (category null) ─────────────────────────────────

    [Fact]
    public void NonCatalogFail_CategoryNull_RoundTrips_CategoryAbsent()
    {
        var source = new D2Result(false, [], null, HttpStatusCode.BadRequest, "CUSTOM_CODE");
        var proto = source.ToProto();
        var rebuilt = proto.ToD2Result<string?>();

        rebuilt.Category.Should().BeNull("non-catalog Fail has no category");
        rebuilt.ErrorCode.Should().Be("CUSTOM_CODE");
        rebuilt.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── ValidationFailed Category round-trip ─────────────────────────────

    [Fact]
    public void ValidationFailed_RoundTrips_ValidationFailureCategory()
    {
        // FIX-3: genuine ToProto→ToD2Result→Category assertion (not just a ToProto status check).
        var rebuilt = D2Result.ValidationFailed().ToProto().ToD2Result<string?>();
        rebuilt.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // ── Remaining categories round-trip (FIX-4) ───────────────────────────

    [Theory]
    [InlineData("UnhandledException", ErrorCategory.InternalError)]
    [InlineData("PayloadTooLarge", ErrorCategory.PayloadTooLarge)]
    [InlineData("TooManyRequests", ErrorCategory.RateLimited)]
    public void Factory_RoundTrips_ExpectedCategory(string factoryName, ErrorCategory expectedCategory)
    {
        // Each factory: ToProto→ToD2Result→assert Category.
        var source = factoryName switch
        {
            "UnhandledException" => D2Result.UnhandledException(),
            "PayloadTooLarge" => D2Result.PayloadTooLarge(),
            "TooManyRequests" => D2Result.TooManyRequests(),
            _ => throw new ArgumentOutOfRangeException(nameof(factoryName), factoryName, "Unknown factory"),
        };

        var rebuilt = source.ToProto().ToD2Result<string?>();
        rebuilt.Category.Should().Be(expectedCategory, $"{factoryName} must round-trip its category");
    }

    [Fact]
    public void Canceled_RoundTrips_ValidationFailureCategory()
    {
        // Pin the non-obvious mapping: Canceled() produces ValidationFailure category.
        var rebuilt = D2Result.Canceled().ToProto().ToD2Result<string?>();
        rebuilt.Category.Should().Be(ErrorCategory.ValidationFailure, "Canceled maps to ValidationFailure category per factory spec");
    }

    // ── Status fidelity — exact integer, NOT a lossy gRPC bucket ─────────

    [Fact]
    public void StatusFidelity_NotFound_404_IsNotRoundTrippedAsGenericFailure()
    {
        var source = D2Result.NotFound();
        var proto = source.ToProto();

        // proto3 status_code field carries the exact int — NOT a lossy gRPC bucket
        proto.StatusCode.Should().Be(404);

        var rebuilt = proto.ToD2Result<string?>();
        rebuilt.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void StatusFidelity_Conflict_409_PreservedDistinctFrom400()
    {
        var conflictProto = D2Result.Conflict().ToProto();
        var validationProto = D2Result.ValidationFailed().ToProto();

        conflictProto.StatusCode.Should().Be(409);
        validationProto.StatusCode.Should().Be(400);
        conflictProto.StatusCode.Should().NotBe(validationProto.StatusCode);
    }

    // ── TraceId round-trip ────────────────────────────────────────────────

    [Fact]
    public void TraceId_RoundTrips_WhenPresent()
    {
        const string trace = "0123456789abcdef0123456789abcdef";
        var source = D2Result.NotFound().WithTraceId(trace);
        var rebuilt = source.ToProto().ToD2Result<string?>();
        rebuilt.TraceId.Should().Be(trace);
    }

    [Fact]
    public void TraceId_Null_WhenAbsent()
    {
        var source = D2Result.NotFound();
        var rebuilt = source.ToProto().ToD2Result<string?>();
        rebuilt.TraceId.Should().BeNull();
    }

    // ── Category absent / unknown wire string → null, not a throw ────────

    [Fact]
    public void Category_AbsentOnWire_RehydratesAsNull()
    {
        var proto = new D2ResultProto { Success = false, StatusCode = 400 };

        // HasCategory == false → no category field set
        var rebuilt = proto.ToD2Result<string?>();
        rebuilt.Category.Should().BeNull();
    }

    [Fact]
    public void Category_UnknownWireString_RehydratesAsNull_NotThrow()
    {
        var proto = new D2ResultProto { Success = false, StatusCode = 400, Category = "xyz_unknown_future" };
        var act = () => proto.ToD2Result<string?>();
        act.Should().NotThrow();
        var result = proto.ToD2Result<string?>();
        result.Category.Should().BeNull();
    }

    // ── Empty params vs null params fidelity ─────────────────────────────

    [Fact]
    public void TKMessage_EmptyParams_RoundTripsAsNullParameters()
    {
        var msg = TK.Common.Errors.NOT_FOUND;
        msg.Parameters.Should().BeNull("no params bound → Parameters is null");
        var rebuilt = D2Result.NotFound().ToProto().ToD2Result<string?>();
        rebuilt.Messages[0].Parameters.Should().BeNull();
    }

    // ── Large inputErrors fit in the envelope (no 8 KB trailer bound) ────

    [Fact]
    public void LargeInputErrors_AllCarriedInEnvelope()
    {
        const int field_count = 50;
        var inputErrors = Enumerable.Range(0, field_count)
            .Select(i => new InputError($"field{i}", [TK.Common.Errors.NOT_FOUND]))
            .ToList();
        var source = new D2Result(false, [], inputErrors, HttpStatusCode.UnprocessableEntity);
        var rebuilt = source.ToProto().ToD2Result<string?>();
        rebuilt.InputErrors.Should().HaveCount(field_count);
    }
}
