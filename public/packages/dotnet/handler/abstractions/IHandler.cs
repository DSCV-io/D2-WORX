// -----------------------------------------------------------------------
// <copyright file="IHandler.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Abstractions;

using System.Threading;
using System.Threading.Tasks;
using DcsvIo.D2.Result;

/// <summary>
/// Generic handler contract — every handler-shaped operation in the platform
/// implements this (CQRS handlers, repo handlers, messaging consumers,
/// scheduled jobs, etc.). The concrete implementation typically derives from
/// <c>BaseHandler&lt;TSelf, TInput, TOutput&gt;</c> in <c>DcsvIo.D2.Handler</c>,
/// which provides the full observability pipeline (activity + log scope +
/// stopwatch + universal try/catch + scope/audience pre-check).
/// </summary>
/// <typeparam name="TInput">
/// The input type (typically a record / DTO).
/// </typeparam>
/// <typeparam name="TOutput">
/// The output type (typically a record / DTO; may be a domain entity).
/// </typeparam>
public interface IHandler<in TInput, TOutput>
{
    /// <summary>
    /// Executes the handler against <paramref name="input"/>.
    /// </summary>
    /// <param name="input">The handler input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="options">
    /// Per-call options (logging toggles, scope/audience checks, time
    /// thresholds). When null, the handler's <c>DefaultOptions</c> apply.
    /// </param>
    /// <returns>
    /// The result. <c>TOutput?</c> is nullable because failure paths return
    /// null data.
    /// </returns>
    ValueTask<D2Result<TOutput?>> HandleAsync(
        TInput input,
        CancellationToken ct = default,
        HandlerOptions? options = null);
}
