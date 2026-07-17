// -----------------------------------------------------------------------
// <copyright file="OtelMessagingTagsSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.OtelMessagingTags.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the OtelMessagingTags spec loader's JSON-shape
/// validation. Drives the loader directly (no Roslyn host) and asserts the
/// <c>EmitDiagnostic</c> records surfaced for malformed input.
/// </summary>
public sealed class OtelMessagingTagsSpecLoaderTests
{
    private const string _PATH = "spec.json";

    [Fact]
    public void Load_ValidSpec_ReturnsPopulatedSpec()
    {
        var json = """
        {
          "tags": [
            {
              "constName": "MESSAGING_SYSTEM",
              "value": "messaging.system",
              "doc": "OTel canonical messaging system tag."
            }
          ]
        }
        """;

        var result = OtelMessagingTagsSpecLoader.Load(_PATH, json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Tags.Should().HaveCount(1);
        var entry = result.Spec.Tags[0];
        entry.ConstName.Should().Be("MESSAGING_SYSTEM");
        entry.Value.Should().Be("messaging.system");
        entry.Doc.Should().Be("OTel canonical messaging system tag.");
    }

    [Fact]
    public void Load_MalformedJson_ReturnsMalformedSpecDiagnostic()
    {
        var result = OtelMessagingTagsSpecLoader.Load(_PATH, "{not valid json");

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_RootNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var result = OtelMessagingTagsSpecLoader.Load(_PATH, "[]");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingTagsArray_ReturnsMalformedSpecDiagnostic()
    {
        var result = OtelMessagingTagsSpecLoader.Load(_PATH, "{}");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryMissingConstName_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "tags": [
            {
              "value": "v",
              "doc": "d"
            }
          ]
        }
        """;

        var result = OtelMessagingTagsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryMissingValue_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "tags": [
            {
              "constName": "X",
              "doc": "d"
            }
          ]
        }
        """;

        var result = OtelMessagingTagsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryMissingDoc_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "tags": [
            {
              "constName": "X",
              "value": "v"
            }
          ]
        }
        """;

        var result = OtelMessagingTagsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var json = """{ "tags": ["NOT_AN_OBJECT"] }""";

        var result = OtelMessagingTagsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EmptyTagsArray_ReturnsEmptySpec()
    {
        var result = OtelMessagingTagsSpecLoader.Load(_PATH, """{ "tags": [] }""");

        result.Diagnostic.Should().BeNull();
        result.Spec!.Tags.Should().BeEmpty();
    }
}
