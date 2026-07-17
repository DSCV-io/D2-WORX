// -----------------------------------------------------------------------
// <copyright file="D2ResultDbFactories.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Repo.Abstractions;

using System.Collections.Generic;
using System.Net;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Result;

/// <summary>
/// DB-flavored semantic factories layered on <see cref="D2Result"/> /
/// <see cref="D2Result{TData}"/>. Mirrors the built-in factories
/// (<c>Conflict</c>, <c>NotFound</c>, etc.) but for failures that originate
/// inside a data store. Pair these with the booleans on
/// <see cref="D2ResultDbBooleans"/> so callers can both emit and discriminate
/// typed DB failures.
/// </summary>
/// <remarks>
/// The default messages come from the <c>common_errors_*</c> namespace and
/// are deliberately generic ("This value is already in use"). Handlers that
/// know the constraint name should override and supply a domain-specific
/// <see cref="TKMessage"/> + <see cref="InputError"/> for crisp UX
/// (e.g. <c>auth_errors_EMAIL_ALREADY_TAKEN</c> with
/// <c>new InputError("email", "EMAIL_ALREADY_TAKEN")</c>).
/// </remarks>
public static class D2ResultDbFactories
{
    extension(D2Result)
    {
        /// <summary>
        /// Optimistic-concurrency conflict (HTTP 409, error code
        /// <see cref="DbErrorCodes.CONCURRENCY_CONFLICT"/>).
        /// </summary>
        /// <param name="messages">
        /// Optional translation messages; defaults to
        /// <c>[TK.Common.Errors.CONCURRENCY_CONFLICT]</c>.
        /// </param>
        /// <param name="traceId">Optional trace identifier.</param>
        /// <returns>A concurrency-conflict <see cref="D2Result"/>.</returns>
        public static D2Result ConcurrencyConflict(
            IReadOnlyList<TKMessage>? messages = null,
            string? traceId = null)
            => Build(
                HttpStatusCode.Conflict,
                DbErrorCodes.CONCURRENCY_CONFLICT,
                TK.Common.Errors.CONCURRENCY_CONFLICT,
                messages,
                inputErrors: null,
                traceId);

        /// <summary>
        /// Unique-constraint violation (HTTP 409, error code
        /// <see cref="DbErrorCodes.UNIQUE_VIOLATION"/>).
        /// </summary>
        /// <param name="messages">
        /// Optional translation messages; defaults to
        /// <c>[TK.Common.Errors.UNIQUE_VIOLATION]</c>.
        /// </param>
        /// <param name="inputErrors">Optional per-field input errors.</param>
        /// <param name="traceId">Optional trace identifier.</param>
        /// <returns>A unique-violation <see cref="D2Result"/>.</returns>
        public static D2Result UniqueViolation(
            IReadOnlyList<TKMessage>? messages = null,
            IReadOnlyList<InputError>? inputErrors = null,
            string? traceId = null)
            => Build(
                HttpStatusCode.Conflict,
                DbErrorCodes.UNIQUE_VIOLATION,
                TK.Common.Errors.UNIQUE_VIOLATION,
                messages,
                inputErrors,
                traceId);

        /// <summary>
        /// Foreign-key-constraint violation (HTTP 409, error code
        /// <see cref="DbErrorCodes.FOREIGN_KEY_VIOLATION"/>).
        /// </summary>
        /// <param name="messages">
        /// Optional translation messages; defaults to
        /// <c>[TK.Common.Errors.FOREIGN_KEY_VIOLATION]</c>.
        /// </param>
        /// <param name="inputErrors">Optional per-field input errors.</param>
        /// <param name="traceId">Optional trace identifier.</param>
        /// <returns>A foreign-key-violation <see cref="D2Result"/>.</returns>
        public static D2Result ForeignKeyViolation(
            IReadOnlyList<TKMessage>? messages = null,
            IReadOnlyList<InputError>? inputErrors = null,
            string? traceId = null)
            => Build(
                HttpStatusCode.Conflict,
                DbErrorCodes.FOREIGN_KEY_VIOLATION,
                TK.Common.Errors.FOREIGN_KEY_VIOLATION,
                messages,
                inputErrors,
                traceId);

        /// <summary>
        /// NOT NULL constraint violation (HTTP 400, error code
        /// <see cref="DbErrorCodes.NOT_NULL_VIOLATION"/>).
        /// </summary>
        /// <param name="messages">
        /// Optional translation messages; defaults to
        /// <c>[TK.Common.Errors.NOT_NULL_VIOLATION]</c>.
        /// </param>
        /// <param name="inputErrors">Optional per-field input errors.</param>
        /// <param name="traceId">Optional trace identifier.</param>
        /// <returns>A NOT NULL violation <see cref="D2Result"/>.</returns>
        public static D2Result NotNullViolation(
            IReadOnlyList<TKMessage>? messages = null,
            IReadOnlyList<InputError>? inputErrors = null,
            string? traceId = null)
            => Build(
                HttpStatusCode.BadRequest,
                DbErrorCodes.NOT_NULL_VIOLATION,
                TK.Common.Errors.NOT_NULL_VIOLATION,
                messages,
                inputErrors,
                traceId);

        /// <summary>
        /// CHECK constraint violation (HTTP 400, error code
        /// <see cref="DbErrorCodes.CHECK_VIOLATION"/>).
        /// </summary>
        /// <param name="messages">
        /// Optional translation messages; defaults to
        /// <c>[TK.Common.Errors.CHECK_VIOLATION]</c>.
        /// </param>
        /// <param name="inputErrors">Optional per-field input errors.</param>
        /// <param name="traceId">Optional trace identifier.</param>
        /// <returns>A CHECK violation <see cref="D2Result"/>.</returns>
        public static D2Result CheckViolation(
            IReadOnlyList<TKMessage>? messages = null,
            IReadOnlyList<InputError>? inputErrors = null,
            string? traceId = null)
            => Build(
                HttpStatusCode.BadRequest,
                DbErrorCodes.CHECK_VIOLATION,
                TK.Common.Errors.CHECK_VIOLATION,
                messages,
                inputErrors,
                traceId);

        /// <summary>
        /// Server-side statement timeout (HTTP 503, error code
        /// <see cref="DbErrorCodes.DB_TIMEOUT"/>).
        /// </summary>
        /// <param name="messages">
        /// Optional translation messages; defaults to
        /// <c>[TK.Common.Errors.DB_TIMEOUT]</c>.
        /// </param>
        /// <param name="traceId">Optional trace identifier.</param>
        /// <returns>A DB-timeout <see cref="D2Result"/>.</returns>
        public static D2Result DbTimeout(
            IReadOnlyList<TKMessage>? messages = null,
            string? traceId = null)
            => Build(
                HttpStatusCode.ServiceUnavailable,
                DbErrorCodes.DB_TIMEOUT,
                TK.Common.Errors.DB_TIMEOUT,
                messages,
                inputErrors: null,
                traceId);

        /// <summary>
        /// Deadlock / serialization failure (HTTP 409, error code
        /// <see cref="DbErrorCodes.DB_DEADLOCK"/>). Caller may retry the
        /// whole operation.
        /// </summary>
        /// <param name="messages">
        /// Optional translation messages; defaults to
        /// <c>[TK.Common.Errors.DB_DEADLOCK]</c>.
        /// </param>
        /// <param name="traceId">Optional trace identifier.</param>
        /// <returns>A deadlock <see cref="D2Result"/>.</returns>
        public static D2Result DbDeadlock(
            IReadOnlyList<TKMessage>? messages = null,
            string? traceId = null)
            => Build(
                HttpStatusCode.Conflict,
                DbErrorCodes.DB_DEADLOCK,
                TK.Common.Errors.DB_DEADLOCK,
                messages,
                inputErrors: null,
                traceId);

        /// <summary>
        /// Connection-level failure (HTTP 503, error code
        /// <see cref="DbErrorCodes.DB_CONNECTION_FAILURE"/>).
        /// </summary>
        /// <param name="messages">
        /// Optional translation messages; defaults to
        /// <c>[TK.Common.Errors.DB_CONNECTION_FAILURE]</c>.
        /// </param>
        /// <param name="traceId">Optional trace identifier.</param>
        /// <returns>A connection-failure <see cref="D2Result"/>.</returns>
        public static D2Result DbConnectionFailure(
            IReadOnlyList<TKMessage>? messages = null,
            string? traceId = null)
            => Build(
                HttpStatusCode.ServiceUnavailable,
                DbErrorCodes.DB_CONNECTION_FAILURE,
                TK.Common.Errors.DB_CONNECTION_FAILURE,
                messages,
                inputErrors: null,
                traceId);
    }

    private static D2Result Build(
        HttpStatusCode status,
        string errorCode,
        TKMessage defaultMessage,
        IReadOnlyList<TKMessage>? messages,
        IReadOnlyList<InputError>? inputErrors,
        string? traceId)
        => D2Result.Fail(
            messages: messages ?? [defaultMessage],
            statusCode: status,
            inputErrors: inputErrors,
            errorCode: errorCode,
            traceId: traceId);
}
