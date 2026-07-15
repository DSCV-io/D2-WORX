// -----------------------------------------------------------------------
// <copyright file="AdvisoryLockMigratorBenignDuplicateTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SharedEntityFrameworkCore;

using AwesomeAssertions;
using DcsvIo.D2.EntityFrameworkCore.Postgres;
using DcsvIo.D2.Tests.Unit.HandlerRepo.Postgres;
using Xunit;

/// <summary>
/// Unit tests for <see cref="AdvisoryLockMigrator{TContext}.IsBenignDuplicateDatabase"/>.
/// Pins the SqlState predicate that guards the CREATE DATABASE race catch-clause:
/// both 42P04 (duplicate_database) and 23505 (unique_violation) must be treated as
/// benign "database now exists" outcomes, while unrelated SqlState codes must not.
/// Deterministic: no live DB, no concurrency — exercises the predicate directly.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AdvisoryLockMigratorBenignDuplicateTests
{
    // =========================================================================
    // 42P04 — duplicate_database (the common CREATE DATABASE race outcome)
    // =========================================================================

    [Fact]
    public void IsBenignDuplicateDatabase_DuplicateDatabaseSqlState_ReturnsTrue()
    {
        var ex = PgExceptionFactory.Create(sqlState: "42P04");

        var result = AdvisoryLockMigrator<FakeDbContext>.IsBenignDuplicateDatabase(ex);

        result.Should().BeTrue(
            "42P04 (duplicate_database) means another migrator already created the " +
            "database — the operation is idempotent");
    }

    // =========================================================================
    // 23505 — unique_violation (narrow interleave on pg_database PK)
    // =========================================================================

    [Fact]
    public void IsBenignDuplicateDatabase_UniqueViolationSqlState_ReturnsTrue()
    {
        var ex = PgExceptionFactory.Create(sqlState: "23505");

        var result = AdvisoryLockMigrator<FakeDbContext>.IsBenignDuplicateDatabase(ex);

        result.Should().BeTrue(
            "23505 (unique_violation) on pg_database means two migrators raced to " +
            "INSERT the new-db catalog row — the database now exists, idempotent");
    }

    // =========================================================================
    // Unrelated SqlState — must NOT be swallowed
    // =========================================================================

    [Fact]
    public void IsBenignDuplicateDatabase_InsufficientPrivilegeSqlState_ReturnsFalse()
    {
        // 42501 = insufficient_privilege — a real error that must propagate.
        var ex = PgExceptionFactory.Create(sqlState: "42501");

        var result = AdvisoryLockMigrator<FakeDbContext>.IsBenignDuplicateDatabase(ex);

        result.Should().BeFalse(
            "42501 (insufficient_privilege) is a genuine failure, not a benign duplicate");
    }

    // =========================================================================
    // Probe DbContext — satisfies the generic constraint; not instantiated
    // =========================================================================

    private sealed class FakeDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public FakeDbContext(
            Microsoft.EntityFrameworkCore.DbContextOptions<FakeDbContext> options)
            : base(options)
        {
        }
    }
}
