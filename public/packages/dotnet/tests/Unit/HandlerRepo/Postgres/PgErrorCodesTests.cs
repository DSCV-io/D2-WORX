// -----------------------------------------------------------------------
// <copyright file="PgErrorCodesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.HandlerRepo.Postgres;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Handler.Repo.Postgres;
using Microsoft.EntityFrameworkCore;
using Xunit;

/// <summary>
/// Pinning + walker tests for <see cref="PgErrorCodes"/>. The
/// <c>SQLSTATE</c> string constants are part of the wire contract; the
/// <see cref="PgErrorCodes.TryGetPgException"/> walker is the unwrap surface
/// used by both the classifier and any consumer that wants the underlying
/// PG exception (e.g. for constraint-name introspection in
/// <c>MapDbException</c> overrides).
/// </summary>
public sealed class PgErrorCodesTests
{
    // ----------------------------------------------------------------------
    // Constants pinned to documented SQLSTATE values
    // ----------------------------------------------------------------------

    [Fact]
    public void Constants_PinnedToDocumentedSqlStateValues()
    {
        PgErrorCodes.UNIQUE_VIOLATION.Should().Be("23505");
        PgErrorCodes.FOREIGN_KEY_VIOLATION.Should().Be("23503");
        PgErrorCodes.NOT_NULL_VIOLATION.Should().Be("23502");
        PgErrorCodes.CHECK_VIOLATION.Should().Be("23514");
        PgErrorCodes.SERIALIZATION_FAILURE.Should().Be("40001");
        PgErrorCodes.DEADLOCK_DETECTED.Should().Be("40P01");
        PgErrorCodes.QUERY_CANCELED.Should().Be("57014");
        PgErrorCodes.CANNOT_CONNECT_NOW.Should().Be("57P03");
        PgErrorCodes.TOO_MANY_CONNECTIONS.Should().Be("53300");
        PgErrorCodes.CONNECTION_EXCEPTION_CLASS.Should().Be("08");
    }

    // ----------------------------------------------------------------------
    // TryGetPgException walker — depth boundary tests
    // ----------------------------------------------------------------------

    [Fact]
    public void TryGetPgException_Depth0_Raw_ReturnsTheException()
    {
        var pg = PgExceptionFactory.Create("23505");

        var got = PgErrorCodes.TryGetPgException(pg);

        got.Should().BeSameAs(pg);
    }

    [Fact]
    public void TryGetPgException_Depth1_DbUpdateExceptionWrap_Unwraps()
    {
        var pg = PgExceptionFactory.Create("23505");
        var wrap = new DbUpdateException("update failed", pg);

        var got = PgErrorCodes.TryGetPgException(wrap);

        got.Should().BeSameAs(pg);
    }

    [Fact]
    public void TryGetPgException_Depth5_Wrappers_Unwraps()
    {
        var pg = PgExceptionFactory.Create("23505");
        var wrap = WrapNTimes(pg, 5);

        var got = PgErrorCodes.TryGetPgException(wrap);

        got.Should().BeSameAs(pg);
    }

    [Fact]
    public void TryGetPgException_Depth9_Wrappers_StillFinds()
    {
        // Adversarial: at depth 9 the walker should still find the PG
        // exception (loop bound is `depth < 10`, so iterations 0..9
        // inclusive get to look — that's 10 chances, finding at depth 9
        // counts).
        var pg = PgExceptionFactory.Create("23505");
        var wrap = WrapNTimes(pg, 9);

        var got = PgErrorCodes.TryGetPgException(wrap);

        got.Should().BeSameAs(pg);
    }

    [Fact]
    public void TryGetPgException_Depth10_AtTheWall_ReturnsNull()
    {
        // Adversarial: at depth 10 the PG exception is at level-10 of the
        // chain. The walker stops at `depth < 10` — i.e. only checks
        // depths 0..9. So a PG at depth 10 is NOT found. Documents the
        // boundary precisely.
        var pg = PgExceptionFactory.Create("23505");
        var wrap = WrapNTimes(pg, 10);

        var got = PgErrorCodes.TryGetPgException(wrap);

        got.Should().BeNull();
    }

    [Fact]
    public void TryGetPgException_Depth11_OverTheWall_ReturnsNull()
    {
        var pg = PgExceptionFactory.Create("23505");
        var wrap = WrapNTimes(pg, 11);

        var got = PgErrorCodes.TryGetPgException(wrap);

        got.Should().BeNull();
    }

    [Fact]
    public void TryGetPgException_NoPgInChain_ReturnsNull()
    {
        var ex = new InvalidOperationException(
            "outer",
            new InvalidOperationException("inner"));

        var got = PgErrorCodes.TryGetPgException(ex);

        got.Should().BeNull();
    }

    private static Exception WrapNTimes(Exception inner, int times)
    {
        var current = inner;
        for (var i = 0; i < times; i++)
            current = new InvalidOperationException($"wrap-{i}", current);
        return current;
    }
}
