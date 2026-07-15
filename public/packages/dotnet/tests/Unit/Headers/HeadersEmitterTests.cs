// -----------------------------------------------------------------------
// <copyright file="HeadersEmitterTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Headers;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.Headers.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic coverage for <see cref="HeadersEmitter"/>: happy-path
/// emission per catalog filter, invalid constName diagnostics, unknown
/// transport diagnostics, empty applicability diagnostics, and unknown
/// convention warnings.
/// </summary>
public sealed class HeadersEmitterTests
{
    [Fact]
    public void Emit_HappyPathHttpCatalog_EmitsConstantWithRightShape()
    {
        var spec = new HeadersSpec(ImmutableArray.Create(
            new HeaderEntry(
                Name: "X-D2-Trace",
                ConstName: "TRACE_HEADER",
                Applicability: ImmutableArray.Create("http"),
                Convention: "d2",
                Description: "Per-request trace header.")));

        var result = HeadersEmitter.Emit(
            spec,
            HeadersEmitter.CatalogFilter.Http,
            targetNamespace: "DcsvIo.D2.Headers.Http",
            className: "HttpHeaders");

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain("public static class HttpHeaders");
        result.GeneratedSource.Should().Contain(
            "public const string TRACE_HEADER = \"X-D2-Trace\";");
    }

    [Fact]
    public void Emit_InvalidConstName_EmitsInvalidConstNameDiagnostic()
    {
        var spec = new HeadersSpec(ImmutableArray.Create(
            new HeaderEntry(
                Name: "X-Bad",
                ConstName: "lower_bad",
                Applicability: ImmutableArray.Create("http"),
                Convention: "d2",
                Description: "Test.")));

        var result = HeadersEmitter.Emit(
            spec,
            HeadersEmitter.CatalogFilter.Http,
            targetNamespace: "D2.Test",
            className: "TestHeaders");

        result.Diagnostics.Should().ContainSingle()
            .Which.DescriptorId.Should().Be(DiagnosticIds.InvalidConstName);
    }

    [Fact]
    public void Emit_EmptyApplicability_EmitsEmptyApplicabilityDiagnostic()
    {
        var spec = new HeadersSpec(ImmutableArray.Create(
            new HeaderEntry(
                Name: "X-Empty",
                ConstName: "EMPTY_HDR",
                Applicability: ImmutableArray<string>.Empty,
                Convention: "d2",
                Description: "Test.")));

        var result = HeadersEmitter.Emit(
            spec,
            HeadersEmitter.CatalogFilter.Http,
            targetNamespace: "D2.Test",
            className: "TestHeaders");

        result.Diagnostics.Should().ContainSingle()
            .Which.DescriptorId.Should().Be(DiagnosticIds.EmptyApplicability);
    }

    [Fact]
    public void Emit_UnknownTransport_EmitsUnknownTransportDiagnostic()
    {
        var spec = new HeadersSpec(ImmutableArray.Create(
            new HeaderEntry(
                Name: "X-Bad",
                ConstName: "BAD_HDR",
                Applicability: ImmutableArray.Create("websocket"),
                Convention: "d2",
                Description: "Test.")));

        var result = HeadersEmitter.Emit(
            spec,
            HeadersEmitter.CatalogFilter.Http,
            targetNamespace: "D2.Test",
            className: "TestHeaders");

        result.Diagnostics.Should().ContainSingle()
            .Which.DescriptorId.Should().Be(DiagnosticIds.UnknownTransport);
    }

    [Fact]
    public void Emit_UnknownConvention_EmitsWarningButKeepsConstant()
    {
        var spec = new HeadersSpec(ImmutableArray.Create(
            new HeaderEntry(
                Name: "X-Vendor",
                ConstName: "VENDOR_HDR",
                Applicability: ImmutableArray.Create("http"),
                Convention: "made-up-convention",
                Description: "Test.")));

        var result = HeadersEmitter.Emit(
            spec,
            HeadersEmitter.CatalogFilter.Http,
            targetNamespace: "D2.Test",
            className: "TestHeaders");

        result.Diagnostics.Should().ContainSingle()
            .Which.DescriptorId.Should().Be(DiagnosticIds.UnknownConvention);
        result.GeneratedSource.Should().Contain(
            "public const string VENDOR_HDR = \"X-Vendor\";");
    }

    [Fact]
    public void Emit_CrossTransportEntry_AppearsInCommonCatalog()
    {
        var spec = new HeadersSpec(ImmutableArray.Create(
            new HeaderEntry(
                Name: "x-d2-context",
                ConstName: "CONTEXT",
                Applicability: ImmutableArray.Create("http", "amqp", "grpc"),
                Convention: "d2",
                Description: "Cross-transport context header.")));

        var result = HeadersEmitter.Emit(
            spec,
            HeadersEmitter.CatalogFilter.Common,
            targetNamespace: "D2.Test",
            className: "TestCommon");

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedSource.Should().Contain(
            "public const string CONTEXT = \"x-d2-context\";");
    }
}
