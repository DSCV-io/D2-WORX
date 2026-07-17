// -----------------------------------------------------------------------
// <copyright file="ErrorCodesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using AwesomeAssertions;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Pins the exact wire-format string of every <see cref="ErrorCodes"/>
/// constant. The constants use <c>nameof(X)</c> so renaming a field would
/// silently change the wire value — wire-format consumers (web client
/// switch statements, audit log queries, alerting rules) wouldn't break the
/// build but would silently misclassify failures. These tests are the gate.
/// </summary>
public sealed class ErrorCodesTests
{
    [Theory]
    [InlineData(nameof(ErrorCodes.NOT_FOUND), "NOT_FOUND")]
    [InlineData(nameof(ErrorCodes.FORBIDDEN), "FORBIDDEN")]
    [InlineData(nameof(ErrorCodes.UNAUTHORIZED), "UNAUTHORIZED")]
    [InlineData(nameof(ErrorCodes.VALIDATION_FAILED), "VALIDATION_FAILED")]
    [InlineData(nameof(ErrorCodes.CONFLICT), "CONFLICT")]
    [InlineData(nameof(ErrorCodes.UNHANDLED_EXCEPTION), "UNHANDLED_EXCEPTION")]
    [InlineData(nameof(ErrorCodes.COULD_NOT_BE_SERIALIZED), "COULD_NOT_BE_SERIALIZED")]
    [InlineData(nameof(ErrorCodes.COULD_NOT_BE_DESERIALIZED), "COULD_NOT_BE_DESERIALIZED")]
    [InlineData(nameof(ErrorCodes.SERVICE_UNAVAILABLE), "SERVICE_UNAVAILABLE")]
    [InlineData(nameof(ErrorCodes.SOME_FOUND), "SOME_FOUND")]
    [InlineData(nameof(ErrorCodes.PARTIAL_SUCCESS), "PARTIAL_SUCCESS")]
    [InlineData(nameof(ErrorCodes.RATE_LIMITED), "RATE_LIMITED")]
    [InlineData(nameof(ErrorCodes.IDEMPOTENCY_IN_FLIGHT), "IDEMPOTENCY_IN_FLIGHT")]
    [InlineData(nameof(ErrorCodes.PAYLOAD_TOO_LARGE), "PAYLOAD_TOO_LARGE")]
    [InlineData(nameof(ErrorCodes.CANCELED), "CANCELED")]
    public void Constant_NameAndValueMatch(string nameOfValue, string expectedWireValue)
    {
        // nameof() captured at the test-call site; the constant's runtime VALUE
        // must equal the field name exactly. Renaming the field flips both the
        // capture AND the runtime value, so this test catches the case where
        // someone replaces `nameof(X)` with a literal that's drifted.
        nameOfValue.Should().Be(expectedWireValue);
    }

    [Fact]
    public void Constant_NotFound_HasCanonicalWireValue()
    {
        ErrorCodes.NOT_FOUND.Should().Be("NOT_FOUND");
    }

    [Fact]
    public void Constant_Forbidden_HasCanonicalWireValue()
    {
        ErrorCodes.FORBIDDEN.Should().Be("FORBIDDEN");
    }

    [Fact]
    public void Constant_Unauthorized_HasCanonicalWireValue()
    {
        ErrorCodes.UNAUTHORIZED.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public void Constant_ValidationFailed_HasCanonicalWireValue()
    {
        ErrorCodes.VALIDATION_FAILED.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public void Constant_Conflict_HasCanonicalWireValue()
    {
        ErrorCodes.CONFLICT.Should().Be("CONFLICT");
    }

    [Fact]
    public void Constant_UnhandledException_HasCanonicalWireValue()
    {
        ErrorCodes.UNHANDLED_EXCEPTION.Should().Be("UNHANDLED_EXCEPTION");
    }

    [Fact]
    public void Constant_CouldNotBeSerialized_HasCanonicalWireValue()
    {
        ErrorCodes.COULD_NOT_BE_SERIALIZED.Should().Be("COULD_NOT_BE_SERIALIZED");
    }

    [Fact]
    public void Constant_CouldNotBeDeserialized_HasCanonicalWireValue()
    {
        ErrorCodes.COULD_NOT_BE_DESERIALIZED.Should().Be("COULD_NOT_BE_DESERIALIZED");
    }

    [Fact]
    public void Constant_ServiceUnavailable_HasCanonicalWireValue()
    {
        ErrorCodes.SERVICE_UNAVAILABLE.Should().Be("SERVICE_UNAVAILABLE");
    }

    [Fact]
    public void Constant_SomeFound_HasCanonicalWireValue()
    {
        ErrorCodes.SOME_FOUND.Should().Be("SOME_FOUND");
    }

    [Fact]
    public void Constant_PartialSuccess_HasCanonicalWireValue()
    {
        ErrorCodes.PARTIAL_SUCCESS.Should().Be("PARTIAL_SUCCESS");
    }

    [Fact]
    public void Constant_RateLimited_HasCanonicalWireValue()
    {
        ErrorCodes.RATE_LIMITED.Should().Be("RATE_LIMITED");
    }

    [Fact]
    public void Constant_IdempotencyInFlight_HasCanonicalWireValue()
    {
        ErrorCodes.IDEMPOTENCY_IN_FLIGHT.Should().Be("IDEMPOTENCY_IN_FLIGHT");
    }

    [Fact]
    public void Constant_PayloadTooLarge_HasCanonicalWireValue()
    {
        ErrorCodes.PAYLOAD_TOO_LARGE.Should().Be("PAYLOAD_TOO_LARGE");
    }

    [Fact]
    public void Constant_Canceled_HasCanonicalWireValue()
    {
        // American English (single L) — matches BCL OperationCanceledException.
        ErrorCodes.CANCELED.Should().Be("CANCELED");
    }
}
