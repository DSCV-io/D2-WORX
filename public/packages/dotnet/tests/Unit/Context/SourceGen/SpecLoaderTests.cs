// -----------------------------------------------------------------------
// <copyright file="SpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Context.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.Context.SourceGen;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="SpecLoader.Load"/>. The loader is
/// responsible only for JSON-shape validation (D2CTX001 — malformed spec); all
/// semantic validation (closed type vocab, name collisions, derived rules)
/// happens in the emitters and is tested separately.
/// </summary>
public sealed class SpecLoaderTests
{
    // ----------------------------------------------------------------------
    // Happy path
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_HappyPath_ReturnsFullyPopulatedSpec()
    {
        const string json = """
        {
          "name": "IAuthContext",
          "namespace": "DcsvIo.D2.AuthContext.Abstractions",
          "description": "Test description",
          "extends": null,
          "sections": [
            {
              "name": "Token",
              "properties": [
                {
                  "name": "IsAuthenticated",
                  "type": "bool?",
                  "trinaryAuth": true,
                  "doc": "Whether the request is authenticated."
                }
              ]
            }
          ]
        }
        """;

        var result = SpecLoader.Load("IAuthContext.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();

        var spec = result.Spec!;
        spec.Name.Should().Be("IAuthContext");
        spec.Namespace.Should().Be("DcsvIo.D2.AuthContext.Abstractions");
        spec.Description.Should().Be("Test description");
        spec.Extends.Should().BeNull();
        spec.Sections.Should().HaveCount(1);

        var prop = spec.Sections[0].Properties[0];
        prop.Name.Should().Be("IsAuthenticated");
        prop.Type.Should().Be("bool?");
        prop.TrinaryAuth.Should().BeTrue();
        prop.Doc.Should().Be("Whether the request is authenticated.");
    }

    [Fact]
    public void Load_PropertyAllOptionalFields_ParseCleanly()
    {
        const string json = """
        {
          "name": "IThing",
          "namespace": "X.Y",
          "extends": "X.Y.IBase",
          "sections": [
            {
              "name": "S",
              "properties": [
                {
                  "name": "Field",
                  "type": "string?",
                  "claim": "d2_field",
                  "derived": "actorChain",
                  "default": "null",
                  "doc": "Doc"
                }
              ]
            }
          ]
        }
        """;

        var result = SpecLoader.Load("IThing.spec.json", json);

        result.Diagnostic.Should().BeNull();
        var prop = result.Spec!.Sections[0].Properties[0];
        prop.Claim.Should().Be("d2_field");
        prop.Derived.Should().Be("actorChain");
        prop.Default.Should().Be("null");
        result.Spec.Extends.Should().Be("X.Y.IBase");
    }

    [Fact]
    public void Load_RedactTrueOnProperty_IsParsedAsTrue()
    {
        const string json = """
        {
          "name": "IThing",
          "namespace": "X.Y",
          "sections": [
            {
              "name": "S",
              "properties": [
                { "name": "Pii", "type": "string?", "redact": true }
              ]
            }
          ]
        }
        """;

        var result = SpecLoader.Load("IThing.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Sections[0].Properties[0].Redact.Should().BeTrue();
    }

    [Fact]
    public void Load_RedactFalseOnProperty_IsParsedAsFalse()
    {
        const string json = """
        {
          "name": "IThing",
          "namespace": "X.Y",
          "sections": [
            {
              "name": "S",
              "properties": [
                { "name": "Pii", "type": "string?", "redact": false }
              ]
            }
          ]
        }
        """;

        var result = SpecLoader.Load("IThing.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Sections[0].Properties[0].Redact.Should().BeFalse();
    }

    [Fact]
    public void Load_RedactOmitted_DefaultsToFalse()
    {
        const string json = """
        {
          "name": "IThing",
          "namespace": "X.Y",
          "sections": [
            {
              "name": "S",
              "properties": [ { "name": "Plain", "type": "string?" } ]
            }
          ]
        }
        """;

        var result = SpecLoader.Load("IThing.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Sections[0].Properties[0].Redact.Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // D2CTX001 — malformed JSON / schema-violating
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("{not valid")]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("\"a string\"")]
    [InlineData("42")]
    [InlineData("null")]
    public void Load_NonObjectRoot_EmitsD2CTX001(string json)
    {
        var result = SpecLoader.Load("test.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingName_EmitsD2CTX001()
    {
        const string json = """
        { "namespace": "X.Y", "sections": [] }
        """;

        var result = SpecLoader.Load("test.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("name");
    }

    [Fact]
    public void Load_MissingNamespace_EmitsD2CTX001()
    {
        const string json = """
        { "name": "IFoo", "sections": [] }
        """;

        var result = SpecLoader.Load("test.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("namespace");
    }

    [Fact]
    public void Load_MissingSections_EmitsD2CTX001()
    {
        const string json = """
        { "name": "IFoo", "namespace": "X.Y" }
        """;

        var result = SpecLoader.Load("test.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("sections");
    }

    [Fact]
    public void Load_SectionMissingName_EmitsD2CTX001()
    {
        const string json = """
        { "name": "IFoo", "namespace": "X.Y", "sections": [ { "properties": [] } ] }
        """;

        var result = SpecLoader.Load("test.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("name");
    }

    [Fact]
    public void Load_SectionMissingProperties_EmitsD2CTX001()
    {
        const string json = """
        { "name": "IFoo", "namespace": "X.Y", "sections": [ { "name": "S" } ] }
        """;

        var result = SpecLoader.Load("test.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("properties");
    }

    [Fact]
    public void Load_PropertyMissingName_EmitsD2CTX001()
    {
        const string json = """
        {
          "name": "IFoo", "namespace": "X.Y",
          "sections": [
            { "name": "S", "properties": [ { "type": "string?" } ] }
          ]
        }
        """;

        var result = SpecLoader.Load("test.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("name");
    }

    [Fact]
    public void Load_PropertyMissingType_EmitsD2CTX001()
    {
        const string json = """
        {
          "name": "IFoo", "namespace": "X.Y",
          "sections": [
            { "name": "S", "properties": [ { "name": "X" } ] }
          ]
        }
        """;

        var result = SpecLoader.Load("test.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("type");
    }

    [Fact]
    public void Load_PropertyIsNotObject_EmitsD2CTX001()
    {
        const string json = """
        {
          "name": "IFoo", "namespace": "X.Y",
          "sections": [
            { "name": "S", "properties": [ "not an object" ] }
          ]
        }
        """;

        var result = SpecLoader.Load("test.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_NonStringNameWrongType_EmitsD2CTX001()
    {
        const string json = """
        { "name": 42, "namespace": "X.Y", "sections": [] }
        """;

        var result = SpecLoader.Load("test.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    // ----------------------------------------------------------------------
    // Adversarial — extra unknown properties (silently allowed by loader;
    // schema's additionalProperties:false catches them at editor time)
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_UnknownExtraPropertyAtRoot_IsIgnored()
    {
        const string json = """
        {
          "name": "IFoo", "namespace": "X.Y", "sections": [],
          "extraField": "ignored"
        }
        """;

        var result = SpecLoader.Load("test.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
    }

    [Fact]
    public void Load_TrinaryAuthMissing_DefaultsToFalse()
    {
        const string json = """
        {
          "name": "IFoo", "namespace": "X.Y",
          "sections": [
            { "name": "S", "properties": [ { "name": "X", "type": "string?" } ] }
          ]
        }
        """;

        var result = SpecLoader.Load("test.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Sections[0].Properties[0].TrinaryAuth.Should().BeFalse();
    }

    [Fact]
    public void Load_EmptySectionsArray_IsAccepted()
    {
        // Schema's minItems: 1 is editor-time only.
        const string json = """
        { "name": "IFoo", "namespace": "X.Y", "sections": [] }
        """;

        var result = SpecLoader.Load("test.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Sections.Should().BeEmpty();
    }
}
