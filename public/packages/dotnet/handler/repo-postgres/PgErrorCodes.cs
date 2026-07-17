// -----------------------------------------------------------------------
// <copyright file="PgErrorCodes.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Repo.Postgres;

using System;
using Microsoft.EntityFrameworkCore;
using Npgsql;

/// <summary>
/// PostgreSQL <c>SQLSTATE</c> string constants + helpers for unwrapping the
/// underlying <see cref="PostgresException"/> from EF-wrapped or raw paths.
/// Codes per
/// <see href="https://www.postgresql.org/docs/current/errcodes-appendix.html"/>.
/// </summary>
public static class PgErrorCodes
{
    /// <summary>SQLSTATE for unique-constraint violation.</summary>
    public const string UNIQUE_VIOLATION = "23505";

    /// <summary>SQLSTATE for foreign-key-constraint violation.</summary>
    public const string FOREIGN_KEY_VIOLATION = "23503";

    /// <summary>SQLSTATE for NOT NULL constraint violation.</summary>
    public const string NOT_NULL_VIOLATION = "23502";

    /// <summary>SQLSTATE for CHECK constraint violation.</summary>
    public const string CHECK_VIOLATION = "23514";

    /// <summary>SQLSTATE for serialization-failure (txn must be retried).</summary>
    public const string SERIALIZATION_FAILURE = "40001";

    /// <summary>SQLSTATE for deadlock-detected.</summary>
    public const string DEADLOCK_DETECTED = "40P01";

    /// <summary>SQLSTATE for query canceled (typically by statement_timeout).</summary>
    public const string QUERY_CANCELED = "57014";

    /// <summary>
    /// SQLSTATE for "cannot connect now" — server is in a state that
    /// rejects connections (e.g. starting up, in recovery).
    /// </summary>
    public const string CANNOT_CONNECT_NOW = "57P03";

    /// <summary>SQLSTATE for "too many connections" (pool / server limit).</summary>
    public const string TOO_MANY_CONNECTIONS = "53300";

    /// <summary>SQLSTATE class for connection exception (any 08xxx).</summary>
    public const string CONNECTION_EXCEPTION_CLASS = "08";

    private const int _MAX_INNER_DEPTH = 10;

    /// <summary>
    /// Walks the inner-exception chain (up to <see cref="_MAX_INNER_DEPTH"/>
    /// levels deep) and returns the first <see cref="PostgresException"/>
    /// found, or <c>null</c> if none. Handles the EF-wrapped path
    /// (<see cref="DbUpdateException"/> → <c>InnerException</c>) plus deeper
    /// wrappers like <see cref="AggregateException"/> (every branch of
    /// <see cref="AggregateException.InnerExceptions"/> is searched, not just
    /// <c>InnerException</c>) and <see cref="System.Reflection.TargetInvocationException"/>.
    /// </summary>
    /// <param name="ex">The exception to unwrap.</param>
    /// <returns>The underlying <see cref="PostgresException"/>, or null.</returns>
    public static PostgresException? TryGetPgException(Exception ex)
        => TryGetPgException(ex, depth: 0);

    private static PostgresException? TryGetPgException(Exception? exception, int depth)
    {
        if (exception is null || depth >= _MAX_INNER_DEPTH)
            return null;

        if (exception is PostgresException pg)
            return pg;

        if (exception is AggregateException agg)
        {
            foreach (var inner in agg.InnerExceptions)
            {
                if (TryGetPgException(inner, depth + 1) is { } found)
                    return found;
            }

            return null;
        }

        return TryGetPgException(exception.InnerException, depth + 1);
    }
}
