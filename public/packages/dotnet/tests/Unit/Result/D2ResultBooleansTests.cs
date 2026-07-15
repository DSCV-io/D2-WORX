// -----------------------------------------------------------------------
// <copyright file="D2ResultBooleansTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using AwesomeAssertions;
using DcsvIo.D2.Result;
using Xunit;

public sealed class D2ResultBooleansTests
{
    // ----------------------------------------------------------------------
    // Per-code booleans — true on the matching factory, false on others
    // ----------------------------------------------------------------------

    [Fact]
    public void IsOk_TrueOnOk_FalseOnFailures()
    {
        D2Result.Ok().IsOk.Should().BeTrue();
        D2Result.NotFound().IsOk.Should().BeFalse();
        D2Result.Conflict().IsOk.Should().BeFalse();
        D2Result.ValidationFailed().IsOk.Should().BeFalse();
    }

    [Fact]
    public void IsCreated_TrueOnCreated_FalseOnOk()
    {
        // Adversarial: IsCreated is StatusCode-based, so plain Ok() should NOT match.
        D2Result.Created().IsCreated.Should().BeTrue();
        D2Result.Ok().IsCreated.Should().BeFalse();
        D2Result.NotFound().IsCreated.Should().BeFalse();
    }

    [Fact]
    public void IsNotFound_TrueOnNotFound_FalseOnOthers()
    {
        D2Result.NotFound().IsNotFound.Should().BeTrue();
        D2Result.Conflict().IsNotFound.Should().BeFalse();
        D2Result.SomeFound().IsNotFound.Should().BeFalse();
        D2Result.Ok().IsNotFound.Should().BeFalse();
    }

    [Fact]
    public void IsSomeFound_TrueOnSomeFound_FalseOnOthers()
    {
        D2Result.SomeFound().IsSomeFound.Should().BeTrue();
        D2Result.NotFound().IsSomeFound.Should().BeFalse();
        D2Result.Ok().IsSomeFound.Should().BeFalse();
    }

    [Fact]
    public void IsConflict_TrueOnConflict_FalseOnOthers()
    {
        D2Result.Conflict().IsConflict.Should().BeTrue();
        D2Result.NotFound().IsConflict.Should().BeFalse();
        D2Result.Ok().IsConflict.Should().BeFalse();
    }

    [Fact]
    public void IsForbidden_TrueOnForbidden_FalseOnOthers()
    {
        D2Result.Forbidden().IsForbidden.Should().BeTrue();
        D2Result.Unauthorized().IsForbidden.Should().BeFalse();
        D2Result.Ok().IsForbidden.Should().BeFalse();
    }

    [Fact]
    public void IsUnauthorized_TrueOnUnauthorized_FalseOnOthers()
    {
        D2Result.Unauthorized().IsUnauthorized.Should().BeTrue();
        D2Result.Forbidden().IsUnauthorized.Should().BeFalse();
        D2Result.Ok().IsUnauthorized.Should().BeFalse();
    }

    [Fact]
    public void IsValidationFailed_TrueOnValidationFailed_FalseOnOthers()
    {
        D2Result.ValidationFailed().IsValidationFailed.Should().BeTrue();
        D2Result.Conflict().IsValidationFailed.Should().BeFalse();
        D2Result.Ok().IsValidationFailed.Should().BeFalse();
    }

    [Fact]
    public void IsValidationFailed_FalseWhenErrorCodeOverridden()
    {
        // Adversarial: ValidationFailed accepts an errorCode override.
        // When overridden, IsValidationFailed should NOT be true (it checks errorCode equality).
        var result = D2Result.ValidationFailed(errorCode: "FILES_INVALID_CONTENT_TYPE");

        result.IsValidationFailed.Should().BeFalse();
        result.ErrorCode.Should().Be("FILES_INVALID_CONTENT_TYPE");
    }

    [Fact]
    public void IsServiceUnavailable_TrueOnServiceUnavailable_FalseOnOthers()
    {
        D2Result.ServiceUnavailable().IsServiceUnavailable.Should().BeTrue();
        D2Result.UnhandledException().IsServiceUnavailable.Should().BeFalse();
        D2Result.TooManyRequests().IsServiceUnavailable.Should().BeFalse();
    }

    [Fact]
    public void IsRateLimited_TrueOnTooManyRequests_FalseOnOthers()
    {
        D2Result.TooManyRequests().IsRateLimited.Should().BeTrue();
        D2Result.ServiceUnavailable().IsRateLimited.Should().BeFalse();
        D2Result.Ok().IsRateLimited.Should().BeFalse();
    }

    [Fact]
    public void IsUnhandledException_TrueOnUnhandledException_FalseOnOthers()
    {
        D2Result.UnhandledException().IsUnhandledException.Should().BeTrue();
        D2Result.ServiceUnavailable().IsUnhandledException.Should().BeFalse();
        D2Result.Conflict().IsUnhandledException.Should().BeFalse();
    }

    [Fact]
    public void IsPayloadTooLarge_TrueOnPayloadTooLarge_FalseOnOthers()
    {
        D2Result.PayloadTooLarge().IsPayloadTooLarge.Should().BeTrue();
        D2Result.ValidationFailed().IsPayloadTooLarge.Should().BeFalse();
    }

    [Fact]
    public void IsCanceled_TrueOnCanceled_FalseOnOthers()
    {
        D2Result.Canceled().IsCanceled.Should().BeTrue();
        D2Result.UnhandledException().IsCanceled.Should().BeFalse();
    }

    [Fact]
    public void IsIdempotencyInFlight_NoFactoryButCheckableViaCustomErrorCode()
    {
        // No semantic factory exists for IDEMPOTENCY_IN_FLIGHT — middleware emits it
        // via Fail(errorCode: ErrorCodes.IDEMPOTENCY_IN_FLIGHT). Verify the boolean
        // discriminator works against that path.
        var result = D2Result.Fail(errorCode: ErrorCodes.IDEMPOTENCY_IN_FLIGHT);

        result.IsIdempotencyInFlight.Should().BeTrue();
        D2Result.Conflict().IsIdempotencyInFlight.Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // Combined helpers
    // ----------------------------------------------------------------------

    [Fact]
    public void IsPartialOrMissing_TrueOnNotFoundAndSomeFound_FalseOnOthers()
    {
        D2Result.NotFound().IsPartialOrMissing.Should().BeTrue();
        D2Result.SomeFound().IsPartialOrMissing.Should().BeTrue();

        D2Result.Ok().IsPartialOrMissing.Should().BeFalse();
        D2Result.Conflict().IsPartialOrMissing.Should().BeFalse();
        D2Result.Forbidden().IsPartialOrMissing.Should().BeFalse();
        D2Result.ValidationFailed().IsPartialOrMissing.Should().BeFalse();
    }

    [Fact]
    public void IsTransientRetryable_TrueOnServiceUnavailableAndRateLimited()
    {
        D2Result.ServiceUnavailable().IsTransientRetryable.Should().BeTrue();
        D2Result.TooManyRequests().IsTransientRetryable.Should().BeTrue();
    }

    [Fact]
    public void IsTransientRetryable_ExplicitlyFalseOnUnhandledException()
    {
        // Critical adversarial test: UnhandledException is INTENTIONALLY excluded from
        // IsTransientRetryable. An unknown exception means unknown system state — retry
        // could mask bugs or double-execute. Retry helpers consult this property; this
        // exclusion is load-bearing.
        D2Result.UnhandledException().IsTransientRetryable.Should().BeFalse();
    }

    [Fact]
    public void IsTransientRetryable_FalseOnNonTransientFailures()
    {
        D2Result.NotFound().IsTransientRetryable.Should().BeFalse();
        D2Result.Conflict().IsTransientRetryable.Should().BeFalse();
        D2Result.Forbidden().IsTransientRetryable.Should().BeFalse();
        D2Result.Unauthorized().IsTransientRetryable.Should().BeFalse();
        D2Result.ValidationFailed().IsTransientRetryable.Should().BeFalse();
        D2Result.PayloadTooLarge().IsTransientRetryable.Should().BeFalse();
        D2Result.Canceled().IsTransientRetryable.Should().BeFalse();
        D2Result.Ok().IsTransientRetryable.Should().BeFalse();
    }

    [Fact]
    public void IsTransientRetryable_FalseWhenServiceUnavailableErrorCodeOverridden()
    {
        // Adversarial: domain override of ServiceUnavailable's errorCode breaks the
        // IsServiceUnavailable check, which IsTransientRetryable depends on. Document
        // this behavior — callers using errorCode overrides on transient categories
        // must be aware retry helpers won't auto-classify them.
        var result = D2Result.ServiceUnavailable(errorCode: "DOMAIN_RETRY_LATER");

        result.IsServiceUnavailable.Should().BeFalse();
        result.IsTransientRetryable.Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // IsPartialSuccess — the unusual `Success=true && ErrorCode != null` case
    // ----------------------------------------------------------------------

    [Fact]
    public void IsPartialSuccess_TrueOnPartialSuccess_FalseOnOk()
    {
        // PartialSuccess is the load-bearing distinction vs SomeFound:
        // Success=true (multi-target write where SOME succeeded), but with
        // an ErrorCode set so callers can branch on the partial outcome.
        D2Result<int>.PartialSuccess(42).IsPartialSuccess.Should().BeTrue();
        D2Result<int>.Ok(42).IsPartialSuccess.Should().BeFalse();
        D2Result<int>.Ok(42).Success.Should().BeTrue();
    }

    [Fact]
    public void IsPartialSuccess_FalseOnSomeFound_FalseOnFailures()
    {
        // SomeFound is the read-side partial; PartialSuccess is the write-side
        // partial. They must NOT alias — different ErrorCodes, different Success.
        D2Result.SomeFound().IsPartialSuccess.Should().BeFalse();
        D2Result.NotFound().IsPartialSuccess.Should().BeFalse();
        D2Result.Conflict().IsPartialSuccess.Should().BeFalse();
        D2Result.ValidationFailed().IsPartialSuccess.Should().BeFalse();
    }

    [Fact]
    public void IsPartialSuccess_CoexistsWithSuccessTrue()
    {
        // Pin the unusual invariant: PartialSuccess is one of the few results
        // where Success=true AND a non-null ErrorCode coexist. A future
        // refactor that "normalizes" this (e.g. Success=>ErrorCode==null)
        // would silently break write-partial semantics across tiered cache
        // and multi-target SAGA flows.
        var result = D2Result<int>.PartialSuccess(42);

        result.Success.Should().BeTrue();
        result.ErrorCode.Should().Be(ErrorCodes.PARTIAL_SUCCESS);
        result.IsPartialSuccess.Should().BeTrue();
    }
}
