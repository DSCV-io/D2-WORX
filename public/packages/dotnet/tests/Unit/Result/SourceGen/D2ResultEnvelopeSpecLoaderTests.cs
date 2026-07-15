// -----------------------------------------------------------------------
// <copyright file="D2ResultEnvelopeSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.Result.Envelope.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the d2result-envelope spec loader. Drives
/// <see cref="D2ResultEnvelopeSpecLoader.Load"/> directly with synthetic
/// JSON inputs and asserts both happy-path parsing and the diagnostic
/// surface for malformed JSON / schema violations.
/// </summary>
public sealed class D2ResultEnvelopeSpecLoaderTests
{
    [Fact]
    public void Load_ValidSingleField_ParsesIntoSpec()
    {
        const string json = """
        {
          "fields": [
            { "constName": "SUCCESS", "value": "success", "doc": "The success flag." }
          ]
        }
        """;

        var result = D2ResultEnvelopeSpecLoader.Load("d2result-envelope.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Fields.Should().HaveCount(1);
        result.Spec.Fields[0].ConstName.Should().Be("SUCCESS");
        result.Spec.Fields[0].Value.Should().Be("success");
        result.Spec.Fields[0].Doc.Should().Be("The success flag.");
    }

    [Fact]
    public void Load_ValidMultipleFields_ParsesIntoSpecPreservingOrder()
    {
        const string json = """
        {
          "fields": [
            { "constName": "SUCCESS", "value": "success", "doc": "success doc" },
            { "constName": "DATA", "value": "data", "doc": "data doc" },
            { "constName": "STATUS_CODE", "value": "statusCode", "doc": "status code doc" }
          ]
        }
        """;

        var result = D2ResultEnvelopeSpecLoader.Load("d2result-envelope.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Fields.Should().HaveCount(3);
        result.Spec.Fields[0].ConstName.Should().Be("SUCCESS");
        result.Spec.Fields[1].ConstName.Should().Be("DATA");
        result.Spec.Fields[2].ConstName.Should().Be("STATUS_CODE");
    }

    [Fact]
    public void Load_MalformedJson_EmitsD2DRE001()
    {
        const string json = "{ this is not JSON";

        var result = D2ResultEnvelopeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_RootIsArray_EmitsD2DRE001()
    {
        const string json = "[]";

        var result = D2ResultEnvelopeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingFieldsArray_EmitsD2DRE001()
    {
        const string json = """{ "other": "field" }""";

        var result = D2ResultEnvelopeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_FieldsIsNotArray_EmitsD2DRE001()
    {
        const string json = """{ "fields": "not-an-array" }""";

        var result = D2ResultEnvelopeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_FieldMissingConstName_EmitsD2DRE001()
    {
        const string json = """
        {
          "fields": [
            { "value": "success", "doc": "doc" }
          ]
        }
        """;

        var result = D2ResultEnvelopeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_FieldMissingValue_EmitsD2DRE001()
    {
        const string json = """
        {
          "fields": [
            { "constName": "SUCCESS", "doc": "doc" }
          ]
        }
        """;

        var result = D2ResultEnvelopeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_FieldMissingDoc_EmitsD2DRE001()
    {
        const string json = """
        {
          "fields": [
            { "constName": "SUCCESS", "value": "success" }
          ]
        }
        """;

        var result = D2ResultEnvelopeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_FieldEntryIsNotObject_EmitsD2DRE001()
    {
        const string json = """{ "fields": ["string-not-object"] }""";

        var result = D2ResultEnvelopeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EmptyFieldsArray_ParsesWithNoEntries()
    {
        // Note: SCHEMA-level "minItems: 1" lives in schema.json (IDE-time
        // validation). The loader itself accepts an empty array — emitter
        // surface stays silent because there are no entries to validate.
        const string json = """{ "fields": [] }""";

        var result = D2ResultEnvelopeSpecLoader.Load("any.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Fields.Should().BeEmpty();
    }
}
