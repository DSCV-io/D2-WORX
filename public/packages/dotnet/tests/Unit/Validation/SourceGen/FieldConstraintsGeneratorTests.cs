// -----------------------------------------------------------------------
// <copyright file="FieldConstraintsGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Validation.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests for the field-constraints SrcGen
/// — drive <see cref="FieldConstraintsGenerator"/> via a synthetic
/// <see cref="CSharpGeneratorDriver"/> rather than the build pipeline.
/// </summary>
public sealed class FieldConstraintsGeneratorTests
{
    private const string _TARGET = "DcsvIo.D2.Validation.Abstractions";

    private const string _SAMPLE_SPEC = """
    {
      "constraints": [
        { "name": "EMAIL_MAX", "value": 254, "doc": "Email max." }
      ],
      "enums": [
        {
          "name": "BiologicalSex",
          "backing": "byte",
          "doc": "Sex.",
          "members": [ { "name": "Unspecified", "doc": "Unknown." } ]
        }
      ]
    }
    """;

    [Fact]
    public void Generator_TargetAssembly_EmitsTwoTrees()
    {
        var driver = RunGenerator(assemblyName: _TARGET, specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(2);
        var fileNames = result.GeneratedTrees
            .Select(t => Path.GetFileName(t.FilePath))
            .ToList();
        fileNames.Should().Contain("FieldConstraints.g.cs");
        fileNames.Should().Contain("Taxonomy.g.cs");
    }

    [Fact]
    public void Generator_NonTargetAssembly_EmitsNothing()
    {
        var driver = RunGenerator(assemblyName: "Some.Other.Assembly", specJson: _SAMPLE_SPEC);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpec_EmitsNothing()
    {
        var driver = RunGenerator(assemblyName: _TARGET, specJson: null);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_MalformedSpec_EmitsD2FC001()
    {
        var driver = RunGenerator(assemblyName: _TARGET, specJson: "{not valid");

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Generator_RunTwice_SameInputs_ProducesIdenticalOutput()
    {
        var first = RunGenerator(assemblyName: _TARGET, specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => Normalize(t.ToString()))
            .ToList();
        var second = RunGenerator(assemblyName: _TARGET, specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => Normalize(t.ToString()))
            .ToList();

        second.Count.Should().Be(first.Count);
        for (var i = 0; i < first.Count; i++)
            second[i].Should().Be(first[i]);
    }

    [Fact]
    public void Generator_MultipleSpecFiles_SelectsOrdinalFirstByPath()
    {
        // The generator applies specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).First()
        // when more than one spec AdditionalFile is registered (FieldConstraintsGenerator.cs:61).
        // "aaa/field-constraints.spec.json" < "zzz/field-constraints.spec.json" by Ordinal →
        // the winner is the spec containing EMAIL_MAX=254 (the 'aaa' path).
        const string winning_spec = """
        {
          "constraints": [
            { "name": "EMAIL_MAX", "value": 254, "doc": "Email max (winner)." }
          ],
          "enums": []
        }
        """;
        const string losing_spec = """
        {
          "constraints": [
            { "name": "PHONE_MAX", "value": 15, "doc": "Phone max (loser)." }
          ],
          "enums": []
        }
        """;

        var compilation = CSharpCompilation.Create(
            assemblyName: _TARGET,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new FieldConstraintsGenerator().AsSourceGenerator();

        // Two spec files — Ordinal sort gives 'aaa/...' < 'zzz/...' → winner first.
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("zzz/field-constraints.spec.json", losing_spec),
            new InMemoryAdditionalText("aaa/field-constraints.spec.json", winning_spec));

        var driver = CSharpGeneratorDriver
            .Create(generators: [generator], additionalTexts: additionalTexts)
            .RunGenerators(compilation);

        var result = driver.GetRunResult();

        // Exactly two output trees (FieldConstraints.g.cs + Taxonomy.g.cs) —
        // proves the generator ran with one spec, not zero or two.
        result.GeneratedTrees.Should().HaveCount(2);

        // FieldConstraints.g.cs emitted from the winning spec must contain EMAIL_MAX.
        var constraintsSource = result.GeneratedTrees
            .Single(t => System.IO.Path.GetFileName(t.FilePath) == "FieldConstraints.g.cs")
            .ToString();
        constraintsSource.Should().Contain("EMAIL_MAX");
        constraintsSource.Should().NotContain("PHONE_MAX");
    }

    private static GeneratorDriver RunGenerator(string assemblyName, string? specJson)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new FieldConstraintsGenerator().AsSourceGenerator();

        var additionalTexts = specJson is null
            ? ImmutableArray<AdditionalText>.Empty
            : ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
                "field-constraints.spec.json",
                specJson));

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts);

        return driver.RunGenerators(compilation);
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();

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
