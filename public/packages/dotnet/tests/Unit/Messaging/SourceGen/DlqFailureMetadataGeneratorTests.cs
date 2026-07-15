// -----------------------------------------------------------------------
// <copyright file="DlqFailureMetadataGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Messaging.DlqMetadata.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests for the DlqFailureMetadata SrcGen
/// — drives <see cref="DlqFailureMetadataGenerator"/> via a synthetic
/// <see cref="CSharpGeneratorDriver"/> rather than the build pipeline.
/// Multi-target dispatch: assembly name determines whether the Fields half
/// or the Causes half emits.
/// </summary>
public sealed class DlqFailureMetadataGeneratorTests
{
    private const string _SAMPLE_SPEC = """
    {
      "fields": [
        {
          "constName": "CAUSE",
          "value": "cause",
          "doc": "doc"
        }
      ],
      "causes": [
        {
          "constName": "HANDLER_EXCEPTION",
          "value": "HANDLER_EXCEPTION",
          "doc": "doc"
        }
      ]
    }
    """;

    [Fact]
    public void Generator_FieldsAssembly_EmitsFieldsGeneratedSource()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Messaging.Abstractions",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        var fileName = Path.GetFileName(result.GeneratedTrees[0].FilePath);
        fileName.Should().Be("DlqFailureMetadataFields.g.cs");
    }

    [Fact]
    public void Generator_CausesAssembly_EmitsCausesGeneratedSource()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Messaging.RabbitMq",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        var fileName = Path.GetFileName(result.GeneratedTrees[0].FilePath);
        fileName.Should().Be("DlqFailureCauses.g.cs");
    }

    [Fact]
    public void Generator_NonTargetAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpec_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Messaging.Abstractions",
            specJson: null);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_MalformedSpec_EmitsMalformedSpecDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Messaging.Abstractions",
            specJson: "{not valid");

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Generator_RunTwice_SameInputs_ProducesIdenticalOutput()
    {
        var first = RunGenerator(
                assemblyName: "DcsvIo.D2.Messaging.Abstractions",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();
        var second = RunGenerator(
                assemblyName: "DcsvIo.D2.Messaging.Abstractions",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();

        second.Count.Should().Be(first.Count);
        for (var i = 0; i < first.Count; i++)
            Normalize(second[i]).Should().Be(Normalize(first[i]));
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

        var generator = new DlqFailureMetadataGenerator().AsSourceGenerator();

        var additionalTexts = specJson is null
            ? ImmutableArray<AdditionalText>.Empty
            : ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
                "dlq-failure-metadata.spec.json",
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
