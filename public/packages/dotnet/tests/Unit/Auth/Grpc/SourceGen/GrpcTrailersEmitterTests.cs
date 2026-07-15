// -----------------------------------------------------------------------
// <copyright file="GrpcTrailersEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Grpc.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.Grpc.Trailers.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the GrpcTrailers emitter. §1.20 fail-path proof:
/// drives the emitter with valid + 3 deliberate-drift specs and asserts
/// both the emit shape and the diagnostics surfaced for invalid inputs.
/// </summary>
public sealed class GrpcTrailersEmitterTests
{
    [Fact]
    public void Emit_ValidSingleEntry_EmitsConstantAndAllTrailers()
    {
        var spec = MakeSpec(new GrpcTrailerEntry("ERROR_CODE", "d2_error_code", "doc"));

        var result = GrpcTrailersEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain("public const string ERROR_CODE = \"d2_error_code\";");
        result.GeneratedSource.Should()
            .Contain("namespace DcsvIo.D2.Auth.Grpc.Status;");
        result.GeneratedSource.Should().Contain("public static class D2GrpcTrailers");
        result.GeneratedSource.Should()
            .Contain("public static IReadOnlyList<string> AllTrailers => sr_allTrailers;");
    }

    [Fact]
    public void Emit_TraceIdCamelCase_PinnedInGeneratedOutput()
    {
        // Regression pin for the wire-breaking casing fix on the gRPC
        // trailer key: `traceid` (lowercase) -> `traceId` (camelCase),
        // matching the HTTP ProblemDetails extension key.
        var spec = MakeSpec(new GrpcTrailerEntry("TRACE_ID", "traceId", "doc"));

        var result = GrpcTrailersEmitter.Emit(spec);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should()
            .Contain("public const string TRACE_ID = \"traceId\";");
        result.GeneratedSource.Should()
            .NotContain("public const string TRACE_ID = \"traceid\";");
    }

    // ---------------------------------------------------------------
    // §1.20 fail-path proof — 3 deliberate drift cases.
    // ---------------------------------------------------------------

    [Fact]
    public void Emit_DuplicateConstName_EmitsDuplicateConstNameDiagnostic()
    {
        // DRIFT CASE 1: two entries declaring the same constName 'X'.
        var spec = MakeSpec(
            new GrpcTrailerEntry("X", "a", "doc"),
            new GrpcTrailerEntry("X", "b", "doc"));

        var result = GrpcTrailersEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateConstName);
    }

    [Fact]
    public void Emit_DuplicateValue_EmitsDuplicateValueDiagnostic()
    {
        // DRIFT CASE 2: two entries declaring the same wire value 'a'.
        var spec = MakeSpec(
            new GrpcTrailerEntry("X", "a", "doc"),
            new GrpcTrailerEntry("Y", "a", "doc"));

        var result = GrpcTrailersEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.DuplicateValue);
    }

    [Fact]
    public void Emit_InvalidConstName_EmitsInvalidConstNameDiagnostic()
    {
        // DRIFT CASE 3: constName violates UPPER_SNAKE_CASE pattern.
        var spec = MakeSpec(new GrpcTrailerEntry("lowerCase", "a", "doc"));

        var result = GrpcTrailersEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.InvalidConstName);
    }

    [Fact]
    public void Emit_EmptyValue_EmitsEmptyValueDiagnostic()
    {
        var spec = MakeSpec(new GrpcTrailerEntry("X", "  ", "doc"));

        var result = GrpcTrailersEmitter.Emit(spec);

        result.Diagnostics.Should()
            .ContainSingle(d => d.DescriptorId == DiagnosticIds.EmptyValue);
    }

    private static GrpcTrailersSpec MakeSpec(params GrpcTrailerEntry[] entries) =>
        new(entries.ToImmutableArray());
}
