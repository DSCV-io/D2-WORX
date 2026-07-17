// -----------------------------------------------------------------------
// <copyright file="D2ResultDbGenericFactoriesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.HandlerRepo.Abstractions;

using System.Collections.Generic;
using System.Linq;
using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.Handler.Repo.Abstractions;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Adversarial coverage of the generic <see cref="D2Result{TData}"/> DB
/// factories. Identical matrix to the non-generic factories — every factory
/// MUST produce a result whose <c>Data</c> is <see langword="default"/> (so
/// callers don't accidentally read uninitialized state on failure).
/// </summary>
public sealed class D2ResultDbGenericFactoriesTests
{
    // ----------------------------------------------------------------------
    // ConcurrencyConflict<TData>
    // ----------------------------------------------------------------------

    [Fact]
    public void ConcurrencyConflict_DefaultMessages_DataIsDefault_AndUsesTkFallback()
    {
        var result = D2Result<SamplePayload>.ConcurrencyConflict();

        result.Failed.Should().BeTrue();
        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.ErrorCode.Should().Be(DbErrorCodes.CONCURRENCY_CONFLICT);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.CONCURRENCY_CONFLICT.Key);
    }

    [Fact]
    public void ConcurrencyConflict_ValueType_DataIsDefault()
    {
        // Adversarial: value-type TData. `default(int)` is 0; verify the
        // generic factory does NOT box up a typed null but returns the
        // language default.
        var result = D2Result<int>.ConcurrencyConflict();

        result.Data.Should().Be(0);
    }

    [Fact]
    public void ConcurrencyConflict_TraceId_ThreadsThrough()
    {
        var result = D2Result<SamplePayload>.ConcurrencyConflict(traceId: "g-cc-1");

        result.TraceId.Should().Be("g-cc-1");
    }

    // ----------------------------------------------------------------------
    // UniqueViolation<TData>
    // ----------------------------------------------------------------------

    [Fact]
    public void UniqueViolation_DefaultMessages_DataIsDefault_AndUsesTkFallback()
    {
        var result = D2Result<SamplePayload>.UniqueViolation();

        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.ErrorCode.Should().Be(DbErrorCodes.UNIQUE_VIOLATION);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.UNIQUE_VIOLATION.Key);
    }

    [Fact]
    public void UniqueViolation_CustomMessagesAndInputErrorsAndTraceId_Propagate()
    {
        var custom_messages = new[] { TK.Common.Errors.CONFLICT };
        var input_errors = new[]
        {
            new InputError("email", [TK.Common.Errors.CONFLICT]),
        };

        var result = D2Result<SamplePayload>.UniqueViolation(
            messages: custom_messages,
            inputErrors: input_errors,
            traceId: "g-uv-1");

        result.Data.Should().BeNull();
        result.Messages.Should().BeSameAs(custom_messages);
        result.InputErrors.Should().BeSameAs(input_errors);
        result.TraceId.Should().Be("g-uv-1");
    }

    // ----------------------------------------------------------------------
    // ForeignKeyViolation<TData>
    // ----------------------------------------------------------------------

    [Fact]
    public void ForeignKeyViolation_DefaultMessages_DataIsDefault_AndUsesTkFallback()
    {
        var result = D2Result<SamplePayload>.ForeignKeyViolation();

        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.ErrorCode.Should().Be(DbErrorCodes.FOREIGN_KEY_VIOLATION);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.FOREIGN_KEY_VIOLATION.Key);
    }

    [Fact]
    public void ForeignKeyViolation_InputErrorsAndTraceId_Propagate()
    {
        var input_errors = new[]
        {
            new InputError("orgId", [TK.Common.Errors.NOT_FOUND]),
        };

        var result = D2Result<SamplePayload>.ForeignKeyViolation(
            inputErrors: input_errors,
            traceId: "g-fk-1");

        result.Data.Should().BeNull();
        result.InputErrors.Should().BeSameAs(input_errors);
        result.TraceId.Should().Be("g-fk-1");
    }

    // ----------------------------------------------------------------------
    // NotNullViolation<TData>
    // ----------------------------------------------------------------------

    [Fact]
    public void NotNullViolation_DefaultMessages_Are400_AndDataIsDefault()
    {
        var result = D2Result<SamplePayload>.NotNullViolation();

        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(DbErrorCodes.NOT_NULL_VIOLATION);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.NOT_NULL_VIOLATION.Key);
    }

    [Fact]
    public void NotNullViolation_InputErrorsAndTraceId_Propagate()
    {
        var input_errors = new[]
        {
            new InputError("name", [TK.Common.Errors.NOT_FOUND]),
        };

        var result = D2Result<SamplePayload>.NotNullViolation(
            inputErrors: input_errors,
            traceId: "g-nn-1");

        result.Data.Should().BeNull();
        result.InputErrors.Should().BeSameAs(input_errors);
        result.TraceId.Should().Be("g-nn-1");
    }

    // ----------------------------------------------------------------------
    // CheckViolation<TData>
    // ----------------------------------------------------------------------

    [Fact]
    public void CheckViolation_DefaultMessages_Are400_AndDataIsDefault()
    {
        var result = D2Result<SamplePayload>.CheckViolation();

        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(DbErrorCodes.CHECK_VIOLATION);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.CHECK_VIOLATION.Key);
    }

    [Fact]
    public void CheckViolation_InputErrorsAndTraceId_Propagate()
    {
        var input_errors = new[]
        {
            new InputError("age", [TK.Common.Errors.CONFLICT]),
        };

        var result = D2Result<SamplePayload>.CheckViolation(
            inputErrors: input_errors,
            traceId: "g-ck-1");

        result.Data.Should().BeNull();
        result.InputErrors.Should().BeSameAs(input_errors);
        result.TraceId.Should().Be("g-ck-1");
    }

    // ----------------------------------------------------------------------
    // DbTimeout<TData>
    // ----------------------------------------------------------------------

    [Fact]
    public void DbTimeout_DefaultMessages_Are503_AndDataIsDefault()
    {
        var result = D2Result<SamplePayload>.DbTimeout();

        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(DbErrorCodes.DB_TIMEOUT);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.DB_TIMEOUT.Key);
    }

    [Fact]
    public void DbTimeout_TraceId_ThreadsThrough()
    {
        var result = D2Result<SamplePayload>.DbTimeout(traceId: "g-to-1");

        result.TraceId.Should().Be("g-to-1");
    }

    // ----------------------------------------------------------------------
    // DbDeadlock<TData>
    // ----------------------------------------------------------------------

    [Fact]
    public void DbDeadlock_DefaultMessages_Are409_AndDataIsDefault()
    {
        var result = D2Result<SamplePayload>.DbDeadlock();

        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.ErrorCode.Should().Be(DbErrorCodes.DB_DEADLOCK);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.DB_DEADLOCK.Key);
    }

    [Fact]
    public void DbDeadlock_TraceId_ThreadsThrough()
    {
        var result = D2Result<SamplePayload>.DbDeadlock(traceId: "g-dl-1");

        result.TraceId.Should().Be("g-dl-1");
    }

    // ----------------------------------------------------------------------
    // DbConnectionFailure<TData>
    // ----------------------------------------------------------------------

    [Fact]
    public void DbConnectionFailure_DefaultMessages_Are503_AndDataIsDefault()
    {
        var result = D2Result<SamplePayload>.DbConnectionFailure();

        result.Data.Should().BeNull();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(DbErrorCodes.DB_CONNECTION_FAILURE);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.DB_CONNECTION_FAILURE.Key);
    }

    [Fact]
    public void DbConnectionFailure_TraceId_ThreadsThrough()
    {
        var result = D2Result<SamplePayload>.DbConnectionFailure(traceId: "g-cf-1");

        result.TraceId.Should().Be("g-cf-1");
    }

    // ----------------------------------------------------------------------
    // Adversarial — empty / large messages list
    // ----------------------------------------------------------------------

    [Fact]
    public void UniqueViolation_EmptyMessagesList_IsPreserved()
    {
        IReadOnlyList<TKMessage> empty = [];

        var result = D2Result<SamplePayload>.UniqueViolation(messages: empty);

        result.Messages.Should().BeEmpty();
        result.Data.Should().BeNull();
    }

    [Fact]
    public void UniqueViolation_HundredElementMessagesList_IsPreserved()
    {
        const int expected_count = 100;
        var many_messages = Enumerable.Range(0, expected_count)
            .Select(_ => TK.Common.Errors.CONFLICT)
            .ToArray();

        var result = D2Result<SamplePayload>.UniqueViolation(messages: many_messages);

        result.Messages.Should().HaveCount(expected_count);
        result.Data.Should().BeNull();
    }

    private sealed class SamplePayload;
}
