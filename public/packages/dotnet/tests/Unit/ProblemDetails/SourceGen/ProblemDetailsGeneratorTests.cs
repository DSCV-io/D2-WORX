// -----------------------------------------------------------------------
// <copyright file="ProblemDetailsGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.ProblemDetails.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.ProblemDetails.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests for the ProblemDetails SrcGen —
/// drive <see cref="ProblemDetailsGenerator"/> via a synthetic
/// <see cref="CSharpGeneratorDriver"/> rather than the build pipeline.
/// </summary>
public sealed class ProblemDetailsGeneratorTests
{
    private const string _TARGET_ASSEMBLY = "DcsvIo.D2.ProblemDetails.Abstractions";

    private const string _SAMPLE_SPEC = """
    {
      "typeUriPrefix": "https://problems.d2.dcsv.io/",
      "contentType": "application/problem+json",
      "extensionKeys": [
        {
          "constName": "ERROR_CODE",
          "value": "d2_error_code",
          "doc": "Error code."
        }
      ],
      "titles": [
        {
          "constName": "UNAUTHORIZED",
          "httpStatus": 401,
          "value": "Unauthorized",
          "doc": "401."
        },
        {
          "constName": "REQUEST_FAILED",
          "httpStatus": null,
          "value": "Request Failed",
          "doc": "Fallback."
        }
      ]
    }
    """;

    [Fact]
    public void Generator_TargetAssemblyWithSpec_EmitsD2ProblemDetailsKeysGenerated()
    {
        var driver = RunGenerator(
            assemblyName: _TARGET_ASSEMBLY,
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        Path.GetFileName(result.GeneratedTrees[0].FilePath)
            .Should().Be("D2ProblemDetailsKeys.g.cs");
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
    public void Generator_AuthHttpAssembly_EmitsNothing()
    {
        // Routing β regression: prior implementation single-targeted on
        // DcsvIo.D2.Auth.Http; new shape targets the abstractions csproj
        // so this assembly name now produces no output.
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Http",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpec_EmitsNothing()
    {
        // No AdditionalText supplied — generator silently no-ops.
        var driver = RunGenerator(
            assemblyName: _TARGET_ASSEMBLY,
            specJson: null);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_MalformedSpec_EmitsMalformedSpecDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: _TARGET_ASSEMBLY,
            specJson: "{not valid");

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Generator_RunTwiceSameInputs_ProducesIdenticalOutput()
    {
        var first = RunGenerator(
                assemblyName: _TARGET_ASSEMBLY,
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();
        var second = RunGenerator(
                assemblyName: _TARGET_ASSEMBLY,
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

        var generator = new ProblemDetailsGenerator().AsSourceGenerator();

        var additionalTexts = specJson is null
            ? ImmutableArray<AdditionalText>.Empty
            : ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
                "problem-details.spec.json",
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
