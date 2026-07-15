// -----------------------------------------------------------------------
// <copyright file="EncryptionFrameSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.EncryptionFrame.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the EncryptionFrame spec loader's JSON-shape
/// validation. Drives the loader directly (no Roslyn host) and asserts the
/// <c>EmitDiagnostic</c> records surfaced for malformed input.
/// </summary>
public sealed class EncryptionFrameSpecLoaderTests
{
    private const string _PATH = "spec.json";

    private const string _VALID_CONSTRAINTS = """
    "constraints": {
      "minKidLength": 1,
      "maxKidLength": 64,
      "nonceLength": 12,
      "tagLength": 16,
      "minFrameSize": 30
    }
    """;

    [Fact]
    public void Load_ValidSpec_ReturnsPopulatedSpec()
    {
        var json = $$"""
        {
          "version": 1,
          "fields": [
            {
              "constName": "VERSION",
              "offset": 0,
              "length": 1,
              "kind": "byte_fixed",
              "doc": "Version field."
            }
          ],
          {{_VALID_CONSTRAINTS}}
        }
        """;

        var result = EncryptionFrameSpecLoader.Load(_PATH, json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Version.Should().Be(1);
        result.Spec.Fields.Should().HaveCount(1);
        var field = result.Spec.Fields[0];
        field.ConstName.Should().Be("VERSION");
        field.Offset.Should().Be(0);
        field.Length.Should().Be(1);
        field.Kind.Should().Be("byte_fixed");
        result.Spec.Constraints.NonceLength.Should().Be(12);
        result.Spec.Constraints.TagLength.Should().Be(16);
    }

    [Fact]
    public void Load_MalformedJson_ReturnsMalformedSpecDiagnostic()
    {
        var result = EncryptionFrameSpecLoader.Load(_PATH, "{not valid json");

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_RootNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var result = EncryptionFrameSpecLoader.Load(_PATH, "[]");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingVersion_ReturnsMalformedSpecDiagnostic()
    {
        var json = $$"""
        {
          "fields": [],
          {{_VALID_CONSTRAINTS}}
        }
        """;

        var result = EncryptionFrameSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingFieldsArray_ReturnsMalformedSpecDiagnostic()
    {
        var json = $$"""
        {
          "version": 1,
          {{_VALID_CONSTRAINTS}}
        }
        """;

        var result = EncryptionFrameSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingConstraints_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "version": 1,
          "fields": []
        }
        """;

        var result = EncryptionFrameSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_FieldMissingConstName_ReturnsMalformedSpecDiagnostic()
    {
        var json = $$"""
        {
          "version": 1,
          "fields": [
            {
              "offset": 0,
              "length": 1,
              "kind": "byte_fixed",
              "doc": "d"
            }
          ],
          {{_VALID_CONSTRAINTS}}
        }
        """;

        var result = EncryptionFrameSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ConstraintsMissingNonceLength_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "version": 1,
          "fields": [],
          "constraints": {
            "minKidLength": 1,
            "maxKidLength": 64,
            "tagLength": 16,
            "minFrameSize": 30
          }
        }
        """;

        var result = EncryptionFrameSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EmptyFieldsArray_ReturnsEmptySpec()
    {
        var json = $$"""
        {
          "version": 1,
          "fields": [],
          {{_VALID_CONSTRAINTS}}
        }
        """;

        var result = EncryptionFrameSpecLoader.Load(_PATH, json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Fields.Should().BeEmpty();
    }
}
