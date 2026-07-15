// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.EncryptionDomains.SourceGen;
using Xunit;

/// <summary>
/// Pure-logic tests for the EncryptionDomains spec loader's JSON-shape
/// validation. Drives the loader directly (no Roslyn host) and asserts the
/// <c>EmitDiagnostic</c> records surfaced for malformed input.
/// </summary>
public sealed class EncryptionDomainsSpecLoaderTests
{
    private const string _PATH = "spec.json";

    [Fact]
    public void Load_ValidSpec_ReturnsPopulatedSpec()
    {
        var json = """
        {
          "domains": [
            {
              "constName": "AUDIT",
              "value": "audit",
              "doc": "Audit keyring domain."
            }
          ]
        }
        """;

        var result = EncryptionDomainsSpecLoader.Load(_PATH, json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Domains.Should().HaveCount(1);
        var entry = result.Spec.Domains[0];
        entry.ConstName.Should().Be("AUDIT");
        entry.Value.Should().Be("audit");
        entry.Doc.Should().Be("Audit keyring domain.");
    }

    [Fact]
    public void Load_MalformedJson_ReturnsMalformedSpecDiagnostic()
    {
        var result = EncryptionDomainsSpecLoader.Load(_PATH, "{not valid json");

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_RootNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var result = EncryptionDomainsSpecLoader.Load(_PATH, "[]");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingDomainsArray_ReturnsMalformedSpecDiagnostic()
    {
        var result = EncryptionDomainsSpecLoader.Load(_PATH, "{}");

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryMissingConstName_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "domains": [
            {
              "value": "v",
              "doc": "d"
            }
          ]
        }
        """;

        var result = EncryptionDomainsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryMissingValue_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "domains": [
            {
              "constName": "X",
              "doc": "d"
            }
          ]
        }
        """;

        var result = EncryptionDomainsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryMissingDoc_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "domains": [
            {
              "constName": "X",
              "value": "v"
            }
          ]
        }
        """;

        var result = EncryptionDomainsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EntryNotObject_ReturnsMalformedSpecDiagnostic()
    {
        var json = """{ "domains": ["NOT_AN_OBJECT"] }""";

        var result = EncryptionDomainsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_EmptyDomainsArray_ReturnsEmptySpec()
    {
        var result = EncryptionDomainsSpecLoader.Load(_PATH, """{ "domains": [] }""");

        result.Diagnostic.Should().BeNull();
        result.Spec!.Domains.Should().BeEmpty();
    }

    [Fact]
    public void Load_EntryWithoutMode_LeavesModeAndConsumerNull()
    {
        var json = """
        {
          "domains": [
            { "constName": "AUDIT", "value": "audit", "doc": "d" }
          ]
        }
        """;

        var result = EncryptionDomainsSpecLoader.Load(_PATH, json);

        result.Diagnostic.Should().BeNull();
        var entry = result.Spec!.Domains[0];
        entry.Mode.Should().BeNull();
        entry.ConsumerService.Should().BeNull();
    }

    [Fact]
    public void Load_SealedEntry_ParsesModeAndConsumerService()
    {
        var json = """
        {
          "domains": [
            {
              "constName": "AUDIT",
              "value": "audit",
              "mode": "sealed",
              "consumerService": "audit",
              "doc": "d"
            }
          ]
        }
        """;

        var result = EncryptionDomainsSpecLoader.Load(_PATH, json);

        result.Diagnostic.Should().BeNull();
        var entry = result.Spec!.Domains[0];
        entry.Mode.Should().Be("sealed");
        entry.ConsumerService.Should().Be("audit");
    }

    [Fact]
    public void Load_ModeNotString_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "domains": [
            { "constName": "X", "value": "x", "mode": 7, "doc": "d" }
          ]
        }
        """;

        var result = EncryptionDomainsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ConsumerServiceNotString_ReturnsMalformedSpecDiagnostic()
    {
        var json = """
        {
          "domains": [
            {
              "constName": "X",
              "value": "x",
              "mode": "sealed",
              "consumerService": true,
              "doc": "d"
            }
          ]
        }
        """;

        var result = EncryptionDomainsSpecLoader.Load(_PATH, json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }
}
