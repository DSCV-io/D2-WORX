// -----------------------------------------------------------------------
// <copyright file="WireShapesDiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.WireShapes.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.WireShapes.SourceGen;
using Xunit;

/// <summary>
/// Pins the <c>D2WS###</c> diagnostic IDs surfaced by the wire-shapes
/// source generator. Cross-language counterparts ride in
/// <c>tools/ts-codegen/src/lib/diagnostics.ts</c> under <c>WS_*</c> keys
/// — identical IDs by spec since both sides describe the same predicate
/// violations against the same spec source.
/// </summary>
public sealed class WireShapesDiagnosticIdsTests
{
    [Fact]
    public void MalformedSpec_IsD2WS001()
    {
        DiagnosticIds.MalformedSpec.Should().Be("D2WS001");
    }

    [Fact]
    public void DuplicatePropertyConstName_IsD2WS002()
    {
        DiagnosticIds.DuplicatePropertyConstName.Should().Be("D2WS002");
    }

    [Fact]
    public void DuplicatePropertyValue_IsD2WS003()
    {
        DiagnosticIds.DuplicatePropertyValue.Should().Be("D2WS003");
    }

    [Fact]
    public void InvalidConstName_IsD2WS004()
    {
        DiagnosticIds.InvalidConstName.Should().Be("D2WS004");
    }

    [Fact]
    public void MissingSpec_IsD2WS005()
    {
        DiagnosticIds.MissingSpec.Should().Be("D2WS005");
    }
}
