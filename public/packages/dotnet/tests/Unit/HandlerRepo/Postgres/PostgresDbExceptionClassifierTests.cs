// -----------------------------------------------------------------------
// <copyright file="PostgresDbExceptionClassifierTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.HandlerRepo.Postgres;

using System;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Handler.Repo.Abstractions;
using DcsvIo.D2.Handler.Repo.Postgres;
using global::Npgsql;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// The big one — adversarial coverage of the PG SQLSTATE → DbFailureKind
/// matrix + every wrapper / exception-shape edge case the classifier walks.
/// </summary>
/// <remarks>
/// This class is the security-critical seam. A misclassification here causes
/// either silent retries on programmer errors (e.g. a bug surfacing as a
/// "transient" failure) or pass-through of recoverable failures as
/// UnhandledException (which kills retries). Every behavior listed in
/// PostgresDbExceptionClassifier's XML doc is asserted below.
/// </remarks>
public sealed class PostgresDbExceptionClassifierTests
{
    private readonly PostgresDbExceptionClassifier _sut = new();

    // ----------------------------------------------------------------------
    // Pass 1: SQLSTATE matrix
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("23505", DbFailureKind.UniqueViolation)]

    // exclusion_violation — deferrable EXCLUDE breach → same 409 conflict as unique_violation
    [InlineData("23P01", DbFailureKind.UniqueViolation)]
    [InlineData("23503", DbFailureKind.ForeignKeyViolation)]
    [InlineData("23502", DbFailureKind.NotNullViolation)]
    [InlineData("23514", DbFailureKind.CheckViolation)]
    [InlineData("40001", DbFailureKind.Deadlock)] // serialization_failure
    [InlineData("40P01", DbFailureKind.Deadlock)] // deadlock_detected
    [InlineData("57014", DbFailureKind.Timeout)] // query_canceled (server statement_timeout)
    [InlineData("57P03", DbFailureKind.ConnectionFailure)] // cannot_connect_now
    [InlineData("53300", DbFailureKind.ConnectionFailure)] // too_many_connections
    public void Classify_RawPgException_KnownSqlState_ReturnsExpectedKind(
        string sqlState,
        DbFailureKind expected)
    {
        var ex = PgExceptionFactory.Create(sqlState);

        var kind = _sut.Classify(ex);

        kind.Should().Be(expected);
    }

    // ----------------------------------------------------------------------
    // 08xxx connection-exception class (StartsWith dispatch)
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("08000")] // connection_exception
    [InlineData("08001")] // sqlclient_unable_to_establish_sqlconnection
    [InlineData("08003")] // connection_does_not_exist
    [InlineData("08006")] // connection_failure
    [InlineData("08P01")] // protocol_violation (still 08-class)
    public void Classify_RawPgException_ConnectionExceptionClass_ReturnsConnectionFailure(
        string sqlState)
    {
        var ex = PgExceptionFactory.Create(sqlState);

        var kind = _sut.Classify(ex);

        kind.Should().Be(DbFailureKind.ConnectionFailure);
    }

    // ----------------------------------------------------------------------
    // Unrecognized SQLSTATE → fall through to network checks → null
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("42601")] // syntax_error — programmer error
    [InlineData("42P01")] // undefined_table — programmer error
    [InlineData("XX000")] // internal_error
    [InlineData("00000")] // successful_completion — never reached on real exception
    public void Classify_RawPgException_UnknownSqlState_ReturnsNull(string sqlState)
    {
        var ex = PgExceptionFactory.Create(sqlState);

        var kind = _sut.Classify(ex);

        kind.Should().BeNull(
            "unknown SQLSTATE — surface as UnhandledException so ops sees the bug");
    }

    // ----------------------------------------------------------------------
    // Wrapper unwrapping: DbUpdateException, AggregateException, TIE
    // ----------------------------------------------------------------------

    [Fact]
    public void Classify_DbUpdateExceptionWrappingPg_Unwraps_AndClassifies()
    {
        var pg = PgExceptionFactory.Create("23505");
        var ex = new DbUpdateException("update failed", pg);

        var kind = _sut.Classify(ex);

        kind.Should().Be(DbFailureKind.UniqueViolation);
    }

    [Fact]
    public void Classify_AggregateExceptionWrappingDbUpdateWrappingPg_Unwraps()
    {
        // 3-deep wrapper chain: AggregateException → DbUpdateException → PG.
        // The walker must dig through both wrapper layers (depth 2 on the
        // PG node).
        var pg = PgExceptionFactory.Create("23505");
        var dbu = new DbUpdateException("update failed", pg);
        var agg = new AggregateException("agg", dbu);

        var kind = _sut.Classify(agg);

        kind.Should().Be(DbFailureKind.UniqueViolation);
    }

    [Fact]
    public void Classify_TargetInvocationExceptionWrappingPg_Unwraps()
    {
        // 2-deep wrapper chain via reflection-style wrapper.
        var pg = PgExceptionFactory.Create("23503");
        var tie = new TargetInvocationException(pg);

        var kind = _sut.Classify(tie);

        kind.Should().Be(DbFailureKind.ForeignKeyViolation);
    }

    [Fact]
    public void Classify_PgAtDepth9_InsideMixedWrapperChain_StillFinds()
    {
        // Adversarial: confirm the walker's depth-9 reach. Buried deep
        // but still reachable.
        var pg = PgExceptionFactory.Create("40P01");
        var wrap = WrapNTimes(pg, 9);

        var kind = _sut.Classify(wrap);

        kind.Should().Be(DbFailureKind.Deadlock);
    }

    [Fact]
    public void Classify_PgBeyondDepth10_ReturnsNull()
    {
        // Adversarial: walker stops at depth 10. A PG buried beyond is
        // invisible — surfaces as null (UnhandledException downstream).
        // This is INTENTIONAL: a wrapper chain this deep is almost
        // certainly a bug that needs investigation, not silent retry.
        var pg = PgExceptionFactory.Create("23505");
        var wrap = WrapNTimes(pg, 11);

        var kind = _sut.Classify(wrap);

        kind.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Pass 2: network-level (SocketException / IOException) detection
    // ----------------------------------------------------------------------

    [Fact]
    public void Classify_BareNpgsqlExceptionWithSocketInner_ReturnsConnectionFailure()
    {
        var inner = new SocketException((int)SocketError.ConnectionRefused);
        var ex = new NpgsqlException("connection refused", inner);

        var kind = _sut.Classify(ex);

        kind.Should().Be(DbFailureKind.ConnectionFailure);
    }

    [Fact]
    public void Classify_BareNpgsqlExceptionWithIOWrappingSocket_ReturnsConnectionFailure()
    {
        var sock = new SocketException((int)SocketError.ConnectionReset);
        var io = new IOException("stream closed", sock);
        var ex = new NpgsqlException("network drop", io);

        var kind = _sut.Classify(ex);

        kind.Should().Be(DbFailureKind.ConnectionFailure);
    }

    [Fact]
    public void Classify_BareExceptionWithIoInner_ReturnsConnectionFailure()
    {
        // Adversarial: even a non-NpgsqlException wrapping IOException
        // gets classified as ConnectionFailure — the network-detection
        // pass walks ANY exception chain, not just NpgsqlException ones.
        var io = new IOException("network error");
        var ex = new InvalidOperationException("wrap", io);

        var kind = _sut.Classify(ex);

        kind.Should().Be(DbFailureKind.ConnectionFailure);
    }

    [Fact]
    public void Classify_BareNpgsqlExceptionWithNoInner_ReturnsNull()
    {
        // KEY BEHAVIOR: bare NpgsqlException with NO inner cause
        // returns null. Used to be ConnectionFailure (per old behavior
        // alluded to in PG classifier XML doc); current code makes this
        // explicit programmer-error / config-failure surface that
        // requires investigation.
        var ex = new NpgsqlException("bare npgsql, no inner");

        var kind = _sut.Classify(ex);

        kind.Should().BeNull(
            "bare NpgsqlException without recognizable inner cause is NOT a transient failure");
    }

    [Fact]
    public void Classify_PlainArgumentException_ReturnsNull()
    {
        // Programmer error — must NOT be classified as anything DB.
        var ex = new ArgumentException("bad arg");

        var kind = _sut.Classify(ex);

        kind.Should().BeNull();
    }

    [Fact]
    public void Classify_PlainBclTimeoutException_ReturnsNull()
    {
        // Adversarial: BCL TimeoutException is NOT classified here.
        // Only the PG SQLSTATE 57014 path counts as "DB timeout."
        // BCL TimeoutException is handled at BaseHandler's OCE level.
        var ex = new TimeoutException("waited too long");

        var kind = _sut.Classify(ex);

        kind.Should().BeNull();
    }

    [Fact]
    public void Classify_NullException_ThrowsArgumentNullException()
    {
        Action act = () => _sut.Classify(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ----------------------------------------------------------------------
    // Precedence: SQLSTATE pass 1 wins over pass-2 network detection
    // ----------------------------------------------------------------------

    [Fact]
    public void Classify_PgWithSqlStateAndSocketInner_PrefersSqlStateClassification()
    {
        // Adversarial precedence: an exception that matches BOTH
        // pass-1 (PG with SqlState) AND pass-2 (SocketException in
        // chain) MUST be classified by pass-1. The PG SQLSTATE is the
        // most reliable signal — server told us what went wrong.
        var sock = new SocketException((int)SocketError.ConnectionReset);
        var pg = PgExceptionFactory.CreateWithInner("23505", sock);

        var kind = _sut.Classify(pg);

        kind.Should().Be(
            DbFailureKind.UniqueViolation,
            "SQLSTATE 23505 must beat the SocketException-in-chain detection");
    }

    [Fact]
    public void Classify_DbUpdateWrappingPgWithUnknownSqlState_FallsThroughToNetworkPass()
    {
        // Adversarial: when the inner PG has an unknown SQLSTATE (so pass-1
        // returns null), the classifier should fall through to pass-2.
        // If pass-2 finds nothing in the chain, the result is null.
        // Documents the precedence correctly.
        var pg = PgExceptionFactory.Create("XX000");
        var wrap = new DbUpdateException("wrap", pg);

        var kind = _sut.Classify(wrap);

        kind.Should().BeNull();
    }

    [Fact]
    public void Classify_AggregateWithUnknownPgAndSocketSibling_DetectsNetwork()
    {
        // Adversarial: AggregateException carrying TWO inner exceptions —
        // a PG with an unknown SqlState (wouldn't classify on its own) and
        // a SocketException sibling at InnerExceptions[1]. The walker MUST
        // traverse every branch of AggregateException.InnerExceptions, not
        // just the first via Exception.InnerException, otherwise EF / TPL
        // parallel batch operations that surface a connection drop as the
        // second inner would silently miss the network failure.
        var pg = PgExceptionFactory.Create("XX000");
        var sock = new SocketException((int)SocketError.ConnectionReset);
        var agg = new AggregateException("agg", pg, sock);

        var kind = _sut.Classify(agg);

        // Pass 1 finds pg first, but XX000 doesn't classify → null.
        // Pass 2 walks for SocketException; the AggregateException-aware
        // walker descends every branch and finds the socket sibling.
        kind.Should().Be(DbFailureKind.ConnectionFailure);
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static Exception WrapNTimes(Exception inner, int times)
    {
        var current = inner;
        for (var i = 0; i < times; i++)
            current = new InvalidOperationException($"wrap-{i}", current);
        return current;
    }
}
