// -----------------------------------------------------------------------
// <copyright file="D2ResultDbFactoriesTests.cs" company="DCSV">
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
/// Adversarial coverage of the non-generic <see cref="D2Result"/> DB-flavored
/// semantic factories. Each factory MUST emit the documented
/// <see cref="HttpStatusCode"/> + <see cref="DbErrorCodes"/> constant + TK
/// fallback message so the boolean discriminators in
/// <see cref="D2ResultDbBooleans"/> can round-trip the result.
/// </summary>
public sealed class D2ResultDbFactoriesTests
{
    // ----------------------------------------------------------------------
    // ConcurrencyConflict — 409, no inputErrors param
    // ----------------------------------------------------------------------

    [Fact]
    public void ConcurrencyConflict_DefaultMessages_UsesTkFallback()
    {
        var result = D2Result.ConcurrencyConflict();

        result.Failed.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.ErrorCode.Should().Be(DbErrorCodes.CONCURRENCY_CONFLICT);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.CONCURRENCY_CONFLICT.Key);
        result.InputErrors.Should().BeEmpty();
    }

    [Fact]
    public void ConcurrencyConflict_CustomMessages_OverridesDefault()
    {
        var custom_messages = new[] { TK.Common.Errors.CONFLICT };

        var result = D2Result.ConcurrencyConflict(messages: custom_messages);

        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.CONFLICT.Key);
    }

    [Fact]
    public void ConcurrencyConflict_TraceId_ThreadsThrough()
    {
        var result = D2Result.ConcurrencyConflict(traceId: "trace-99");

        result.TraceId.Should().Be("trace-99");
    }

    // ----------------------------------------------------------------------
    // UniqueViolation — 409, supports inputErrors
    // ----------------------------------------------------------------------

    [Fact]
    public void UniqueViolation_DefaultMessages_UsesTkFallback()
    {
        var result = D2Result.UniqueViolation();

        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.ErrorCode.Should().Be(DbErrorCodes.UNIQUE_VIOLATION);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.UNIQUE_VIOLATION.Key);
    }

    [Fact]
    public void UniqueViolation_CustomMessages_OverridesDefault()
    {
        var custom_messages = new[] { TK.Common.Errors.CONFLICT };

        var result = D2Result.UniqueViolation(messages: custom_messages);

        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.CONFLICT.Key);
    }

    [Fact]
    public void UniqueViolation_InputErrors_Propagate()
    {
        var input_errors = new[]
        {
            new InputError("email", [TK.Common.Errors.CONFLICT]),
        };

        var result = D2Result.UniqueViolation(inputErrors: input_errors);

        result.InputErrors.Should().BeSameAs(input_errors);
        result.InputErrors.Should().ContainSingle()
            .Which.Field.Should().Be("email");
    }

    [Fact]
    public void UniqueViolation_TraceId_ThreadsThrough()
    {
        var result = D2Result.UniqueViolation(traceId: "trace-u");

        result.TraceId.Should().Be("trace-u");
    }

    // ----------------------------------------------------------------------
    // ForeignKeyViolation — 409, supports inputErrors
    // ----------------------------------------------------------------------

    [Fact]
    public void ForeignKeyViolation_DefaultMessages_UsesTkFallback()
    {
        var result = D2Result.ForeignKeyViolation();

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

        var result = D2Result.ForeignKeyViolation(inputErrors: input_errors, traceId: "fk-1");

        result.InputErrors.Should().BeSameAs(input_errors);
        result.TraceId.Should().Be("fk-1");
    }

    // ----------------------------------------------------------------------
    // NotNullViolation — 400, supports inputErrors
    // ----------------------------------------------------------------------

    [Fact]
    public void NotNullViolation_DefaultMessages_Are400_AndUseTkFallback()
    {
        // Adversarial: NotNull / Check are NOT 409 (server-side rejection of
        // bad payload, not a state-conflict). Keep status mapping audited.
        var result = D2Result.NotNullViolation();

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

        var result = D2Result.NotNullViolation(inputErrors: input_errors, traceId: "nn-1");

        result.InputErrors.Should().BeSameAs(input_errors);
        result.TraceId.Should().Be("nn-1");
    }

    // ----------------------------------------------------------------------
    // CheckViolation — 400, supports inputErrors
    // ----------------------------------------------------------------------

    [Fact]
    public void CheckViolation_DefaultMessages_Are400_AndUseTkFallback()
    {
        var result = D2Result.CheckViolation();

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

        var result = D2Result.CheckViolation(inputErrors: input_errors, traceId: "ck-1");

        result.InputErrors.Should().BeSameAs(input_errors);
        result.TraceId.Should().Be("ck-1");
    }

    // ----------------------------------------------------------------------
    // DbTimeout — 503, no inputErrors
    // ----------------------------------------------------------------------

    [Fact]
    public void DbTimeout_DefaultMessages_Are503_AndUseTkFallback()
    {
        var result = D2Result.DbTimeout();

        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(DbErrorCodes.DB_TIMEOUT);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.DB_TIMEOUT.Key);
        result.InputErrors.Should().BeEmpty();
    }

    [Fact]
    public void DbTimeout_TraceId_ThreadsThrough()
    {
        var result = D2Result.DbTimeout(traceId: "to-1");

        result.TraceId.Should().Be("to-1");
    }

    // ----------------------------------------------------------------------
    // DbDeadlock — 409, no inputErrors
    // ----------------------------------------------------------------------

    [Fact]
    public void DbDeadlock_DefaultMessages_Are409_AndUseTkFallback()
    {
        // Adversarial: deadlock is 409 (caller can retry the whole txn) —
        // NOT 503 like timeout/connection failure. Surfaces the
        // retry-policy semantics distinction.
        var result = D2Result.DbDeadlock();

        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
        result.ErrorCode.Should().Be(DbErrorCodes.DB_DEADLOCK);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.DB_DEADLOCK.Key);
    }

    [Fact]
    public void DbDeadlock_TraceId_ThreadsThrough()
    {
        var result = D2Result.DbDeadlock(traceId: "dl-1");

        result.TraceId.Should().Be("dl-1");
    }

    // ----------------------------------------------------------------------
    // DbConnectionFailure — 503, no inputErrors
    // ----------------------------------------------------------------------

    [Fact]
    public void DbConnectionFailure_DefaultMessages_Are503_AndUseTkFallback()
    {
        var result = D2Result.DbConnectionFailure();

        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(DbErrorCodes.DB_CONNECTION_FAILURE);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.DB_CONNECTION_FAILURE.Key);
    }

    [Fact]
    public void DbConnectionFailure_TraceId_ThreadsThrough()
    {
        var result = D2Result.DbConnectionFailure(traceId: "cf-1");

        result.TraceId.Should().Be("cf-1");
    }

    // ----------------------------------------------------------------------
    // Adversarial — empty / large messages list
    // ----------------------------------------------------------------------

    [Fact]
    public void UniqueViolation_EmptyMessagesList_IsPreserved_NotReplacedWithFallback()
    {
        // Adversarial: caller passing an EMPTY (non-null) list signals
        // "I want zero messages." The factory uses `?? [default]` which
        // distinguishes null from empty. An empty list MUST be honored —
        // otherwise callers can't suppress the default.
        IReadOnlyList<TKMessage> empty = [];

        var result = D2Result.UniqueViolation(messages: empty);

        result.Messages.Should().BeEmpty();
    }

    [Fact]
    public void UniqueViolation_HundredElementMessagesList_IsPreserved()
    {
        // Adversarial: large lists must round-trip (no implicit truncation /
        // copying-to-smaller-buffer). Detects accidental ToArray-with-cap.
        const int expected_count = 100;
        var many_messages = Enumerable.Range(0, expected_count)
            .Select(_ => TK.Common.Errors.CONFLICT)
            .ToArray();

        var result = D2Result.UniqueViolation(messages: many_messages);

        result.Messages.Should().HaveCount(expected_count);
    }

    [Fact]
    public void ConcurrencyConflict_NullMessages_IsTreatedAsDefault()
    {
        // Adversarial: explicit null vs default. Both must produce the same
        // shape (single TK fallback). Documents the `?? [default]` semantics.
        var explicit_null = D2Result.ConcurrencyConflict(messages: null);
        var defaulted = D2Result.ConcurrencyConflict();

        explicit_null.Messages.Should().BeEquivalentTo(defaulted.Messages);
    }
}
