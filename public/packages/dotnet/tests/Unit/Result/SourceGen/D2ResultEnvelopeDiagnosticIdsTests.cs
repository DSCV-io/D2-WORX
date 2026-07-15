// -----------------------------------------------------------------------
// <copyright file="D2ResultEnvelopeDiagnosticIdsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.Result.Envelope.SourceGen;
using Xunit;

/// <summary>
/// Pins the <c>D2DRE###</c> diagnostic IDs surfaced by the
/// d2result-envelope source generator. Cross-language counterparts ride
/// in <c>tools/ts-codegen/src/lib/diagnostics.ts</c> under <c>DRE_*</c>
/// keys — identical IDs by spec since both sides describe the same
/// predicate violations against the same spec source.
/// </summary>
public sealed class D2ResultEnvelopeDiagnosticIdsTests
{
    [Fact]
    public void MalformedSpec_IsD2DRE001()
    {
        DiagnosticIds.MalformedSpec.Should().Be("D2DRE001");
    }

    [Fact]
    public void DuplicateFieldConstName_IsD2DRE002()
    {
        DiagnosticIds.DuplicateFieldConstName.Should().Be("D2DRE002");
    }

    [Fact]
    public void DuplicateFieldValue_IsD2DRE003()
    {
        DiagnosticIds.DuplicateFieldValue.Should().Be("D2DRE003");
    }

    [Fact]
    public void InvalidConstName_IsD2DRE004()
    {
        DiagnosticIds.InvalidConstName.Should().Be("D2DRE004");
    }

    [Fact]
    public void EmptyValue_IsD2DRE005()
    {
        DiagnosticIds.EmptyValue.Should().Be("D2DRE005");
    }
}
