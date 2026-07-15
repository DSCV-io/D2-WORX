// -----------------------------------------------------------------------
// <copyright file="D2Result.Generic.Monadic.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result;

/// <summary>
/// Monadic instance methods on <see cref="D2Result{TData}"/> for composing pipelines
/// where state flows step-to-step. Use these for genuine linear flows (sign-in, file
/// processing, risk scoring); for the workhorse "guard against failure and continue
/// with locals" pattern, prefer <c>BubbleOnFailure</c> from
/// <see cref="D2ResultGuardExtensions"/>.
/// </summary>
public sealed partial class D2Result<TData>
{
    /// <summary>
    /// Monadic bind. If this result is successful, invokes <paramref name="next"/>
    /// with <see cref="Data"/> and returns its result. If this result failed, returns
    /// a <see cref="BubbleFail"/> of this result with the new payload type —
    /// <paramref name="next"/> is NOT invoked (short-circuits).
    /// </summary>
    ///
    /// <typeparam name="TNext">
    /// The payload type produced by the next step.
    /// </typeparam>
    /// <param name="next">
    /// Continuation invoked only on success.
    /// </param>
    public D2Result<TNext> Bind<TNext>(Func<TData, D2Result<TNext>> next) =>
        Success ? next(Data!) : D2Result<TNext>.BubbleFail(this);

    /// <summary>
    /// Functor map. If this result is successful, projects <see cref="Data"/> through
    /// <paramref name="projection"/> and wraps it in a new <c>D2Result&lt;TNext&gt;.Ok</c>.
    /// If this result failed, returns a <see cref="BubbleFail"/> of this result —
    /// <paramref name="projection"/> is NOT invoked.
    /// </summary>
    ///
    /// <typeparam name="TNext">
    /// The payload type produced by the projection.
    /// </typeparam>
    /// <param name="projection">
    /// Pure transformation invoked only on success.
    /// </param>
    public D2Result<TNext> Map<TNext>(Func<TData, TNext> projection) =>
        Success ? D2Result<TNext>.Ok(projection(Data!)) : D2Result<TNext>.BubbleFail(this);

    /// <summary>
    /// Pattern match. Invokes <paramref name="onSuccess"/> with <see cref="Data"/> if
    /// successful, otherwise invokes <paramref name="onFailure"/> with the failed
    /// result so the caller can inspect status code / error code / messages.
    /// </summary>
    ///
    /// <typeparam name="TResult">
    /// The unified return type from both branches.
    /// </typeparam>
    /// <param name="onSuccess">
    /// Branch invoked on success with the unwrapped payload.
    /// </param>
    /// <param name="onFailure">
    /// Branch invoked on failure with the failed result.
    /// </param>
    public TResult Match<TResult>(
        Func<TData, TResult> onSuccess,
        Func<D2Result<TData>, TResult> onFailure) =>
        Success ? onSuccess(Data!) : onFailure(this);
}
