// -----------------------------------------------------------------------
// <copyright file="D2Result.Generic.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Result;

using System.Net;
using D2.Shared.I18n;

/// <summary>
/// Represents the result of an operation that produces a payload of type
/// <typeparamref name="TData"/>.
/// </summary>
/// <typeparam name="TData">
/// The type of the data returned by the operation.
/// </typeparam>
public sealed partial class D2Result<TData> : D2Result
{
    /// <summary>
    /// Initializes a new instance of the <see cref="D2Result{TData}"/> class.
    /// </summary>
    /// <param name="success">Whether the operation was successful.</param>
    /// <param name="data">The resulting data of the operation. Optional.</param>
    /// <param name="messages">Translation messages related to the operation. Optional.</param>
    /// <param name="inputErrors">Per-field input errors. Optional.</param>
    /// <param name="statusCode">
    /// The <see cref="HttpStatusCode"/> for the operation. Optional.
    /// </param>
    /// <param name="errorCode">A standardized error code. Optional.</param>
    /// <param name="traceId">Trace identifier for correlating logs. Optional.</param>
    public D2Result(
        bool success,
        TData? data = default,
        IReadOnlyList<TKMessage>? messages = null,
        IReadOnlyList<InputError>? inputErrors = null,
        HttpStatusCode? statusCode = null,
        string? errorCode = null,
        string? traceId = null)
        : base(success, messages, inputErrors, statusCode, errorCode, traceId)
    {
        Data = data;
    }

    /// <summary>
    /// Gets the resulting data of the operation, if any.
    /// </summary>
    public TData? Data { get; }

    /// <summary>
    /// Returns <see cref="D2Result.Success"/> while exposing <see cref="Data"/> via the
    /// out parameter. Convenience for inline destructuring at the call site:
    /// <c>if (result.CheckSuccess(out var data)) { … }</c>.
    /// </summary>
    /// <param name="data">Receives <see cref="Data"/> regardless of success.</param>
    /// <returns><see langword="true"/> if the result is successful.</returns>
    public bool CheckSuccess(out TData? data)
    {
        data = Data;
        return Success;
    }

    /// <summary>
    /// Returns <see cref="D2Result.Failed"/> while exposing <see cref="Data"/> via the
    /// out parameter. Useful for partial-success flows (<see cref="ErrorCodes.SOME_FOUND"/>)
    /// where data is still present despite <see cref="D2Result.Success"/> being <c>false</c>.
    /// </summary>
    /// <param name="data">Receives <see cref="Data"/> regardless of failure.</param>
    /// <returns><see langword="true"/> if the result is a failure.</returns>
    public bool CheckFailure(out TData? data)
    {
        data = Data;
        return Failed;
    }

    /// <summary>
    /// Returns a new <see cref="D2Result{TData}"/> with the same shape and
    /// <see cref="Data"/> payload but with the supplied <paramref name="traceId"/>
    /// in place of the original. Used by <c>BaseHandler.RunCorePipelineAsync</c>
    /// to auto-inject the request trace id on every typed result.
    /// </summary>
    /// <param name="traceId">The trace id to attach.</param>
    /// <returns>
    /// A new <see cref="D2Result{TData}"/> with <paramref name="traceId"/> applied.
    /// </returns>
    public new D2Result<TData> WithTraceId(string? traceId)
        => new(Success, Data, Messages, InputErrors, StatusCode, ErrorCode, traceId);
}
