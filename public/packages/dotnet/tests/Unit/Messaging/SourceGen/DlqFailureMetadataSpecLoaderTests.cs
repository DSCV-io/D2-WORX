// -----------------------------------------------------------------------
// <copyright file="DlqFailureMetadataSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.Messaging.DlqMetadata.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the DlqFailureMetadata spec loader's JSON-shape
/// validation. Drives the loader directly (no Roslyn host) and asserts the
/// <c>EmitDiagnostic</c> records surfaced for malformed input.
/// </summary>
public sealed class DlqFailureMetadataSpecLoaderTests
{
    private const string _PATH = "spec.json";

    [Fact]
    public void Load_ValidSpec_ReturnsPopulatedSpec()
    {
        var json = """
        {
          "fields": [
            {
              "constName": "CAUSE",
              "value": "cause",
              "doc": "DLQ failure cause field."
            }
          ],
          "causes": [
            {
              "constName": "HANDLER_EXCEPTION",
              "value": "HANDLER_EXCEPTION",
              "doc": "Handler threw."
            }
          ]
        }
        """;

        var result = DlqFailureMetadataSpecLoader.Load(_PATH, json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Fields.Should().HaveCount(1);
        result.Spec.Causes.Should().HaveCount(1);
        result.Spec.Fields[0].ConstName.Should().Be("CAUSE");
        result.Spec.Fields[0].Value.Should().Be("cause");
        result.Spec.Causes[0].ConstName.Should().Be("HANDLER_EXCEPTION");
    }

    [Fact]
    public void Load_MalformedJson_ReturnsMalformedSpecDiagnostic()
    {
        var result = DlqFailureMetadataSpecLoader.Load(_PATH, "{not valid json");

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_RootNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var result = DlqFailureMetadataSpecLoader.Load(_PATH, "[]");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingFieldsArray_ReturnsMalformedSpecDiagnostic()
    {
        var result = DlqFailureMetadataSpecLoader.Load(
            _PATH,
            """{ "causes": [] }""");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingCausesArray_ReturnsMalformedSpecDiagnostic()
    {
        var result = DlqFailureMetadataSpecLoader.Load(
            _PATH,
            """{ "fields": [] }""");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_FieldEntryMissingConstName_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "fields": [
            {
              "value": "v",
              "doc": "d"
            }
          ],
          "causes": []
        }
        """;

        var result = DlqFailureMetadataSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_CauseEntryMissingValue_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "fields": [],
          "causes": [
            {
              "constName": "X",
              "doc": "d"
            }
          ]
        }
        """;

        var result = DlqFailureMetadataSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_FieldEntryNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var json = """{ "fields": ["NOT_AN_OBJECT"], "causes": [] }""";

        var result = DlqFailureMetadataSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_BothArraysEmpty_ReturnsEmptySpec()
    {
        var result = DlqFailureMetadataSpecLoader.Load(
            _PATH,
            """{ "fields": [], "causes": [] }""");

        result.Diagnostic.Should().BeNull();
        result.Spec!.Fields.Should().BeEmpty();
        result.Spec.Causes.Should().BeEmpty();
    }
}
