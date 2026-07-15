// -----------------------------------------------------------------------
// <copyright file="AudiencesGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Audiences.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests — drive <see cref="AudiencesGenerator"/>
/// via a synthetic <see cref="CSharpGeneratorDriver"/> rather than the build
/// pipeline. Asserts assembly-name gating, AdditionalFiles wiring, missing-spec
/// + malformed-spec degradation paths, and cache stability.
/// </summary>
public sealed class AudiencesGeneratorTests
{
    private const string _SAMPLE_SPEC = """
    {
      "audiences": [
        { "name": "Files", "url": "https://files.internal" },
        { "name": "Notifications", "url": "https://notifications.internal" }
      ]
    }
    """;

    private const string _PRIVATE_SPEC = """
    {
      "audiences": [
        { "name": "Courier", "url": "https://courier.internal" },
        { "name": "Audit", "url": "https://audit.internal" }
      ]
    }
    """;

    [Fact]
    public void Generator_TargetAssemblyWithSpec_EmitsAudiencesGeneratedSource()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().NotBeEmpty();
        var generated = result.GeneratedTrees.Single();
        Path.GetFileName(generated.FilePath).Should().Be("Audiences.g.cs");

        var src = generated.ToString();
        src.Should().Contain("public static partial class Audiences");
        src.Should().Contain("\"https://files.internal\"");
        src.Should().Contain("\"https://notifications.internal\"");
    }

    [Fact]
    public void Generator_NonTargetAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        // Generator no-ops for non-target assemblies — no Audiences.g.cs produced.
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpec_EmitsMissingSpecFileDiagnostic()
    {
        // No AdditionalText supplied — generator must fire D2AUD006 (MissingSpecFile).
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: null);

        var result = driver.GetRunResult();

        var diagnostics = result.Diagnostics;
        diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MissingSpecFile);

        // Even on missing spec, an empty shell file is emitted so downstream
        // compilation can still see the Audiences type (avoids cascade errors).
        result.GeneratedTrees.Should().HaveCount(1);
    }

    [Fact]
    public void Generator_MalformedSpec_EmitsMalformedSpecDiagnosticAndStillProducesEmptyShell()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: "{not valid");

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);

        // Empty shell still emitted on malformed input so downstream consumers
        // don't see "type does not exist" errors masking the real diagnostic.
        result.GeneratedTrees.Should().HaveCount(1);
    }

    [Fact]
    public void Generator_RunTwice_SameInputs_ProducesIdenticalOutput()
    {
        // Cache stability — identical inputs must produce identical generator
        // output (otherwise downstream incremental builds re-run unnecessarily).
        var first = RunGenerator(
                assemblyName: "DcsvIo.D2.Auth.Abstractions",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees.Single().ToString();

        var second = RunGenerator(
                assemblyName: "DcsvIo.D2.Auth.Abstractions",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees.Single().ToString();

        Normalize(second).Should().Be(Normalize(first));
    }

    // ----------------------------------------------------------------------
    // Dual-target / multi-spec merge (private Auth.Abstractions.Extensions host)
    // ----------------------------------------------------------------------

    [Fact]
    public void Generator_PrivateAuthAbstractionsExtensions_MultiSpec_EmitsProductAudiencesUnion()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Private.Auth.Abstractions.Extensions",
            specJson: null,
            multiSpecs:
            [
                ("public/audiences.spec.json", _SAMPLE_SPEC),
                ("private/audiences.spec.json", _PRIVATE_SPEC),
            ]);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        Path.GetFileName(result.GeneratedTrees.Single().FilePath)
            .Should().Be("ProductAudiences.g.cs");

        var src = result.GeneratedTrees.Single().ToString();
        src.Should().Contain("namespace DcsvIo.D2.Private.Auth;");
        src.Should().Contain("public static partial class ProductAudiences");
        src.Should().Contain("\"https://files.internal\"");
        src.Should().Contain("\"https://courier.internal\"");
        src.Should().Contain("\"https://audit.internal\"");
        src.Should().NotContain("public static partial class Audiences");
    }

    [Fact]
    public void Generator_WrongAssembly_WithMultiSpec_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specJson: null,
            multiSpecs:
            [
                ("public/audiences.spec.json", _SAMPLE_SPEC),
                ("private/audiences.spec.json", _PRIVATE_SPEC),
            ]);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    private static GeneratorDriver RunGenerator(
        string assemblyName,
        string? specJson,
        (string Path, string Content)[]? multiSpecs = null)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new AudiencesGenerator().AsSourceGenerator();

        ImmutableArray<AdditionalText> additionalTexts;
        if (multiSpecs is not null)
        {
            additionalTexts = ImmutableArray.CreateRange<AdditionalText>(
                multiSpecs.Select(s => new InMemoryAdditionalText(s.Path, s.Content)));
        }
        else if (specJson is null)
        {
            additionalTexts = ImmutableArray<AdditionalText>.Empty;
        }
        else
        {
            additionalTexts = ImmutableArray.Create<AdditionalText>(
                new InMemoryAdditionalText("audiences.spec.json", specJson));
        }

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts);

        return driver.RunGenerators(compilation);
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();

    /// <summary>
    /// Minimal AdditionalText shim for synthesizing AdditionalFiles in
    /// generator tests without filesystem I/O.
    /// </summary>
    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText r_text;

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            r_text = SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText GetText(
            System.Threading.CancellationToken cancellationToken = default) => r_text;
    }
}
