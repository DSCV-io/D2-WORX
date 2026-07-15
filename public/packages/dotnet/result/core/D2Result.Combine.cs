// -----------------------------------------------------------------------
// <copyright file="D2Result.Combine.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result;

using System.Collections.Generic;
using System.Linq;
using DcsvIo.D2.I18n;

/// <summary>
/// Aggregator for combining multiple typed <see cref="D2Result{T}"/> instances
/// into one — handy for fan-out scenarios (parallel sub-handlers, multi-field
/// smart-constructor validation). All-success returns the combined payload;
/// any-failure aggregates messages + input errors into a
/// <see cref="D2Result{T}.ValidationFailed"/>.
/// </summary>
/// <remarks>
/// <b>Designed for smart-constructor multi-field validation aggregation</b>
/// — every input is expected to be either <c>Ok</c> or
/// <c>ValidationFailed</c>-equivalent. Combining heterogeneous failure
/// types (e.g. <c>UniqueViolation</c> + <c>NotFound</c> + raw
/// <c>Forbidden</c>) <b>collapses every failure into a single
/// <c>ValidationFailed</c></b>; the per-input typed error code (<c>IsUniqueViolation</c>,
/// <c>IsNotFound</c>, <c>IsForbidden</c>, etc.) is NOT preserved on the
/// aggregated output. If a caller needs to propagate a single typed
/// failure unchanged, use <c>BubbleFail</c> on that one result instead of
/// running it through <c>Combine</c>.
/// </remarks>
public partial class D2Result
{
    /// <summary>
    /// Combines two typed results. All-success → tuple payload; any-failure →
    /// aggregated <see cref="D2Result{T}.ValidationFailed"/>.
    /// </summary>
    /// <typeparam name="T1">Type of <paramref name="r1"/>'s payload.</typeparam>
    /// <typeparam name="T2">Type of <paramref name="r2"/>'s payload.</typeparam>
    /// <param name="r1">First result.</param>
    /// <param name="r2">Second result.</param>
    /// <returns>Combined result.</returns>
    public static D2Result<(T1 Item1, T2 Item2)> Combine<T1, T2>(
        D2Result<T1> r1,
        D2Result<T2> r2)
    {
        if (r1.Success && r2.Success)
        {
            return D2Result<(T1 Item1, T2 Item2)>.Ok(
                (r1.Data!, r2.Data!),
                traceId: FirstTraceId(r1, r2));
        }

        return AggregateFailure<(T1, T2)>([r1, r2]);
    }

    /// <summary>Combines three typed results.</summary>
    /// <typeparam name="T1">Type of r1's payload.</typeparam>
    /// <typeparam name="T2">Type of r2's payload.</typeparam>
    /// <typeparam name="T3">Type of r3's payload.</typeparam>
    /// <param name="r1">First result.</param>
    /// <param name="r2">Second result.</param>
    /// <param name="r3">Third result.</param>
    /// <returns>Combined result.</returns>
    public static D2Result<(T1 Item1, T2 Item2, T3 Item3)> Combine<T1, T2, T3>(
        D2Result<T1> r1,
        D2Result<T2> r2,
        D2Result<T3> r3)
    {
        if (r1.Success && r2.Success && r3.Success)
        {
            return D2Result<(T1 Item1, T2 Item2, T3 Item3)>.Ok(
                (r1.Data!, r2.Data!, r3.Data!),
                traceId: FirstTraceId(r1, r2, r3));
        }

        return AggregateFailure<(T1, T2, T3)>([r1, r2, r3]);
    }

    /// <summary>Combines four typed results.</summary>
    /// <typeparam name="T1">Type of r1's payload.</typeparam>
    /// <typeparam name="T2">Type of r2's payload.</typeparam>
    /// <typeparam name="T3">Type of r3's payload.</typeparam>
    /// <typeparam name="T4">Type of r4's payload.</typeparam>
    /// <param name="r1">First result.</param>
    /// <param name="r2">Second result.</param>
    /// <param name="r3">Third result.</param>
    /// <param name="r4">Fourth result.</param>
    /// <returns>Combined result.</returns>
    public static D2Result<(T1 Item1, T2 Item2, T3 Item3, T4 Item4)> Combine<T1, T2, T3, T4>(
        D2Result<T1> r1,
        D2Result<T2> r2,
        D2Result<T3> r3,
        D2Result<T4> r4)
    {
        if (r1.Success && r2.Success && r3.Success && r4.Success)
        {
            return D2Result<(T1 Item1, T2 Item2, T3 Item3, T4 Item4)>.Ok(
                (r1.Data!, r2.Data!, r3.Data!, r4.Data!),
                traceId: FirstTraceId(r1, r2, r3, r4));
        }

        return AggregateFailure<(T1, T2, T3, T4)>([r1, r2, r3, r4]);
    }

    /// <summary>Combines five typed results.</summary>
    /// <typeparam name="T1">Type of r1's payload.</typeparam>
    /// <typeparam name="T2">Type of r2's payload.</typeparam>
    /// <typeparam name="T3">Type of r3's payload.</typeparam>
    /// <typeparam name="T4">Type of r4's payload.</typeparam>
    /// <typeparam name="T5">Type of r5's payload.</typeparam>
    /// <param name="r1">First result.</param>
    /// <param name="r2">Second result.</param>
    /// <param name="r3">Third result.</param>
    /// <param name="r4">Fourth result.</param>
    /// <param name="r5">Fifth result.</param>
    /// <returns>Combined result.</returns>
    public static D2Result<(T1 Item1, T2 Item2, T3 Item3, T4 Item4, T5 Item5)>
        Combine<T1, T2, T3, T4, T5>(
            D2Result<T1> r1,
            D2Result<T2> r2,
            D2Result<T3> r3,
            D2Result<T4> r4,
            D2Result<T5> r5)
    {
        if (r1.Success && r2.Success && r3.Success && r4.Success && r5.Success)
        {
            return D2Result<(T1 Item1, T2 Item2, T3 Item3, T4 Item4, T5 Item5)>.Ok(
                (r1.Data!, r2.Data!, r3.Data!, r4.Data!, r5.Data!),
                traceId: FirstTraceId(r1, r2, r3, r4, r5));
        }

        return AggregateFailure<(T1, T2, T3, T4, T5)>([r1, r2, r3, r4, r5]);
    }

    /// <summary>
    /// Combines an arbitrary collection of typed results into a
    /// <see cref="D2Result{T}"/> of an <see cref="IReadOnlyList{T}"/> of payloads.
    /// </summary>
    /// <typeparam name="T">Type of the payloads.</typeparam>
    /// <param name="results">Results to combine.</param>
    /// <returns>
    /// All-success → <c>D2Result&lt;T&gt;.Ok</c> with the unwrapped payload list.
    /// Any-failure → aggregated <see cref="D2Result{T}.ValidationFailed"/>.
    /// Empty input → <c>D2Result&lt;T&gt;.Ok</c> with an empty list.
    /// </returns>
    public static D2Result<IReadOnlyList<T>> Combine<T>(IEnumerable<D2Result<T>> results)
    {
        var materialized = results as IReadOnlyList<D2Result<T>> ?? results.ToList();
        if (materialized.Count == 0)
            return D2Result<IReadOnlyList<T>>.Ok([]);

        if (materialized.All(r => r.Success))
        {
            var payloads = new List<T>(materialized.Count);
            foreach (var r in materialized)
                payloads.Add(r.Data!);

            return D2Result<IReadOnlyList<T>>.Ok(
                payloads,
                traceId: FirstTraceId(materialized));
        }

        return AggregateFailure<IReadOnlyList<T>>(materialized);
    }

    private static string? FirstTraceId(params D2Result[] results)
    {
        foreach (var r in results)
        {
            if (r.TraceId is not null)
                return r.TraceId;
        }

        return null;
    }

    private static string? FirstTraceId(IEnumerable<D2Result> results)
    {
        foreach (var r in results)
        {
            if (r.TraceId is not null)
                return r.TraceId;
        }

        return null;
    }

    private static D2Result<TPayload> AggregateFailure<TPayload>(IEnumerable<D2Result> results)
    {
        var messages = new List<TKMessage>();
        var inputErrors = new List<InputError>();
        string? traceId = null;

        foreach (var r in results)
        {
            if (!r.Success)
            {
                if (r.Messages.Count > 0)
                    messages.AddRange(r.Messages);

                if (r.InputErrors.Count > 0)
                    inputErrors.AddRange(r.InputErrors);
            }

            traceId ??= r.TraceId;
        }

        return D2Result<TPayload>.ValidationFailed(
            messages: messages.Count > 0 ? messages : null,
            inputErrors: inputErrors.Count > 0 ? inputErrors : null,
            traceId: traceId);
    }
}
