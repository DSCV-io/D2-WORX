// -----------------------------------------------------------------------
// <copyright file="D2ResultEnvelopeGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Result.Envelope.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests for the d2result-envelope SrcGen
/// — drive <see cref="D2ResultEnvelopeGenerator"/> via a synthetic
/// <see cref="CSharpGeneratorDriver"/> rather than the build pipeline.
/// Asserts the single-target dispatch behavior: DcsvIo.D2.Result gets
/// D2ResultEnvelopeFieldNames from d2result-envelope.spec.json; anything
/// else emits nothing.
/// </summary>
public sealed class D2ResultEnvelopeGeneratorTests
{
    private const string _TARGET_ASSEMBLY = "DcsvIo.D2.Result";

    private const string _VALID_SPEC = """
    {
      "fields": [
        { "constName": "SUCCESS", "value": "success", "doc": "Success." },
        { "constName": "DATA", "value": "data", "doc": "Data." },
        { "constName": "MESSAGES", "value": "messages", "doc": "Messages." }
      ]
    }
    """;

    [Fact]
    public void Generator_TargetAssembly_EmitsD2ResultEnvelopeFieldNamesG()
    {
        var driver = RunGenerator(
            assemblyName: _TARGET_ASSEMBLY,
            specs: new[] { ("d2result-envelope.spec.json", _VALID_SPEC) });

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        Path.GetFileName(result.GeneratedTrees[0].FilePath)
            .Should().Be("D2ResultEnvelopeFieldNames.g.cs");
        result.GeneratedTrees[0].ToString().Should().Contain(
            "namespace DcsvIo.D2.Result;");
        result.GeneratedTrees[0].ToString().Should().Contain(
            "public const string SUCCESS = \"success\";");
        result.GeneratedTrees[0].ToString().Should().Contain(
            "public const string DATA = \"data\";");
        result.GeneratedTrees[0].ToString().Should().Contain(
            "public const string MESSAGES = \"messages\";");
    }

    [Fact]
    public void Generator_NonTargetAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specs: new[] { ("d2result-envelope.spec.json", _VALID_SPEC) });

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpec_EmitsNothing()
    {
        // Loader silently exits on no spec — unlike WireShapesGenerator,
        // this single-target generator doesn't fire MissingSpec because
        // the spec might legitimately be absent during partial-build
        // scenarios. The hand-written D2Result.cs gives a clear compile
        // error referencing D2ResultEnvelopeFieldNames if the spec is
        // somehow missing.
        var driver = RunGenerator(
            assemblyName: _TARGET_ASSEMBLY,
            specs: System.Array.Empty<(string, string)>());

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_MalformedSpec_EmitsMalformedSpecDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: _TARGET_ASSEMBLY,
            specs: new[] { ("d2result-envelope.spec.json", "{not valid") });

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Generator_DuplicateFieldValueInSpec_EmitsD2DRE003()
    {
        const string duplicateSpec = """
        {
          "fields": [
            { "constName": "A", "value": "x", "doc": "A doc" },
            { "constName": "B", "value": "x", "doc": "B doc" }
          ]
        }
        """;
        var driver = RunGenerator(
            assemblyName: _TARGET_ASSEMBLY,
            specs: new[] { ("d2result-envelope.spec.json", duplicateSpec) });

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.DuplicateFieldValue);
    }

    [Fact]
    public void Generator_RunTwiceSameInputs_ProducesIdenticalOutput()
    {
        var first = RunGenerator(
                assemblyName: _TARGET_ASSEMBLY,
                specs: new[] { ("d2result-envelope.spec.json", _VALID_SPEC) })
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();
        var second = RunGenerator(
                assemblyName: _TARGET_ASSEMBLY,
                specs: new[] { ("d2result-envelope.spec.json", _VALID_SPEC) })
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();

        second.Count.Should().Be(first.Count);
        for (var i = 0; i < first.Count; i++)
            Normalize(second[i]).Should().Be(Normalize(first[i]));
    }

    private static GeneratorDriver RunGenerator(
        string assemblyName,
        (string Path, string Content)[] specs)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new D2ResultEnvelopeGenerator().AsSourceGenerator();

        var additionalTexts = specs.Length == 0
            ? ImmutableArray<AdditionalText>.Empty
            : ImmutableArray.CreateRange<AdditionalText>(
                specs.Select(s => new InMemoryAdditionalText(s.Path, s.Content)));

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
