// -----------------------------------------------------------------------
// <copyright file="AdvisoryLocksEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AdvisoryLocks.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.AdvisoryLocks.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for <see cref="AdvisoryLocksEmitter"/>:
/// emitted class shape, per-database grouping, duplicate detection,
/// invalid constName detection, and the <c>SnakeToPascal</c> helper.
/// </summary>
public sealed class AdvisoryLocksEmitterTests
{
    // =========================================================================
    // Happy-path shape
    // =========================================================================

    [Fact]
    public void Emit_SingleEntry_EmitsNamespaceAndNestedClass()
    {
        var spec = MakeSpec(new AdvisoryLockEntry("MIGRATOR", "d2-keycustodian", 1001001001L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain("namespace DcsvIo.D2.Private.Edge.KeyCustodian.Infra;");
        result.GeneratedSource.Should()
            .Contain("public static class AdvisoryLocks");
        result.GeneratedSource.Should()
            .Contain("public static class D2Keycustodian");
        result.GeneratedSource.Should()
            .Contain("public const long MIGRATOR = 1001001001L;");
    }

    [Fact]
    public void Emit_TwoEntriesSameDatabase_EmitsOneNestedClassWithBothConstants()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("MIGRATOR", "d2-keycustodian", 1001001001L, "mig"),
            new AdvisoryLockEntry("ROTATION", "d2-keycustodian", 2002002002L, "rot"));

        var result = AdvisoryLocksEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain("public const long MIGRATOR = 1001001001L;");
        result.GeneratedSource.Should()
            .Contain("public const long ROTATION = 2002002002L;");

        // Only one nested class for the database
        result.GeneratedSource.Split("public static class D2Keycustodian").Length.Should().Be(2);
    }

    [Fact]
    public void Emit_TwoDatabasesWithSameKeyValue_EmitsBothDatabasesNoError()
    {
        // Same key in DIFFERENT databases is legal — different keyspaces.
        var spec = MakeSpec(
            new AdvisoryLockEntry("MIGRATOR", "db_one", 9999L, "doc"),
            new AdvisoryLockEntry("MIGRATOR", "db_two", 9999L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static class DbOne");
        result.GeneratedSource.Should().Contain("public static class DbTwo");
    }

    // =========================================================================
    // Diagnostics — duplicate constName within a database
    // =========================================================================

    [Fact]
    public void Emit_DuplicateConstNameSameDatabase_EmitsDuplicateConstNameDiagnostic()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("LOCK_A", "db", 1L, "doc"),
            new AdvisoryLockEntry("LOCK_A", "db", 2L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateConstNameInDatabase);
    }

    [Fact]
    public void Emit_DuplicateConstNameDifferentDatabases_NoDiagnostic()
    {
        // Same constName in different databases is legal.
        var spec = MakeSpec(
            new AdvisoryLockEntry("MIGRATOR", "db_one", 1L, "doc"),
            new AdvisoryLockEntry("MIGRATOR", "db_two", 2L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
    }

    // =========================================================================
    // Diagnostics — duplicate key within a database
    // =========================================================================

    [Fact]
    public void Emit_DuplicateKeySameDatabase_EmitsDuplicateKeyInDatabaseDiagnostic()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("LOCK_A", "db", 42L, "doc"),
            new AdvisoryLockEntry("LOCK_B", "db", 42L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateKeyInDatabase);
    }

    // =========================================================================
    // Diagnostics — invalid constName
    // =========================================================================

    [Fact]
    public void Emit_LowerCaseConstName_EmitsInvalidConstNameDiagnostic()
    {
        var spec = MakeSpec(new AdvisoryLockEntry("lowercase", "db", 1L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidConstName);
    }

    [Fact]
    public void Emit_ConstNameStartsWithDigit_EmitsInvalidConstNameDiagnostic()
    {
        var spec = MakeSpec(new AdvisoryLockEntry("1LOCK", "db", 1L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidConstName);
    }

    // =========================================================================
    // auto-generated banner
    // =========================================================================

    [Fact]
    public void Emit_AnySpec_GeneratedSourceContainsAutoGeneratedBanner()
    {
        var spec = MakeSpec(new AdvisoryLockEntry("X", "db", 1L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec);

        result.GeneratedSource.Should().Contain("<auto-generated>");
        result.GeneratedSource.Should().Contain("Manual edits will be lost on rebuild.");
    }

    // =========================================================================
    // SnakeToPascal helper
    // =========================================================================

    [Theory]
    [InlineData("d2-keycustodian", "D2Keycustodian")]
    [InlineData("my_long_db_name", "MyLongDbName")]
    [InlineData("single", "Single")]
    [InlineData("a_b_c", "ABC")]
    [InlineData("", "")]
    public void SnakeToPascal_VariousInputs_ProducesExpectedOutput(
        string input, string expected)
    {
        AdvisoryLocksEmitter.SnakeToPascal(input).Should().Be(expected);
    }

    // =========================================================================
    // Database ordering — alphabetical
    // =========================================================================

    [Fact]
    public void Emit_TwoDatabases_DatabaseClassesEmittedAlphabetically()
    {
        var spec = MakeSpec(
            new AdvisoryLockEntry("LOCK_B", "zz_db", 2L, "doc"),
            new AdvisoryLockEntry("LOCK_A", "aa_db", 1L, "doc"));

        var result = AdvisoryLocksEmitter.Emit(spec);

        var aaIdx = result.GeneratedSource.IndexOf(
            "public static class AaDb",
            System.StringComparison.Ordinal);
        var zzIdx = result.GeneratedSource.IndexOf(
            "public static class ZzDb",
            System.StringComparison.Ordinal);

        aaIdx.Should().BeLessThan(zzIdx, "databases must be emitted alphabetically by key");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static AdvisoryLocksSpec MakeSpec(params AdvisoryLockEntry[] entries) =>
        new(entries.ToImmutableArray());
}
