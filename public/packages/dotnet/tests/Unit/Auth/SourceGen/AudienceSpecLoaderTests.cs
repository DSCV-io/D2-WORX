// -----------------------------------------------------------------------
// <copyright file="AudienceSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Audiences.SourceGen;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="AudienceSpecLoader.Load"/>. The loader is
/// responsible only for JSON-shape validation (D2AUD001 — malformed spec); all
/// semantic validation (name shape, URL parsing, duplicates) is delegated to
/// <see cref="AudiencesEmitter"/> and tested separately.
/// </summary>
public sealed class AudienceSpecLoaderTests
{
    // ----------------------------------------------------------------------
    // Happy path
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_HappyPath_ReturnsSpecWithEntries()
    {
        const string json = """
        {
          "audiences": [
            {
              "name": "Files",
              "url": "https://files.internal",
              "description": "D2 Files service."
            }
          ]
        }
        """;

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Audiences.Should().HaveCount(1);
        var audience = result.Spec.Audiences[0];
        audience.Name.Should().Be("Files");
        audience.Url.Should().Be("https://files.internal");
        audience.Description.Should().Be("D2 Files service.");
    }

    [Fact]
    public void Load_DescriptionAbsent_ReturnsSpecWithNullDescription()
    {
        const string json = """
        { "audiences": [ { "name": "Files", "url": "https://files.internal" } ] }
        """;

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Audiences[0].Description.Should().BeNull();
    }

    [Fact]
    public void Load_MultipleAudiences_PreservesOrder()
    {
        const string json = """
        {
          "audiences": [
            { "name": "Zeta",    "url": "https://z.internal" },
            { "name": "Alpha",   "url": "https://a.internal" },
            { "name": "Mu",      "url": "https://m.internal" }
          ]
        }
        """;

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Audiences.Select(a => a.Name)
            .Should().ContainInOrder("Zeta", "Alpha", "Mu");
    }

    // ----------------------------------------------------------------------
    // D2AUD001 — malformed JSON / schema-violating
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("{not valid json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("\"a string\"")]
    [InlineData("[]")]
    public void Load_NonObjectRoot_EmitsD2AUD001(string json)
    {
        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingAudiencesArray_EmitsD2AUD001()
    {
        const string json = """{ "notAudiences": [] }""";

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("audiences");
    }

    [Fact]
    public void Load_AudiencesIsObjectNotArray_EmitsD2AUD001()
    {
        const string json = """{ "audiences": { "x": 1 } }""";

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_AudienceEntryIsString_EmitsD2AUD001()
    {
        const string json = """{ "audiences": ["not an object"] }""";

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("audiences[0]");
    }

    [Fact]
    public void Load_AudienceMissingName_EmitsD2AUD001()
    {
        const string json = """{ "audiences": [ { "url": "https://x.internal" } ] }""";

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("name");
    }

    [Fact]
    public void Load_AudienceMissingUrl_EmitsD2AUD001()
    {
        const string json = """{ "audiences": [ { "name": "Files" } ] }""";

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("url");
    }

    [Fact]
    public void Load_AudienceNameIsNumber_EmitsD2AUD001()
    {
        const string json = """
        { "audiences": [ { "name": 42, "url": "https://x.internal" } ] }
        """;

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_AudienceUrlIsNumber_EmitsD2AUD001()
    {
        const string json = """
        { "audiences": [ { "name": "Files", "url": 42 } ] }
        """;

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    // ----------------------------------------------------------------------
    // Adversarial — extra unknown properties
    //
    // The schema's additionalProperties is `false` (editor-time gate), but the
    // loader's responsibility ends at JSON-shape validation. Unknown properties
    // on an audience entry are silently ignored at load-time — schema-level gate
    // catches them in editors / IDEs / a JSON-Schema CLI.
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_UnknownExtraPropertyOnAudience_IsIgnoredByLoader()
    {
        const string json = """
        {
          "audiences": [
            { "name": "Files", "url": "https://files.internal",
              "extraField": "ignored", "anotherExtra": 42 }
          ]
        }
        """;

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Audiences.Should().HaveCount(1);
        result.Spec.Audiences[0].Name.Should().Be("Files");
    }

    [Fact]
    public void Load_DescriptionWrongType_IsTreatedAsAbsent()
    {
        // description is optional; a non-string value is silently treated as
        // absent rather than rejected — keeps loader lenient on optional fields.
        const string json = """
        { "audiences": [ { "name": "Files", "url": "https://x.internal", "description": 42 } ] }
        """;

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Audiences[0].Description.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Boundary — empty audiences array
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_EmptyAudiencesArray_ReturnsEmptySpec()
    {
        // Schema's minItems: 1 is editor-time only; loader accepts empty array.
        const string json = """{ "audiences": [] }""";

        var result = AudienceSpecLoader.Load("audiences.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Audiences.Should().BeEmpty();
    }
}
