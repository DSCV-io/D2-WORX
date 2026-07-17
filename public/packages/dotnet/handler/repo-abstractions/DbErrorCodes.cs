// -----------------------------------------------------------------------
// <copyright file="DbErrorCodes.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Repo.Abstractions;

using DcsvIo.D2.Result;

/// <summary>
/// Standardized <see cref="D2Result.ErrorCode"/> values for DB-flavored
/// failures. Parallel to <see cref="ErrorCodes"/> in the result lib but
/// scoped to database concerns. Used by the
/// <see cref="D2Result"/> extension factories defined in this package and
/// surfaced to callers via the matching <c>IsXxx</c> boolean discriminators.
/// </summary>
public static class DbErrorCodes
{
    /// <summary>
    /// Optimistic-concurrency conflict (<see cref="DbFailureKind.ConcurrencyConflict"/>).
    /// </summary>
    public const string CONCURRENCY_CONFLICT = nameof(CONCURRENCY_CONFLICT);

    /// <summary>
    /// Unique-constraint violation (<see cref="DbFailureKind.UniqueViolation"/>).
    /// </summary>
    public const string UNIQUE_VIOLATION = nameof(UNIQUE_VIOLATION);

    /// <summary>
    /// Foreign-key-constraint violation (<see cref="DbFailureKind.ForeignKeyViolation"/>).
    /// </summary>
    public const string FOREIGN_KEY_VIOLATION = nameof(FOREIGN_KEY_VIOLATION);

    /// <summary>
    /// NOT NULL constraint violation (<see cref="DbFailureKind.NotNullViolation"/>).
    /// </summary>
    public const string NOT_NULL_VIOLATION = nameof(NOT_NULL_VIOLATION);

    /// <summary>
    /// CHECK constraint violation (<see cref="DbFailureKind.CheckViolation"/>).
    /// </summary>
    public const string CHECK_VIOLATION = nameof(CHECK_VIOLATION);

    /// <summary>
    /// Server-side timeout on a statement (<see cref="DbFailureKind.Timeout"/>).
    /// </summary>
    public const string DB_TIMEOUT = nameof(DB_TIMEOUT);

    /// <summary>
    /// Deadlock / serialization failure (<see cref="DbFailureKind.Deadlock"/>).
    /// </summary>
    public const string DB_DEADLOCK = nameof(DB_DEADLOCK);

    /// <summary>
    /// Connection-level failure (<see cref="DbFailureKind.ConnectionFailure"/>).
    /// </summary>
    public const string DB_CONNECTION_FAILURE = nameof(DB_CONNECTION_FAILURE);
}
