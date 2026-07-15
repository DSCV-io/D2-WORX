// -----------------------------------------------------------------------
// <copyright file="PostgresDbExceptionClassifier.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Repo.Postgres;

using System;
using System.IO;
using System.Net.Sockets;
using DcsvIo.D2.Handler.Repo.Abstractions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

/// <summary>
/// PostgreSQL implementation of <see cref="IDbExceptionClassifier"/>.
/// Classifies in two precedence-ordered passes; an exception that matches
/// both is categorized by the first match.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pass 1 — server-returned <see cref="PostgresException"/> with non-null
/// SqlState.</b> Most reliable signal: the server itself reported what went
/// wrong. SqlState routes through <see cref="ClassifyBySqlState"/>. Reached
/// for both raw <see cref="PostgresException"/> instances AND EF-wrapped
/// <see cref="DbUpdateException"/> instances; the unwrap walks the inner
/// chain via <c>PgErrorCodes.TryGetPgException</c> up to 10 levels
/// deep (defends against <see cref="AggregateException"/> /
/// <see cref="System.Reflection.TargetInvocationException"/> wrappers).
/// </para>
/// <para>
/// <b>Pass 2 — network-level <see cref="SocketException"/> /
/// <see cref="IOException"/> anywhere in the inner-exception chain.</b>
/// Indicates the connection dropped, was refused, or the OS network stack
/// faulted — transient, retryable.
/// </para>
/// <para>
/// <b>Anything else returns <c>null</c></b> — including bare
/// <see cref="NpgsqlException"/>s with no recognizable inner cause (bad
/// connection-string parse, SSL handshake failure, concurrent-connection
/// misuse, internal Npgsql state errors). These are programmer / config
/// failures that should NOT be silently treated as transient and retried;
/// the caller surfaces them as <c>UnhandledException</c> so they page ops.
/// </para>
/// <para>
/// <b>Client-side timeouts are NOT classified here.</b> Npgsql surfaces a
/// blown <c>NpgsqlCommand.CommandTimeout</c> as
/// <see cref="OperationCanceledException"/>, which
/// <c>BaseHandler.RunCorePipelineAsync</c> already handles before
/// <c>BaseRepoHandler</c> sees the captured exception (mapping to
/// <c>D2Result.Canceled</c> when the caller's <see cref="System.Threading.CancellationToken"/>
/// fired, otherwise to <c>D2Result.ServiceUnavailable</c>). Only the
/// server-side <c>statement_timeout</c> path (SQLSTATE <c>57014</c>) reaches
/// pass 1 here as <see cref="DbFailureKind.Timeout"/>.
/// </para>
/// </remarks>
public sealed class PostgresDbExceptionClassifier : IDbExceptionClassifier
{
    private const int _MAX_INNER_DEPTH = 10;

    /// <inheritdoc/>
    public DbFailureKind? Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // Pass 1: server-returned PG exception with usable SqlState.
        if (PgErrorCodes.TryGetPgException(exception) is { SqlState: { } sqlState }
            && ClassifyBySqlState(sqlState) is { } byState)
        {
            return byState;
        }

        // Pass 2: network-level drop. SocketException covers refused / reset /
        // host-unreachable; IOException covers stream-level failures wrapped
        // around a SocketException by Npgsql's transport layer.
        if (HasInChain<SocketException>(exception)
            || HasInChain<IOException>(exception))
        {
            return DbFailureKind.ConnectionFailure;
        }

        // Unrecognized — programmer / config error, surface as UnhandledException.
        return null;
    }

    private static DbFailureKind? ClassifyBySqlState(string sqlState)
        => sqlState switch
        {
            PgErrorCodes.UNIQUE_VIOLATION => DbFailureKind.UniqueViolation,

            // exclusion_violation (23P01): a partial deferrable EXCLUDE
            // constraint breach (e.g. "one Active key per domain"). PostgreSQL
            // raises this — NOT 23505 — for an EXCLUDE collision, yet it is
            // semantically a uniqueness conflict, so it shares
            // DbFailureKind.UniqueViolation → 409 Conflict with 23505. Uses
            // Npgsql's own named constant (not a PgErrorCodes const) so this
            // internal mapping does not expand the package's public API.
            PostgresErrorCodes.ExclusionViolation => DbFailureKind.UniqueViolation,
            PgErrorCodes.FOREIGN_KEY_VIOLATION => DbFailureKind.ForeignKeyViolation,
            PgErrorCodes.NOT_NULL_VIOLATION => DbFailureKind.NotNullViolation,
            PgErrorCodes.CHECK_VIOLATION => DbFailureKind.CheckViolation,
            PgErrorCodes.SERIALIZATION_FAILURE => DbFailureKind.Deadlock,
            PgErrorCodes.DEADLOCK_DETECTED => DbFailureKind.Deadlock,
            PgErrorCodes.QUERY_CANCELED => DbFailureKind.Timeout,
            PgErrorCodes.CANNOT_CONNECT_NOW => DbFailureKind.ConnectionFailure,
            PgErrorCodes.TOO_MANY_CONNECTIONS => DbFailureKind.ConnectionFailure,
            _ => sqlState.StartsWith(
                    PgErrorCodes.CONNECTION_EXCEPTION_CLASS,
                    StringComparison.Ordinal)
                ? DbFailureKind.ConnectionFailure
                : null,
        };

    private static bool HasInChain<T>(Exception exception)
        where T : Exception
        => HasInChain<T>(exception, depth: 0);

    private static bool HasInChain<T>(Exception? exception, int depth)
        where T : Exception
    {
        if (exception is null || depth >= _MAX_INNER_DEPTH)
            return false;

        if (exception is T)
            return true;

        // AggregateException carries N parallel inner exceptions — search every
        // branch, not just the first. EF / TPL parallel batch operations can
        // surface a SocketException as InnerExceptions[1+] while [0] is some
        // unrelated wrapper, and a single-pointer walker would miss it.
        if (exception is AggregateException agg)
        {
            foreach (var inner in agg.InnerExceptions)
            {
                if (HasInChain<T>(inner, depth + 1))
                    return true;
            }

            return false;
        }

        return HasInChain<T>(exception.InnerException, depth + 1);
    }
}
