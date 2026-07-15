// -----------------------------------------------------------------------
// <copyright file="D2Result.Generic.Factories.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result;

using System.Net;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.I18n;

/// <summary>
/// Hand-rolled factory methods on <see cref="D2Result{TData}"/> that are NOT
/// derived from the error-code spec — the success / raw / propagation /
/// data-carrying factories (<see cref="Ok"/> / <see cref="Created"/> /
/// <see cref="Fail"/> / <see cref="BubbleFail"/> / <see cref="Bubble"/> /
/// <see cref="SomeFound"/> / <see cref="PartialSuccess"/>). The spec-derived
/// semantic failure factories (the typed twins of <c>NotFound</c> / … carrying
/// <c>default</c> data) are generated onto the same partial class — see
/// <c>D2Result.Generic.Factories.g.cs</c>.
/// </summary>
public sealed partial class D2Result<TData>
{
    /// <summary>
    /// Creates a successful result with optional <paramref name="data"/>.
    /// </summary>
    /// <param name="data">Optional payload.</param>
    /// <param name="messages">Optional translation messages.</param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A successful <see cref="D2Result{TData}"/>.</returns>
    public static D2Result<TData> Ok(
        TData? data = default,
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
        => new(true, data, messages, traceId: traceId);

    /// <summary>
    /// Creates a successful result with HTTP status <see cref="HttpStatusCode.Created"/>.
    /// </summary>
    /// <param name="data">Optional payload (typically the newly-created resource).</param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A created <see cref="D2Result{TData}"/>.</returns>
    public static D2Result<TData> Created(TData? data = default, string? traceId = null)
        => new(true, data, statusCode: HttpStatusCode.Created, traceId: traceId);

    /// <summary>
    /// Creates a failure result. Use only when no semantic factory matches.
    /// </summary>
    /// <param name="messages">Optional translation messages.</param>
    /// <param name="statusCode">
    /// Optional <see cref="HttpStatusCode"/>; defaults to
    /// <see cref="HttpStatusCode.BadRequest"/>.
    /// </param>
    /// <param name="inputErrors">Optional per-field input errors.</param>
    /// <param name="errorCode">Optional standardized error code.</param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A failure <see cref="D2Result{TData}"/>.</returns>
    public static new D2Result<TData> Fail(
        IReadOnlyList<TKMessage>? messages = null,
        HttpStatusCode? statusCode = null,
        IReadOnlyList<InputError>? inputErrors = null,
        string? errorCode = null,
        string? traceId = null)
        => new(false, default, messages, inputErrors, statusCode, errorCode, traceId);

    /// <summary>
    /// Propagates a failed upstream <see cref="D2Result"/> into a typed
    /// <see cref="D2Result{TData}"/>, preserving the upstream messages, input errors,
    /// status code, error code, and trace ID. <see cref="Data"/> is set to <c>default</c>.
    /// </summary>
    /// <param name="d2Result">The upstream result to propagate.</param>
    /// <returns>A typed <see cref="D2Result{TData}"/> mirroring the upstream failure.</returns>
    public static D2Result<TData> BubbleFail(D2Result d2Result)
        => new(
            false,
            default,
            d2Result.Messages,
            d2Result.InputErrors,
            d2Result.StatusCode,
            d2Result.ErrorCode,
            d2Result.TraceId,
            d2Result.Category);

    /// <summary>
    /// Propagates an upstream <see cref="D2Result"/> (success OR failure) into a typed
    /// <see cref="D2Result{TData}"/>, preserving its <see cref="D2Result.Success"/>
    /// flag and all metadata, with the supplied <paramref name="data"/> attached.
    /// </summary>
    /// <param name="d2Result">The upstream result to propagate.</param>
    /// <param name="data">Optional payload to attach.</param>
    /// <returns>A typed <see cref="D2Result{TData}"/> mirroring the upstream.</returns>
    public static D2Result<TData> Bubble(D2Result d2Result, TData? data = default)
        => new(
            d2Result.Success,
            data,
            d2Result.Messages,
            d2Result.InputErrors,
            d2Result.StatusCode,
            d2Result.ErrorCode,
            d2Result.TraceId,
            d2Result.Category);

    /// <summary>
    /// Creates a partial-success result (HTTP 206, error code
    /// <see cref="ErrorCodes.SOME_FOUND"/>) carrying the partial
    /// <paramref name="data"/>. <see cref="D2Result.Success"/> is <c>false</c> on the
    /// partial-success ladder (NOT_FOUND → SOME_FOUND → OK) — only fully-found
    /// queries succeed.
    /// </summary>
    /// <param name="data">Optional partial payload.</param>
    /// <param name="messages">
    /// Optional translation messages; defaults to <c>[TK.Common.Errors.SOME_FOUND]</c>.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A partial-success <see cref="D2Result{TData}"/>.</returns>
    public static D2Result<TData> SomeFound(
        TData? data = default,
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.SOME_FOUND];
        return new(
            false,
            data,
            messages,
            statusCode: HttpStatusCode.PartialContent,
            errorCode: ErrorCodes.SOME_FOUND,
            traceId: traceId,
            category: ErrorCategory.PartialSuccess);
    }

    /// <summary>
    /// Creates a partial-success result (HTTP 207 Multi-Status, error code
    /// <see cref="ErrorCodes.PARTIAL_SUCCESS"/>) for a multi-target write
    /// where some targets succeeded and others failed.
    /// <see cref="D2Result.Success"/> is <c>true</c> — the operation did
    /// partially succeed. Callers inspect <c>IsPartialSuccess</c> and the
    /// payload to decide on retry / compensation for the failed target(s).
    /// </summary>
    /// <param name="data">Outcome payload describing which targets succeeded.</param>
    /// <param name="messages">
    /// Optional translation messages; defaults to <c>[TK.Common.Errors.PARTIAL_SUCCESS]</c>.
    /// </param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <returns>A partial-success <see cref="D2Result{TData}"/>.</returns>
    public static D2Result<TData> PartialSuccess(
        TData? data = default,
        IReadOnlyList<TKMessage>? messages = null,
        string? traceId = null)
    {
        messages ??= [TK.Common.Errors.PARTIAL_SUCCESS];
        return new(
            true,
            data,
            messages,
            statusCode: HttpStatusCode.MultiStatus,
            errorCode: ErrorCodes.PARTIAL_SUCCESS,
            traceId: traceId,
            category: ErrorCategory.PartialSuccess);
    }
}
