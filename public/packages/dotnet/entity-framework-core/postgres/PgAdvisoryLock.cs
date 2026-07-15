// -----------------------------------------------------------------------
// <copyright file="PgAdvisoryLock.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EntityFrameworkCore.Postgres;

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DcsvIo.D2.Utilities.Extensions;
using JetBrains.Annotations;
using Npgsql;

/// <summary>
/// PostgreSQL session-scoped advisory lock helper.
/// </summary>
/// <remarks>
/// <para>
/// Opens a <strong>dedicated</strong> <see cref="NpgsqlConnection"/>; every acquired lock
/// lives on that connection for the duration of the scope. Session advisory locks are
/// automatically released when the connection is closed or dropped — no explicit unlock
/// is required, but <see cref="DisposeAsync"/> sends an explicit <c>pg_advisory_unlock</c>
/// as belt-and-suspenders.
/// </para>
/// <para>
/// <strong>Do NOT use <c>EnableRetryOnFailure</c></strong> alongside session advisory
/// locks. An execution-strategy reconnect silently drops the session lock, leaving the
/// caller believing it still holds the critical section. Session advisory locks depend on
/// connection lifetime continuity — reconnect = lock lost.
/// </para>
/// <para>
/// Keys are caller-supplied <see cref="long"/> values. Use domain-owned
/// generated <c>AdvisoryLocks.*</c> constants (emitted into the owning module
/// assembly from <c>contracts/advisory-locks/</c>) to avoid raw literals.
/// </para>
/// </remarks>
[MustDisposeResource]
public sealed class PgAdvisoryLock : IAsyncDisposable
{
    private readonly NpgsqlConnection r_connection;
    private readonly long r_key;
    private bool _disposed;

    private PgAdvisoryLock(NpgsqlConnection connection, long key)
    {
        r_connection = connection;
        r_key = key;
    }

    /// <summary>
    /// Gets a value indicating whether this instance currently holds the advisory lock.
    /// </summary>
    public bool IsHeld { get; private set; }

    // =========================================================================
    // Factory methods
    // =========================================================================

    /// <summary>
    /// Attempts to acquire the session advisory lock immediately (non-blocking).
    /// </summary>
    /// <param name="connectionString">Npgsql connection string.</param>
    /// <param name="key">Advisory lock key (unique within the database keyspace).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="PgAdvisoryLock"/> whose <see cref="IsHeld"/> is <see langword="true"/>
    /// when the lock was acquired, or <see langword="false"/> when another session already
    /// holds it. Always dispose the returned instance.
    /// </returns>
    [MustDisposeResource]
    public static async Task<PgAdvisoryLock> TryAcquireSessionAsync(
        string connectionString, long key, CancellationToken ct = default)
    {
        connectionString.ThrowIfFalsey();

        // Pooling=false: forces the physical session to terminate on CloseAsync /
        // DisposeAsync, releasing the session advisory lock deterministically regardless
        // of the No Reset On Close connection-string option. If that option is active,
        // Npgsql suppresses the pool-return DISCARD ALL that would otherwise release
        // a held lock — meaning the next pool borrower could inherit a live lock.
        // Disabling pooling eliminates that footgun entirely.
        var connection = new NpgsqlConnection(AsUnpooled(connectionString));
        var handle = new PgAdvisoryLock(connection, key);

        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT pg_try_advisory_lock(@key)";
            cmd.Parameters.AddWithValue("key", key);
            var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            handle.IsHeld = result is true;

            return handle;
        }
        catch
        {
            await handle.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Acquires the session advisory lock, blocking until it is available.
    /// </summary>
    /// <param name="connectionString">Npgsql connection string.</param>
    /// <param name="key">Advisory lock key (unique within the database keyspace).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="PgAdvisoryLock"/> whose <see cref="IsHeld"/> is <see langword="true"/>.
    /// Always dispose the returned instance.
    /// </returns>
    [MustDisposeResource]
    public static async Task<PgAdvisoryLock> AcquireSessionBlockingAsync(
        string connectionString, long key, CancellationToken ct = default)
    {
        connectionString.ThrowIfFalsey();

        var connection = new NpgsqlConnection(AsUnpooled(connectionString));
        var handle = new PgAdvisoryLock(connection, key);

        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT pg_advisory_lock(@key)";
            cmd.Parameters.AddWithValue("key", key);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            handle.IsHeld = true;

            return handle;
        }
        catch
        {
            await handle.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    // =========================================================================
    // IAsyncDisposable
    // =========================================================================

    /// <summary>
    /// Releases the advisory lock (explicit <c>pg_advisory_unlock</c>) and closes the
    /// underlying connection. Safe to call even when <see cref="IsHeld"/> is
    /// <see langword="false"/> (no-op unlock on unacquired locks is harmless).
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            if (r_connection.State == ConnectionState.Open && IsHeld)
            {
                await using var cmd = r_connection.CreateCommand();
                cmd.CommandText = "SELECT pg_advisory_unlock(@key)";
                cmd.Parameters.AddWithValue("key", r_key);
                await cmd.ExecuteScalarAsync().ConfigureAwait(false);
                IsHeld = false;
            }
        }
        finally
        {
            await r_connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    // =========================================================================
    // Test seam (InternalsVisibleTo DcsvIo.D2.Tests)
    // =========================================================================

    /// <summary>
    /// Drops the underlying connection without releasing the advisory lock, simulating
    /// a connection failure. Tests use this to verify PostgreSQL server-side auto-release
    /// (session advisory locks are automatically released when the connection is dropped).
    /// Because connections are created with <c>Pooling=false</c>, <c>CloseAsync</c> here
    /// terminates the physical TCP session and the server releases the lock immediately.
    /// </summary>
    internal async Task ForceDropConnectionForTestAsync()
        => await r_connection.CloseAsync().ConfigureAwait(false);

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>
    /// Returns a connection string with <c>Pooling=false</c>, guaranteeing that the
    /// physical session terminates on <c>CloseAsync</c> / <c>DisposeAsync</c> and
    /// releases the session advisory lock deterministically. Without this, the
    /// <c>No Reset On Close</c> connection-string option (when set) suppresses
    /// Npgsql's pool-return <c>DISCARD ALL</c>, which would otherwise release the
    /// lock — leaking a held lock to the next pool borrower.
    /// The override applies only to the copy of the connection string used for this
    /// helper's dedicated lock connection; the caller's original string is not mutated
    /// and their application or DbContext pooled connections are unaffected.
    /// </summary>
    private static string AsUnpooled(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = false,
        };
        return builder.ToString();
    }
}
