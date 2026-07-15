// -----------------------------------------------------------------------
// <copyright file="D2ResultCombineTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;
using Xunit;

public sealed class D2ResultCombineTests
{
    // ----------------------------------------------------------------------
    // 2-arity
    // ----------------------------------------------------------------------

    [Fact]
    public void Combine2_AllSuccess_ReturnsOkTuple()
    {
        var r1 = D2Result<int>.Ok(7);
        var r2 = D2Result<string>.Ok("hello");

        var combined = D2Result.Combine(r1, r2);

        combined.Success.Should().BeTrue();
        combined.IsOk.Should().BeTrue();
        combined.Data.Item1.Should().Be(7);
        combined.Data.Item2.Should().Be("hello");
    }

    [Fact]
    public void Combine2_OneFailure_ReturnsValidationFailedCollapsed()
    {
        // Adversarial: r1 Ok + r2 NotFound → collapses to ValidationFailed,
        // NotFound's typed code is intentionally NOT preserved (per remarks).
        var r1 = D2Result<int>.Ok(1);
        var r2 = D2Result<string>.NotFound();

        var combined = D2Result.Combine(r1, r2);

        combined.Success.Should().BeFalse();
        combined.IsValidationFailed.Should().BeTrue();
        combined.IsNotFound.Should().BeFalse();
        combined.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        combined.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);
        combined.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.NOT_FOUND.Key);
    }

    [Fact]
    public void Combine2_TupleIsInArgumentOrder_NotIterationOrder()
    {
        // Adversarial: ensure (Item1, Item2) maps to (r1, r2), not the other way.
        var r1 = D2Result<string>.Ok("first");
        var r2 = D2Result<string>.Ok("second");

        var combined = D2Result.Combine(r1, r2);

        combined.Data.Item1.Should().Be("first");
        combined.Data.Item2.Should().Be("second");
    }

    // ----------------------------------------------------------------------
    // 3-arity
    // ----------------------------------------------------------------------

    [Fact]
    public void Combine3_AllSuccess_ReturnsOkTriple()
    {
        var combined = D2Result.Combine(
            D2Result<int>.Ok(1),
            D2Result<string>.Ok("two"),
            D2Result<bool>.Ok(true));

        combined.Success.Should().BeTrue();
        combined.Data.Item1.Should().Be(1);
        combined.Data.Item2.Should().Be("two");
        combined.Data.Item3.Should().BeTrue();
    }

    [Fact]
    public void Combine3_MiddleFailure_AggregatesOnlyFailureMessages()
    {
        var combined = D2Result.Combine(
            D2Result<int>.Ok(1),
            D2Result<string>.Conflict(),
            D2Result<bool>.Ok(true));

        combined.IsValidationFailed.Should().BeTrue();
        combined.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.CONFLICT.Key);
    }

    // ----------------------------------------------------------------------
    // 4-arity
    // ----------------------------------------------------------------------

    [Fact]
    public void Combine4_AllSuccess_ReturnsOkQuad()
    {
        var combined = D2Result.Combine(
            D2Result<int>.Ok(1),
            D2Result<int>.Ok(2),
            D2Result<int>.Ok(3),
            D2Result<int>.Ok(4));

        combined.Success.Should().BeTrue();
        combined.Data.Item1.Should().Be(1);
        combined.Data.Item2.Should().Be(2);
        combined.Data.Item3.Should().Be(3);
        combined.Data.Item4.Should().Be(4);
    }

    [Fact]
    public void Combine4_LastFailure_BubblesAsValidationFailed()
    {
        var combined = D2Result.Combine(
            D2Result<int>.Ok(1),
            D2Result<int>.Ok(2),
            D2Result<int>.Ok(3),
            D2Result<int>.Forbidden());

        combined.IsValidationFailed.Should().BeTrue();
        combined.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.FORBIDDEN.Key);
    }

    // ----------------------------------------------------------------------
    // 5-arity
    // ----------------------------------------------------------------------

    [Fact]
    public void Combine5_AllSuccess_ReturnsOkQuintuple()
    {
        var combined = D2Result.Combine(
            D2Result<int>.Ok(10),
            D2Result<int>.Ok(20),
            D2Result<int>.Ok(30),
            D2Result<int>.Ok(40),
            D2Result<int>.Ok(50));

        combined.Success.Should().BeTrue();
        combined.Data.Item1.Should().Be(10);
        combined.Data.Item2.Should().Be(20);
        combined.Data.Item3.Should().Be(30);
        combined.Data.Item4.Should().Be(40);
        combined.Data.Item5.Should().Be(50);
    }

    [Fact]
    public void Combine5_AllDifferentFailures_CollapseToValidationFailedWithAggregatedMessages()
    {
        // Adversarial: per remarks, Combine collapses heterogeneous failures into a
        // SINGLE ValidationFailed; per-input typed codes (UniqueViolation, NotFound,
        // Forbidden, Conflict, ServiceUnavailable) are NOT preserved on output.
        var r1 = D2Result<int>.Fail(
            messages: [TK.Common.Errors.UNIQUE_VIOLATION],
            statusCode: HttpStatusCode.Conflict,
            errorCode: "UNIQUE_VIOLATION");
        var r2 = D2Result<int>.NotFound();
        var r3 = D2Result<int>.Forbidden();
        var r4 = D2Result<int>.Conflict();
        var r5 = D2Result<int>.ServiceUnavailable();

        var combined = D2Result.Combine(r1, r2, r3, r4, r5);

        combined.IsValidationFailed.Should().BeTrue();
        combined.IsNotFound.Should().BeFalse();
        combined.IsForbidden.Should().BeFalse();
        combined.IsConflict.Should().BeFalse();
        combined.IsServiceUnavailable.Should().BeFalse();
        combined.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        combined.ErrorCode.Should().Be(ErrorCodes.VALIDATION_FAILED);

        // Messages from ALL 5 failures aggregated, in input order.
        combined.Messages.Select(m => m.Key).Should().Equal(
            TK.Common.Errors.UNIQUE_VIOLATION.Key,
            TK.Common.Errors.NOT_FOUND.Key,
            TK.Common.Errors.FORBIDDEN.Key,
            TK.Common.Errors.CONFLICT.Key,
            TK.Common.Errors.SERVICE_UNAVAILABLE.Key);
    }

    [Fact]
    public void Combine5_ThreeSuccessTwoFailure_OnlyFailureMessagesAppearInAggregate()
    {
        // Adversarial: success messages must NOT bleed into the failure aggregate
        // (the source's `if (!r.Success)` gate). And success Data is dropped — it's
        // unrecoverable from the combined result on the failure path.
        var r1 = D2Result<int>.Ok(1, messages: [TK.Common.Errors.NOT_AUTHENTICATED]);
        var r2 = D2Result<int>.Forbidden();
        var r3 = D2Result<int>.Ok(3);
        var r4 = D2Result<int>.Conflict();
        var r5 = D2Result<int>.Ok(5);

        var combined = D2Result.Combine(r1, r2, r3, r4, r5);

        combined.IsValidationFailed.Should().BeTrue();
        combined.Messages.Select(m => m.Key).Should().Equal(
            TK.Common.Errors.FORBIDDEN.Key,
            TK.Common.Errors.CONFLICT.Key);
        combined.Messages.Select(m => m.Key).Should()
            .NotContain(TK.Common.Errors.NOT_AUTHENTICATED.Key);

        // Combined.Data is the tuple type, default(...) on the failure path.
        combined.Data.Should().Be(default((int, int, int, int, int)));
    }

    // ----------------------------------------------------------------------
    // TraceId carry semantics
    // ----------------------------------------------------------------------

    [Fact]
    public void Combine_FirstNonNullTraceIdWins_OnSuccessPath()
    {
        var r1 = D2Result<int>.Ok(1, traceId: null);
        var r2 = D2Result<int>.Ok(2, traceId: "trace-2");
        var r3 = D2Result<int>.Ok(3, traceId: "trace-3");

        var combined = D2Result.Combine(r1, r2, r3);

        combined.TraceId.Should().Be("trace-2");
    }

    [Fact]
    public void Combine_AllNullTraceIds_TraceIdIsNull()
    {
        var combined = D2Result.Combine(
            D2Result<int>.Ok(1),
            D2Result<int>.Ok(2));

        combined.TraceId.Should().BeNull();
    }

    [Fact]
    public void Combine_FirstNonNullTraceIdIsFirst_PicksFirst()
    {
        var r1 = D2Result<int>.Ok(1, traceId: "trace-1");
        var r2 = D2Result<int>.Ok(2, traceId: "trace-2");

        var combined = D2Result.Combine(r1, r2);

        combined.TraceId.Should().Be("trace-1");
    }

    [Fact]
    public void Combine_FirstNonNullTraceIdWins_OnFailurePath_EvenFromSuccessInput()
    {
        // Adversarial: AggregateFailure's traceId loop scans EVERY input (success or
        // failure) for the first non-null. Test that a success input's traceId still
        // contributes when it's the first non-null in iteration order.
        var r1 = D2Result<int>.Ok(1, traceId: "trace-success-first");
        var r2 = D2Result<int>.Forbidden(traceId: "trace-fail-second");

        var combined = D2Result.Combine(r1, r2);

        combined.IsValidationFailed.Should().BeTrue();
        combined.TraceId.Should().Be("trace-success-first");
    }

    // ----------------------------------------------------------------------
    // IEnumerable overload — empty input
    // ----------------------------------------------------------------------

    [Fact]
    public void CombineEnumerable_EmptyArray_ReturnsOkWithEmptyList()
    {
        var combined = D2Result.Combine(Array.Empty<D2Result<int>>());

        combined.Success.Should().BeTrue();
        combined.IsOk.Should().BeTrue();
        combined.Data.Should().NotBeNull();
        combined.Data.Count.Should().Be(0);
    }

    [Fact]
    public void CombineEnumerable_EnumerableEmpty_ReturnsOkWithEmptyList()
    {
        // Adversarial: explicit Enumerable.Empty<T>() (no IReadOnlyList shortcut).
        var combined = D2Result.Combine(Enumerable.Empty<D2Result<int>>());

        combined.Success.Should().BeTrue();
        combined.Data!.Count.Should().Be(0);
    }

    [Fact]
    public void CombineEnumerable_NewEmptyList_ReturnsOkWithEmptyList()
    {
        var combined = D2Result.Combine(new List<D2Result<int>>());

        combined.Success.Should().BeTrue();
        combined.Data!.Count.Should().Be(0);
    }

    // ----------------------------------------------------------------------
    // IEnumerable overload — happy path
    // ----------------------------------------------------------------------

    [Fact]
    public void CombineEnumerable_AllSuccess_ReturnsOkPayloadListInInputOrder()
    {
        var input = new[]
        {
            D2Result<string>.Ok("alpha"),
            D2Result<string>.Ok("beta"),
            D2Result<string>.Ok("gamma"),
        };

        var combined = D2Result.Combine(input);

        combined.Success.Should().BeTrue();
        combined.Data!.Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public void CombineEnumerable_TraceIdFirstNonNullWins()
    {
        var input = new[]
        {
            D2Result<int>.Ok(1, traceId: null),
            D2Result<int>.Ok(2, traceId: "trace-second"),
            D2Result<int>.Ok(3, traceId: "trace-third"),
        };

        var combined = D2Result.Combine(input);

        combined.TraceId.Should().Be("trace-second");
    }

    [Fact]
    public void CombineEnumerable_AllNullTraceIds_TraceIdIsNull()
    {
        var combined = D2Result.Combine(new[]
        {
            D2Result<int>.Ok(1),
            D2Result<int>.Ok(2),
        });

        combined.TraceId.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // IEnumerable overload — failures
    // ----------------------------------------------------------------------

    [Fact]
    public void CombineEnumerable_OneFailureAmongFive_ReturnsValidationFailedWithThatMessage()
    {
        var input = new[]
        {
            D2Result<int>.Ok(1),
            D2Result<int>.Ok(2),
            D2Result<int>.NotFound(),
            D2Result<int>.Ok(4),
            D2Result<int>.Ok(5),
        };

        var combined = D2Result.Combine(input);

        combined.IsValidationFailed.Should().BeTrue();
        combined.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.NOT_FOUND.Key);
    }

    [Fact]
    public void CombineEnumerable_AllFailures_AggregatesAllMessagesAndInputErrors()
    {
        var ie1 = new InputError("emailAddr", [TK.Common.Errors.VALIDATION_FAILED]);
        var ie2 = new InputError("phone", [TK.Common.Errors.VALIDATION_FAILED]);

        var input = new[]
        {
            D2Result<int>.ValidationFailed(
                messages: [TK.Common.Errors.NOT_FOUND],
                inputErrors: [ie1]),
            D2Result<int>.ValidationFailed(
                messages: [TK.Common.Errors.FORBIDDEN],
                inputErrors: [ie2]),
        };

        var combined = D2Result.Combine(input);

        combined.IsValidationFailed.Should().BeTrue();
        combined.Messages.Select(m => m.Key).Should().Equal(
            TK.Common.Errors.NOT_FOUND.Key,
            TK.Common.Errors.FORBIDDEN.Key);
        combined.InputErrors.Should().HaveCount(2);
        combined.InputErrors.Select(e => e.Field).Should().Equal("emailAddr", "phone");
    }

    [Fact]
    public void CombineEnumerable_HundredInputErrors_AllPreserved()
    {
        // Adversarial: a result with 100-element InputErrors must propagate cleanly.
        var bigErrors = Enumerable.Range(0, 100)
            .Select(i => new InputError(
                $"field_{i}",
                [TK.Common.Errors.VALIDATION_FAILED]))
            .ToArray();
        var input = new[]
        {
            D2Result<int>.ValidationFailed(inputErrors: bigErrors),
        };

        var combined = D2Result.Combine(input);

        combined.InputErrors.Should().HaveCount(100);
    }

    [Fact]
    public void CombineEnumerable_FailureWithEmptyInputErrors_ContributesNothingToAggregate()
    {
        // Adversarial: empty InputErrors list MUST NOT add a sentinel entry.
        var input = new[]
        {
            D2Result<int>.ValidationFailed(),
            D2Result<int>.ValidationFailed(),
        };

        var combined = D2Result.Combine(input);

        combined.InputErrors.Should().BeEmpty();
    }

    [Fact]
    public void CombineEnumerable_FailureWithDefaultMessages_PreservesDefaultsInAggregate()
    {
        // Adversarial: ValidationFailed() applies a default message; verify aggregation.
        var input = new[]
        {
            D2Result<int>.ValidationFailed(),
        };

        var combined = D2Result.Combine(input);

        combined.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.VALIDATION_FAILED.Key);
    }

    // ----------------------------------------------------------------------
    // IEnumerable overload — efficiency / iteration semantics
    // ----------------------------------------------------------------------

    [Fact]
    public void CombineEnumerable_BackedByIReadOnlyList_DoesNotMaterializeAgain()
    {
        // Smoke check: an IReadOnlyList backing should hit the `as IReadOnlyList<>` shortcut
        // (no extra ToList()). We can't easily prove "no allocation," but we can prove
        // single enumeration via a generator-like spy further down.
        IReadOnlyList<D2Result<int>> input =
        [
            D2Result<int>.Ok(1),
            D2Result<int>.Ok(2),
        ];

        var combined = D2Result.Combine(input);

        combined.Success.Should().BeTrue();
        combined.Data!.Should().Equal(1, 2);
    }

    [Fact]
    public void CombineEnumerable_DeferredGenerator_MaterializedExactlyOnce()
    {
        // Adversarial: a deferred generator must enumerate cleanly. With the
        // `as IReadOnlyList ?? .ToList()` pattern, the generator is materialized
        // exactly once; subsequent loops walk the materialized list.
        var enumerationCount = 0;

        IEnumerable<D2Result<int>> Generator()
        {
            enumerationCount++;
            yield return D2Result<int>.Ok(1);
            yield return D2Result<int>.Ok(2);
            yield return D2Result<int>.Ok(3);
        }

        var combined = D2Result.Combine(Generator());

        combined.Success.Should().BeTrue();
        combined.Data!.Should().Equal(1, 2, 3);
        enumerationCount.Should().Be(1);
    }

    [Fact]
    public void CombineEnumerable_GeneratorThatThrows_PropagatesException()
    {
        // Adversarial: an enumerator that throws mid-iteration must surface the
        // exception (NOT silently produce a partial result).
        IEnumerable<D2Result<int>> ThrowingGenerator()
        {
            yield return D2Result<int>.Ok(1);
            throw new InvalidOperationException("boom");
        }

        Action act = () => D2Result.Combine(ThrowingGenerator());

        act.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    // ----------------------------------------------------------------------
    // Category always ValidationFailure (cross-runtime parity)
    // ----------------------------------------------------------------------

    [Fact]
    public void Combine_NotFound_Forbidden_CollapsesCategoryToValidationFailure()
    {
        // AggregateFailure always calls ValidationFailed(…),
        // so Category on the aggregated result is ALWAYS ValidationFailure
        // regardless of the input failure categories (not_found, policy_denied, …).
        // Mirrors TS combineMany([notFound(), forbidden()]) → validation_failure.
        var combined = D2Result.Combine(
            D2Result<int>.NotFound(),
            D2Result<int>.Forbidden());

        combined.IsValidationFailed.Should().BeTrue();
        combined.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Combine_SingleNotFound_CategoryIsValidationFailure()
    {
        // A single non-validation failure still collapses to ValidationFailure.
        var combined = D2Result.Combine(
            D2Result<int>.Ok(1),
            D2Result<int>.NotFound());

        combined.IsValidationFailed.Should().BeTrue();
        combined.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void CombineEnumerable_HeterogeneousFailures_CategoryAlwaysValidationFailure()
    {
        // Enumerable overload: NotFound + ServiceUnavailable → ValidationFailure category.
        var input = new[]
        {
            D2Result<int>.NotFound(),
            D2Result<int>.ServiceUnavailable(),
        };

        var combined = D2Result.Combine(input);

        combined.IsValidationFailed.Should().BeTrue();
        combined.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // ----------------------------------------------------------------------
    // IEnumerable overload — null/empty Messages defenses
    // ----------------------------------------------------------------------

    [Fact]
    public void CombineEnumerable_FailureWithEmptyMessages_NoNRE_AndNoExtraEntries()
    {
        // Adversarial: a failure built with explicit empty messages must not add to
        // the aggregate (the source guards `if (r.Messages.Count > 0)`).
        var r1 = D2Result<int>.Fail(messages: []);
        var r2 = D2Result<int>.NotFound();

        var combined = D2Result.Combine(new[] { r1, r2 });

        combined.IsValidationFailed.Should().BeTrue();

        // Only r2's NOT_FOUND message contributes; r1's empty list is dropped.
        combined.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.NOT_FOUND.Key);
    }

    [Fact]
    public void CombineEnumerable_AllFailuresWithEmptyMessages_FallsBackToValidationFailedDefault()
    {
        // Adversarial: when no messages are aggregated, AggregateFailure passes
        // null to ValidationFailed, which substitutes its default message.
        var r1 = D2Result<int>.Fail(messages: []);
        var r2 = D2Result<int>.Fail(messages: []);

        var combined = D2Result.Combine(new[] { r1, r2 });

        combined.IsValidationFailed.Should().BeTrue();
        combined.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.VALIDATION_FAILED.Key);
    }

    // ----------------------------------------------------------------------
    // Return shape — named tuple accessibility
    // ----------------------------------------------------------------------

    [Fact]
    public void Combine2_NamedTupleItemsAccessible()
    {
        var combined = D2Result.Combine(
            D2Result<int>.Ok(1),
            D2Result<string>.Ok("a"));

        combined.Data.Item1.Should().Be(1);
        combined.Data.Item2.Should().Be("a");
    }

    [Fact]
    public void Combine5_NamedTupleAllItemsAccessible()
    {
        var combined = D2Result.Combine(
            D2Result<int>.Ok(1),
            D2Result<int>.Ok(2),
            D2Result<int>.Ok(3),
            D2Result<int>.Ok(4),
            D2Result<int>.Ok(5));

        combined.Data.Item1.Should().Be(1);
        combined.Data.Item2.Should().Be(2);
        combined.Data.Item3.Should().Be(3);
        combined.Data.Item4.Should().Be(4);
        combined.Data.Item5.Should().Be(5);
    }
}
