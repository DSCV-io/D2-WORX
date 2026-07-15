// -----------------------------------------------------------------------
// <copyright file="D2ResultWithTraceIdTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="D2Result.WithTraceId"/> +
/// <see cref="D2Result{TData}.WithTraceId"/>. The transform is the
/// auto-injection seam used by <c>BaseHandler.RunCorePipelineAsync</c>;
/// any defect here ripples into every handler boundary.
/// </summary>
public sealed class D2ResultWithTraceIdTests
{
    private const string _ORIGINAL_TRACE = "trace-original";
    private const string _NEW_TRACE = "trace-new";

    // ----------------------------------------------------------------------
    // Immutability — the original instance MUST be untouched
    // ----------------------------------------------------------------------

    [Fact]
    public void WithTraceId_DoesNotMutateOriginal()
    {
        // Adversarial: the source impl uses `new D2Result(...)` to clone.
        // If a future refactor accidentally swaps to a setter / mutation,
        // the original instance must NOT change. D2Result is documented as
        // immutable; that contract has to hold for the auto-injection
        // pattern to be safe under shared references.
        var original = D2Result.Ok();

        var clone = original.WithTraceId(_NEW_TRACE);

        original.TraceId.Should().BeNull();
        clone.TraceId.Should().Be(_NEW_TRACE);
        clone.Should().NotBeSameAs(original);
    }

    [Fact]
    public void WithTraceId_Generic_DoesNotMutateOriginal()
    {
        var original = D2Result<int>.Ok(42);

        var clone = original.WithTraceId(_NEW_TRACE);

        original.TraceId.Should().BeNull();
        clone.TraceId.Should().Be(_NEW_TRACE);
        clone.Should().NotBeSameAs(original);
    }

    // ----------------------------------------------------------------------
    // Field preservation — every other property must round-trip identically
    // ----------------------------------------------------------------------

    [Fact]
    public void WithTraceId_PreservesSuccessFlag()
    {
        D2Result.Ok().WithTraceId(_NEW_TRACE).Success.Should().BeTrue();
        D2Result.NotFound().WithTraceId(_NEW_TRACE).Success.Should().BeFalse();
    }

    [Fact]
    public void WithTraceId_PreservesStatusCode()
    {
        // Cover several factory-driven status codes.
        D2Result.Ok().WithTraceId(_NEW_TRACE).StatusCode.Should().Be(HttpStatusCode.OK);
        D2Result.Created().WithTraceId(_NEW_TRACE).StatusCode.Should().Be(HttpStatusCode.Created);
        D2Result.NotFound().WithTraceId(_NEW_TRACE).StatusCode.Should().Be(HttpStatusCode.NotFound);
        D2Result.Conflict().WithTraceId(_NEW_TRACE).StatusCode.Should().Be(HttpStatusCode.Conflict);
        D2Result.Forbidden().WithTraceId(_NEW_TRACE).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public void WithTraceId_PreservesErrorCode()
    {
        var result = D2Result.NotFound();
        var original_error_code = result.ErrorCode;

        var transformed = result.WithTraceId(_NEW_TRACE);

        transformed.ErrorCode.Should().Be(original_error_code);
        transformed.ErrorCode.Should().Be(ErrorCodes.NOT_FOUND);
    }

    [Fact]
    public void WithTraceId_PreservesMessages()
    {
        // Verify the messages list survives by-value through the transform.
        var result = D2Result.NotFound();
        var original_message_count = result.Messages.Count;

        var transformed = result.WithTraceId(_NEW_TRACE);

        transformed.Messages.Should().HaveCount(original_message_count);
        transformed.Messages.Should().BeEquivalentTo(result.Messages);
    }

    [Fact]
    public void WithTraceId_PreservesInputErrors()
    {
        // Adversarial: ValidationFailed-with-InputErrors is the most
        // common failure shape that flows through Combine/aggregate paths.
        // Losing InputErrors on a TraceId injection would silently drop
        // per-field error context.
        var input_errors = new[]
        {
            new InputError("email", [TK.Common.Errors.UNKNOWN]),
            new InputError("phone", [TK.Common.Errors.VALIDATION_FAILED]),
        };
        var result = D2Result.ValidationFailed(inputErrors: input_errors);

        var transformed = result.WithTraceId(_NEW_TRACE);

        transformed.InputErrors.Should().HaveCount(2);
        transformed.InputErrors.Should().BeEquivalentTo(result.InputErrors);
    }

    [Fact]
    public void WithTraceId_Generic_PreservesData()
    {
        var data = new Sample();
        var result = D2Result<Sample>.Ok(data);

        var transformed = result.WithTraceId(_NEW_TRACE);

        transformed.Data.Should().BeSameAs(data);
    }

    [Fact]
    public void WithTraceId_Generic_PreservesDefaultDataOnFailure()
    {
        // Failure factories on D2Result<T> set Data = default. WithTraceId
        // must preserve that.
        var result = D2Result<Sample>.NotFound();

        var transformed = result.WithTraceId(_NEW_TRACE);

        transformed.Data.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // TraceId arg — null / empty / whitespace / replacement matrix
    // ----------------------------------------------------------------------

    [Fact]
    public void WithTraceId_NullArg_ResultTraceIdBecomesNull()
    {
        // Strip a TraceId by passing null. Useful for redaction / privacy
        // boundaries (e.g. before logging cross-tenant).
        var result = D2Result.Ok().WithTraceId(_ORIGINAL_TRACE);

        var stripped = result.WithTraceId(null);

        stripped.TraceId.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void WithTraceId_EmptyOrWhitespaceArg_PreservedAsProvided(string trace)
    {
        // D2Result.TraceId is a transparent string passthrough — it does
        // NOT canonicalize empty/whitespace into null. Document the
        // pass-through behavior so callers know to canonicalize upstream
        // if they want "" → null semantics.
        var result = D2Result.Ok().WithTraceId(trace);

        result.TraceId.Should().Be(trace);
    }

    [Fact]
    public void WithTraceId_OverwritesExistingTraceId()
    {
        var original = D2Result.Ok().WithTraceId(_ORIGINAL_TRACE);

        var overwritten = original.WithTraceId(_NEW_TRACE);

        overwritten.TraceId.Should().Be(_NEW_TRACE);
        original.TraceId.Should().Be(_ORIGINAL_TRACE); // immutability
    }

    [Fact]
    public void WithTraceId_Idempotent_SameTraceIdProducesEqualResult()
    {
        var original = D2Result.Ok().WithTraceId(_NEW_TRACE);

        var same_trace = original.WithTraceId(_NEW_TRACE);

        same_trace.TraceId.Should().Be(_NEW_TRACE);
        same_trace.Should().NotBeSameAs(original); // new instance, but content-equivalent
        same_trace.Success.Should().Be(original.Success);
        same_trace.StatusCode.Should().Be(original.StatusCode);
        same_trace.ErrorCode.Should().Be(original.ErrorCode);
    }

    // ----------------------------------------------------------------------
    // Type narrowing — `new` keyword on D2Result<T>.WithTraceId
    // ----------------------------------------------------------------------

    [Fact]
    public void WithTraceId_GenericInstance_ReturnsGenericNotBaseType()
    {
        // Adversarial: the `new` member-hiding pattern on D2Result<T>
        // means callers that hold a D2Result<T> reference get a
        // D2Result<T> back — NOT D2Result. If the override accidentally
        // gets removed (or is not `new`'d correctly), the static return
        // type silently becomes the base, breaking callers that chain
        // .Data access.
        D2Result<int> original = D2Result<int>.Ok(7);

        var transformed = original.WithTraceId(_NEW_TRACE);

        transformed.Should().BeOfType<D2Result<int>>();
        transformed.Data.Should().Be(7);
    }

    [Fact]
    public void WithTraceId_GenericCalledViaBaseReference_ReturnsBaseType()
    {
        // Adversarial: if a caller has a D2Result base reference to a
        // generic instance and calls WithTraceId, they get the BASE
        // signature (no Data). Documents the `new`-hiding semantics —
        // member resolution is by static reference type, not runtime
        // type. Callers who care about Data must hold the generic
        // reference.
        D2Result base_ref = D2Result<int>.Ok(7);

        var transformed = base_ref.WithTraceId(_NEW_TRACE);

        // Runtime type is still D2Result<int> (the impl creates a new
        // base instance via `new D2Result(...)`, so the returned object
        // is D2Result, NOT D2Result<int>).
        transformed.Should().BeOfType<D2Result>();
    }

    // ----------------------------------------------------------------------
    // Round-trip via the auto-injection use case
    // ----------------------------------------------------------------------

    [Fact]
    public void WithTraceId_AfterFactory_BehavesLikeFactoryWithTraceIdArg()
    {
        // Auto-injection use case: BaseHandler calls .WithTraceId on a
        // result that already came from a factory. The result should be
        // indistinguishable from calling the factory with the traceId
        // arg directly.
        var via_transform = D2Result<int>.Ok(42).WithTraceId(_NEW_TRACE);
        var via_factory = D2Result<int>.Ok(42, traceId: _NEW_TRACE);

        via_transform.Success.Should().Be(via_factory.Success);
        via_transform.Data.Should().Be(via_factory.Data);
        via_transform.TraceId.Should().Be(via_factory.TraceId);
        via_transform.StatusCode.Should().Be(via_factory.StatusCode);
        via_transform.ErrorCode.Should().Be(via_factory.ErrorCode);
        via_transform.Messages.Should().BeEquivalentTo(via_factory.Messages);
        via_transform.InputErrors.Should().BeEquivalentTo(via_factory.InputErrors);
    }

    // Used only as a generic type token for D2Result<Sample>; identity
    // matters for BeSameAs assertions, no field reads needed.
    private sealed class Sample
    {
    }
}
