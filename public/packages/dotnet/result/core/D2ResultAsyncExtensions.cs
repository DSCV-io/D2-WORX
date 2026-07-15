// -----------------------------------------------------------------------
// <copyright file="D2ResultAsyncExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result;

// This file uses the classic `this T target` extension-method parameter style instead of the
// C# 14 `extension(T target) { ... }` block form. The block form fails for these methods due
// to two real C# 14 compiler limitations:
//   - CS1061 extension-resolution failure on nested generic receivers like
//     `ValueTask<D2Result<TData>>` — the new block form does not currently resolve the
//     extension when the receiver type itself is a closed generic wrapping another generic.
//   - CA2012 "ValueTask awaited multiple times" false-positives on fluent chains
//     `seed.BindAsync(...).BindAsync(...)` when expressed via the block form.
// Migration is deferred until C# 15+ resolves the underlying compiler limitations OR until the
// fluent async extension pattern is refactored away.

/// <summary>
/// Async extension methods on <see cref="Task{T}"/> and <see cref="ValueTask{T}"/>
/// wrapping <see cref="D2Result{TData}"/>, enabling fluent monadic chaining across
/// asynchronous handler calls without intermediate <c>await</c>s in the call site.
/// <para>
/// All chaining operators are <b>short-circuiting</b>: if the awaited upstream result
/// is a failure, the continuation is NOT invoked — the failure propagates via
/// <see cref="D2Result{TData}.BubbleFail"/>.
/// </para>
/// </summary>
public static class D2ResultAsyncExtensions
{
    /// <summary>
    /// Awaits the upstream <paramref name="task"/>; on success, invokes
    /// <paramref name="next"/> with the unwrapped payload and awaits its result. On
    /// failure, propagates via <see cref="D2Result{TData}.BubbleFail"/> without
    /// invoking <paramref name="next"/>.
    /// </summary>
    ///
    /// <typeparam name="TData">
    /// The upstream payload type.
    /// </typeparam>
    /// <typeparam name="TNext">
    /// The downstream payload type.
    /// </typeparam>
    /// <param name="task">
    /// The upstream async result.
    /// </param>
    /// <param name="next">
    /// Async continuation invoked only on success.
    /// </param>
    public static async ValueTask<D2Result<TNext>> BindAsync<TData, TNext>(
        this ValueTask<D2Result<TData>> task,
        Func<TData, ValueTask<D2Result<TNext>>> next)
    {
        var result = await task.ConfigureAwait(false);
        return result.Success
            ? await next(result.Data!).ConfigureAwait(false)
            : D2Result<TNext>.BubbleFail(result);
    }

    /// <summary>
    /// <see cref="Task{T}"/>-returning overload of the
    /// <see cref="ValueTask{T}"/> <c>BindAsync</c> above.
    /// </summary>
    ///
    /// <typeparam name="TData">
    /// The upstream payload type.
    /// </typeparam>
    /// <typeparam name="TNext">
    /// The downstream payload type.
    /// </typeparam>
    /// <param name="task">
    /// The upstream async result.
    /// </param>
    /// <param name="next">
    /// Async continuation invoked only on success.
    /// </param>
    public static async Task<D2Result<TNext>> BindAsync<TData, TNext>(
        this Task<D2Result<TData>> task,
        Func<TData, Task<D2Result<TNext>>> next)
    {
        var result = await task.ConfigureAwait(false);
        return result.Success
            ? await next(result.Data!).ConfigureAwait(false)
            : D2Result<TNext>.BubbleFail(result);
    }

    /// <summary>
    /// Awaits the upstream <paramref name="task"/>; on success, applies the synchronous
    /// <paramref name="projection"/> and wraps the result in
    /// <c>D2Result&lt;TNext&gt;.Ok</c>. On failure, propagates via
    /// <see cref="D2Result{TData}.BubbleFail"/> without invoking
    /// <paramref name="projection"/>.
    /// </summary>
    ///
    /// <typeparam name="TData">
    /// The upstream payload type.
    /// </typeparam>
    /// <typeparam name="TNext">
    /// The downstream payload type.
    /// </typeparam>
    /// <param name="task">
    /// The upstream async result.
    /// </param>
    /// <param name="projection">
    /// Pure transformation invoked only on success.
    /// </param>
    public static async ValueTask<D2Result<TNext>> MapAsync<TData, TNext>(
        this ValueTask<D2Result<TData>> task,
        Func<TData, TNext> projection)
    {
        var result = await task.ConfigureAwait(false);
        return result.Success
            ? D2Result<TNext>.Ok(projection(result.Data!))
            : D2Result<TNext>.BubbleFail(result);
    }

    /// <summary>
    /// <see cref="Task{T}"/>-returning overload of
    /// <see cref="MapAsync{TData,TNext}(ValueTask{D2Result{TData}}, Func{TData, TNext})"/>.
    /// </summary>
    ///
    /// <typeparam name="TData">
    /// The upstream payload type.
    /// </typeparam>
    /// <typeparam name="TNext">
    /// The downstream payload type.
    /// </typeparam>
    /// <param name="task">
    /// The upstream async result.
    /// </param>
    /// <param name="projection">
    /// Pure transformation invoked only on success.
    /// </param>
    public static async Task<D2Result<TNext>> MapAsync<TData, TNext>(
        this Task<D2Result<TData>> task,
        Func<TData, TNext> projection)
    {
        var result = await task.ConfigureAwait(false);
        return result.Success
            ? D2Result<TNext>.Ok(projection(result.Data!))
            : D2Result<TNext>.BubbleFail(result);
    }

    /// <summary>
    /// Same-shape async chain: awaits the upstream, then on success awaits
    /// <paramref name="next"/> which produces a result of the same payload type. Sugar
    /// for <c>BindAsync</c> when <c>TData == TNext</c> — useful when state evolves
    /// step-to-step within a single payload type.
    /// </summary>
    ///
    /// <typeparam name="TData">
    /// The payload type carried through both steps.
    /// </typeparam>
    /// <param name="task">
    /// The upstream async result.
    /// </param>
    /// <param name="next">
    /// Async continuation invoked only on success.
    /// </param>
    public static ValueTask<D2Result<TData>> ThenAsync<TData>(
        this ValueTask<D2Result<TData>> task,
        Func<TData, ValueTask<D2Result<TData>>> next) => task.BindAsync(next);

    /// <summary>
    /// <see cref="Task{T}"/>-returning overload of the
    /// <see cref="ValueTask{T}"/> <c>ThenAsync</c> above.
    /// </summary>
    ///
    /// <typeparam name="TData">
    /// The payload type carried through both steps.
    /// </typeparam>
    /// <param name="task">
    /// The upstream async result.
    /// </param>
    /// <param name="next">
    /// Async continuation invoked only on success.
    /// </param>
    public static Task<D2Result<TData>> ThenAsync<TData>(
        this Task<D2Result<TData>> task,
        Func<TData, Task<D2Result<TData>>> next) => task.BindAsync(next);
}
