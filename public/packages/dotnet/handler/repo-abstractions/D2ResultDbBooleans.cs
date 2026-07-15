// -----------------------------------------------------------------------
// <copyright file="D2ResultDbBooleans.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Repo.Abstractions;

using DcsvIo.D2.Result;

/// <summary>
/// Per-DB-error-code boolean discriminators on <see cref="D2Result"/>.
/// Mirrors the built-in <c>IsConflict</c> / <c>IsValidationFailed</c> shape
/// in the result lib, scoped to DB failures emitted via
/// <see cref="D2ResultDbFactories"/> /
/// <see cref="D2ResultDbGenericFactories"/>.
/// </summary>
public static class D2ResultDbBooleans
{
    extension(D2Result result)
    {
        /// <summary>
        /// Gets a value indicating whether this result is a concurrency
        /// conflict (<see cref="DbErrorCodes.CONCURRENCY_CONFLICT"/>).
        /// </summary>
        public bool IsConcurrencyConflict =>
            result.ErrorCode == DbErrorCodes.CONCURRENCY_CONFLICT;

        /// <summary>
        /// Gets a value indicating whether this result is a unique-constraint
        /// violation (<see cref="DbErrorCodes.UNIQUE_VIOLATION"/>).
        /// </summary>
        public bool IsUniqueViolation =>
            result.ErrorCode == DbErrorCodes.UNIQUE_VIOLATION;

        /// <summary>
        /// Gets a value indicating whether this result is a foreign-key
        /// violation (<see cref="DbErrorCodes.FOREIGN_KEY_VIOLATION"/>).
        /// </summary>
        public bool IsForeignKeyViolation =>
            result.ErrorCode == DbErrorCodes.FOREIGN_KEY_VIOLATION;

        /// <summary>
        /// Gets a value indicating whether this result is a NOT NULL
        /// violation (<see cref="DbErrorCodes.NOT_NULL_VIOLATION"/>).
        /// </summary>
        public bool IsNotNullViolation =>
            result.ErrorCode == DbErrorCodes.NOT_NULL_VIOLATION;

        /// <summary>
        /// Gets a value indicating whether this result is a CHECK violation
        /// (<see cref="DbErrorCodes.CHECK_VIOLATION"/>).
        /// </summary>
        public bool IsCheckViolation =>
            result.ErrorCode == DbErrorCodes.CHECK_VIOLATION;

        /// <summary>
        /// Gets a value indicating whether this result is a DB statement
        /// timeout (<see cref="DbErrorCodes.DB_TIMEOUT"/>).
        /// </summary>
        public bool IsDbTimeout =>
            result.ErrorCode == DbErrorCodes.DB_TIMEOUT;

        /// <summary>
        /// Gets a value indicating whether this result is a deadlock /
        /// serialization failure (<see cref="DbErrorCodes.DB_DEADLOCK"/>).
        /// Caller may safely retry the operation.
        /// </summary>
        public bool IsDbDeadlock =>
            result.ErrorCode == DbErrorCodes.DB_DEADLOCK;

        /// <summary>
        /// Gets a value indicating whether this result is a DB connection
        /// failure (<see cref="DbErrorCodes.DB_CONNECTION_FAILURE"/>).
        /// </summary>
        public bool IsDbConnectionFailure =>
            result.ErrorCode == DbErrorCodes.DB_CONNECTION_FAILURE;

        /// <summary>
        /// Gets a value indicating whether the result represents any
        /// retryable DB failure — deadlock, timeout, or connection failure.
        /// Concurrency conflicts are intentionally excluded — they require
        /// reload-then-merge logic, not a blind retry.
        /// </summary>
        /// <remarks>
        /// Distinct axis from the built-in <c>IsTransientRetryable</c>
        /// (<c>IsServiceUnavailable || IsRateLimited</c>) on
        /// <see cref="D2Result"/> in the result lib. A generic retry
        /// policy that wants to catch BOTH HTTP-flavored and DB-flavored
        /// transient failures should check the union:
        /// <c>result.IsTransientRetryable || result.IsTransientDbFailure</c>.
        /// They live in separate libs because not every consumer of
        /// <see cref="D2Result"/> deals with a database; the DB roll-up
        /// only loads when the consumer references
        /// <c>DcsvIo.D2.Handler.Repo.Abstractions</c>.
        /// </remarks>
        public bool IsTransientDbFailure =>
            result.IsDbDeadlock || result.IsDbTimeout || result.IsDbConnectionFailure;
    }
}
