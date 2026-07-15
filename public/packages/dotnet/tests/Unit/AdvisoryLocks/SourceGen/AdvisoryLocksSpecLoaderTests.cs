// -----------------------------------------------------------------------
// <copyright file="AdvisoryLocksSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AdvisoryLocks.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.AdvisoryLocks.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for <see cref="AdvisoryLocksSpecLoader"/>.
/// Covers the happy path, all MalformedSpec branches, and the
/// KeyOutOfRange branch.
/// </summary>
public sealed class AdvisoryLocksSpecLoaderTests
{
    // =========================================================================
    // Happy path
    // =========================================================================

    [Fact]
    public void Load_ValidSpec_ReturnsTwoEntries()
    {
        const string json = """
            {
              "locks": [
                {
                  "constName": "MIGRATOR",
                  "database": "d2-keycustodian",
                  "key": 1001001001,
                  "doc": "Migration lock."
                },
                {
                  "constName": "ROTATION",
                  "database": "d2-keycustodian",
                  "key": 2002002002,
                  "doc": "Rotation lock."
                }
              ]
            }
            """;

        var result = AdvisoryLocksSpecLoader.Load("advisory-locks.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Locks.Should().HaveCount(2);
        result.Spec.Locks[0].ConstName.Should().Be("MIGRATOR");
        result.Spec.Locks[0].Database.Should().Be("d2-keycustodian");
        result.Spec.Locks[0].Key.Should().Be(1001001001L);
        result.Spec.Locks[1].ConstName.Should().Be("ROTATION");
        result.Spec.Locks[1].Key.Should().Be(2002002002L);
    }

    [Fact]
    public void Load_EmptyLocksArray_ReturnsEmptySpec()
    {
        const string json = """{"locks": []}""";

        var result = AdvisoryLocksSpecLoader.Load("advisory-locks.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Locks.Should().BeEmpty();
    }

    // =========================================================================
    // MalformedSpec — root not an object
    // =========================================================================

    [Fact]
    public void Load_RootIsArray_ReturnsMalformedSpecDiagnostic()
    {
        var result = AdvisoryLocksSpecLoader.Load("advisory-locks.spec.json", "[]");

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_RootIsString_ReturnsMalformedSpecDiagnostic()
    {
        var result = AdvisoryLocksSpecLoader.Load("spec.json", "\"not an object\"");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    // =========================================================================
    // MalformedSpec — invalid JSON
    // =========================================================================

    [Fact]
    public void Load_NotJson_ReturnsMalformedSpecDiagnostic()
    {
        var result = AdvisoryLocksSpecLoader.Load("spec.json", "{not valid");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    // =========================================================================
    // MalformedSpec — missing locks array
    // =========================================================================

    [Fact]
    public void Load_MissingLocksProperty_ReturnsMalformedSpecDiagnostic()
    {
        var result = AdvisoryLocksSpecLoader.Load("spec.json", "{}");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_LocksPropertyIsString_ReturnsMalformedSpecDiagnostic()
    {
        var result = AdvisoryLocksSpecLoader.Load("spec.json", """{"locks": "oops"}""");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    // =========================================================================
    // MalformedSpec — entry-level field validation
    // =========================================================================

    [Fact]
    public void Load_EntryMissingConstName_ReturnsMalformedSpecDiagnostic()
    {
        const string json = """
            {
              "locks": [
                {"database": "db", "key": 1, "doc": "d"}
              ]
            }
            """;

        var result = AdvisoryLocksSpecLoader.Load("spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryMissingDatabase_ReturnsMalformedSpecDiagnostic()
    {
        const string json = """
            {
              "locks": [
                {"constName": "X", "key": 1, "doc": "d"}
              ]
            }
            """;

        var result = AdvisoryLocksSpecLoader.Load("spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryMissingKey_ReturnsMalformedSpecDiagnostic()
    {
        const string json = """
            {
              "locks": [
                {"constName": "X", "database": "db", "doc": "d"}
              ]
            }
            """;

        var result = AdvisoryLocksSpecLoader.Load("spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryMissingDoc_ReturnsMalformedSpecDiagnostic()
    {
        const string json = """
            {
              "locks": [
                {"constName": "X", "database": "db", "key": 1}
              ]
            }
            """;

        var result = AdvisoryLocksSpecLoader.Load("spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryKeyIsString_ReturnsMalformedSpecDiagnostic()
    {
        const string json = """
            {
              "locks": [
                {"constName": "X", "database": "db", "key": "not-a-number", "doc": "d"}
              ]
            }
            """;

        var result = AdvisoryLocksSpecLoader.Load("spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    // =========================================================================
    // KeyOutOfRange — key exceeds int64
    // =========================================================================

    [Fact]
    public void Load_KeyExceedsInt64_ReturnsKeyOutOfRangeDiagnostic()
    {
        // 9223372036854775808 = long.MaxValue + 1 — one beyond the max
        const string json = """
            {
              "locks": [
                {"constName": "X", "database": "db", "key": 9223372036854775808, "doc": "d"}
              ]
            }
            """;

        var result = AdvisoryLocksSpecLoader.Load("spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.KeyOutOfRange);
    }

    // =========================================================================
    // Path used in diagnostic args (first arg is file name, not full path)
    // =========================================================================

    [Fact]
    public void Load_MalformedJson_DiagnosticArgsContainFileName()
    {
        var result = AdvisoryLocksSpecLoader.Load(
            "some/deep/path/advisory-locks.spec.json", "{bad");

        result.Diagnostic!.Args.Should().NotBeEmpty();
        var firstArg = result.Diagnostic.Args[0].ToString();
        firstArg.Should().Be("advisory-locks.spec.json");
    }
}
