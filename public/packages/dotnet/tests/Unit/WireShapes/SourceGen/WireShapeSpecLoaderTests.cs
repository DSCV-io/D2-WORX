// -----------------------------------------------------------------------
// <copyright file="WireShapeSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.WireShapes.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.WireShapes.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the wire-shape spec loader. Drives
/// <see cref="WireShapeSpecLoader.Load"/> directly with synthetic JSON
/// inputs and asserts both happy-path parsing and the diagnostic
/// surface for malformed JSON / schema violations.
/// </summary>
public sealed class WireShapeSpecLoaderTests
{
    [Fact]
    public void Load_ValidSingleProperty_ParsesIntoSpec()
    {
        const string json = """
        {
          "properties": [
            { "constName": "KEY", "value": "key", "doc": "The key property." }
          ]
        }
        """;

        var result = WireShapeSpecLoader.Load("tk-message.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Properties.Should().HaveCount(1);
        result.Spec.Properties[0].ConstName.Should().Be("KEY");
        result.Spec.Properties[0].Value.Should().Be("key");
        result.Spec.Properties[0].Doc.Should().Be("The key property.");
    }

    [Fact]
    public void Load_ValidMultipleProperties_ParsesIntoSpecPreservingOrder()
    {
        const string json = """
        {
          "properties": [
            { "constName": "FIELD", "value": "field", "doc": "field doc" },
            { "constName": "ERRORS", "value": "errors", "doc": "errors doc" }
          ]
        }
        """;

        var result = WireShapeSpecLoader.Load("input-error.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Properties.Should().HaveCount(2);
        result.Spec.Properties[0].ConstName.Should().Be("FIELD");
        result.Spec.Properties[1].ConstName.Should().Be("ERRORS");
    }

    [Fact]
    public void Load_MalformedJson_EmitsD2WS001()
    {
        const string json = "{ this is not JSON";

        var result = WireShapeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_RootIsArray_EmitsD2WS001()
    {
        const string json = "[]";

        var result = WireShapeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingPropertiesArray_EmitsD2WS001()
    {
        const string json = """{ "other": "field" }""";

        var result = WireShapeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_PropertiesIsNotArray_EmitsD2WS001()
    {
        const string json = """{ "properties": "not-an-array" }""";

        var result = WireShapeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_PropertyMissingConstName_EmitsD2WS001()
    {
        const string json = """
        {
          "properties": [
            { "value": "key", "doc": "doc" }
          ]
        }
        """;

        var result = WireShapeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_PropertyMissingValue_EmitsD2WS001()
    {
        const string json = """
        {
          "properties": [
            { "constName": "KEY", "doc": "doc" }
          ]
        }
        """;

        var result = WireShapeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_PropertyMissingDoc_EmitsD2WS001()
    {
        const string json = """
        {
          "properties": [
            { "constName": "KEY", "value": "key" }
          ]
        }
        """;

        var result = WireShapeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_PropertyEntryIsNotObject_EmitsD2WS001()
    {
        const string json = """{ "properties": ["string-not-object"] }""";

        var result = WireShapeSpecLoader.Load("any.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EmptyPropertiesArray_ParsesWithNoEntries()
    {
        // Note: SCHEMA-level "minItems: 1" lives in schema.json (IDE-time
        // validation). The loader itself accepts an empty array — emitter
        // surface stays silent because there are no entries to validate.
        // This documents the loader's contract: no schema-level enforcement.
        const string json = """{ "properties": [] }""";

        var result = WireShapeSpecLoader.Load("any.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Properties.Should().BeEmpty();
    }
}
