// -----------------------------------------------------------------------
// <copyright file="D2ResultDbGenericFactories.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Repo.Abstractions;

using System.Collections.Generic;
using System.Net;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;

/// <summary>
/// Generic-typed counterparts to <see cref="D2ResultDbFactories"/>. Each
/// factory mirrors the non-generic shape but produces a typed
/// <see cref="D2Result{TData}"/> with <c>Data = default</c>, ready for
/// handlers to return without additional bubbling.
/// </summary>
public static class D2ResultDbGenericFactories
{
    extension<TData>(D2Result<TData>)
    {
        /// <inheritdoc cref="D2ResultDbFactories.ConcurrencyConflict"/>
        public static D2Result<TData> ConcurrencyConflict(
            IReadOnlyList<TKMessage>? messages = null,
            string? traceId = null)
            => Build<TData>(
                HttpStatusCode.Conflict,
                DbErrorCodes.CONCURRENCY_CONFLICT,
                TK.Common.Errors.CONCURRENCY_CONFLICT,
                messages,
                inputErrors: null,
                traceId);

        /// <inheritdoc cref="D2ResultDbFactories.UniqueViolation"/>
        public static D2Result<TData> UniqueViolation(
            IReadOnlyList<TKMessage>? messages = null,
            IReadOnlyList<InputError>? inputErrors = null,
            string? traceId = null)
            => Build<TData>(
                HttpStatusCode.Conflict,
                DbErrorCodes.UNIQUE_VIOLATION,
                TK.Common.Errors.UNIQUE_VIOLATION,
                messages,
                inputErrors,
                traceId);

        /// <inheritdoc cref="D2ResultDbFactories.ForeignKeyViolation"/>
        public static D2Result<TData> ForeignKeyViolation(
            IReadOnlyList<TKMessage>? messages = null,
            IReadOnlyList<InputError>? inputErrors = null,
            string? traceId = null)
            => Build<TData>(
                HttpStatusCode.Conflict,
                DbErrorCodes.FOREIGN_KEY_VIOLATION,
                TK.Common.Errors.FOREIGN_KEY_VIOLATION,
                messages,
                inputErrors,
                traceId);

        /// <inheritdoc cref="D2ResultDbFactories.NotNullViolation"/>
        public static D2Result<TData> NotNullViolation(
            IReadOnlyList<TKMessage>? messages = null,
            IReadOnlyList<InputError>? inputErrors = null,
            string? traceId = null)
            => Build<TData>(
                HttpStatusCode.BadRequest,
                DbErrorCodes.NOT_NULL_VIOLATION,
                TK.Common.Errors.NOT_NULL_VIOLATION,
                messages,
                inputErrors,
                traceId);

        /// <inheritdoc cref="D2ResultDbFactories.CheckViolation"/>
        public static D2Result<TData> CheckViolation(
            IReadOnlyList<TKMessage>? messages = null,
            IReadOnlyList<InputError>? inputErrors = null,
            string? traceId = null)
            => Build<TData>(
                HttpStatusCode.BadRequest,
                DbErrorCodes.CHECK_VIOLATION,
                TK.Common.Errors.CHECK_VIOLATION,
                messages,
                inputErrors,
                traceId);

        /// <inheritdoc cref="D2ResultDbFactories.DbTimeout"/>
        public static D2Result<TData> DbTimeout(
            IReadOnlyList<TKMessage>? messages = null,
            string? traceId = null)
            => Build<TData>(
                HttpStatusCode.ServiceUnavailable,
                DbErrorCodes.DB_TIMEOUT,
                TK.Common.Errors.DB_TIMEOUT,
                messages,
                inputErrors: null,
                traceId);

        /// <inheritdoc cref="D2ResultDbFactories.DbDeadlock"/>
        public static D2Result<TData> DbDeadlock(
            IReadOnlyList<TKMessage>? messages = null,
            string? traceId = null)
            => Build<TData>(
                HttpStatusCode.Conflict,
                DbErrorCodes.DB_DEADLOCK,
                TK.Common.Errors.DB_DEADLOCK,
                messages,
                inputErrors: null,
                traceId);

        /// <inheritdoc cref="D2ResultDbFactories.DbConnectionFailure"/>
        public static D2Result<TData> DbConnectionFailure(
            IReadOnlyList<TKMessage>? messages = null,
            string? traceId = null)
            => Build<TData>(
                HttpStatusCode.ServiceUnavailable,
                DbErrorCodes.DB_CONNECTION_FAILURE,
                TK.Common.Errors.DB_CONNECTION_FAILURE,
                messages,
                inputErrors: null,
                traceId);
    }

    private static D2Result<TData> Build<TData>(
        HttpStatusCode status,
        string errorCode,
        TKMessage defaultMessage,
        IReadOnlyList<TKMessage>? messages,
        IReadOnlyList<InputError>? inputErrors,
        string? traceId)
        => D2Result<TData>.Fail(
            messages: messages ?? [defaultMessage],
            statusCode: status,
            inputErrors: inputErrors,
            errorCode: errorCode,
            traceId: traceId);
}
