// -----------------------------------------------------------------------
// <copyright file="ErrorCodesGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias ResultErrorCodesSourceGen;

namespace DcsvIo.D2.Tests.Unit.Result.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using DiagnosticIds = ResultErrorCodesSourceGen::DcsvIo.D2.ResultErrorCodes.SourceGen.DiagnosticIds;
using ErrorCodesGenerator =
    ResultErrorCodesSourceGen::DcsvIo.D2.ResultErrorCodes.SourceGen.ErrorCodesGenerator;

/// <summary>
/// IIncrementalGenerator integration tests for the generic ErrorCodes SrcGen
/// — drive <see cref="ErrorCodesGenerator"/> via a synthetic
/// <see cref="CSharpGeneratorDriver"/> rather than the build pipeline.
/// </summary>
public sealed class ErrorCodesGeneratorTests
{
    private const string _SAMPLE_SPEC = """
    {
      "errorCodes": [
        {
          "code": "TEST_THING",
          "httpStatus": 404,
          "doc": "Test entry."
        }
      ]
    }
    """;

    [Fact]
    public void Generator_TargetAssemblyWithSpec_EmitsConstantsAndConstructingFactoriesAndBooleans()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Result",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        // The generic catalog (FactoryHost.Base) emits the constants file plus
        // the constructing failure factories onto the D2Result / D2Result<TData>
        // partials AND the per-code booleans.
        result.GeneratedTrees.Should().HaveCount(4);
        var fileNames = result.GeneratedTrees
            .Select(t => Path.GetFileName(t.FilePath))
            .OrderBy(n => n)
            .ToList();
        fileNames.Should().BeEquivalentTo(
            new[]
            {
                "D2Result.Booleans.g.cs",
                "D2Result.Factories.g.cs",
                "D2Result.Generic.Factories.g.cs",
                "ErrorCodes.g.cs",
            });
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
        // No AdditionalText supplied — generator silently no-ops (no spec).
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Result",
            specJson: null);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_MalformedSpec_EmitsMalformedSpecDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Result",
            specJson: "{not valid");

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Generator_RunTwice_SameInputs_ProducesIdenticalOutput()
    {
        var first = RunGenerator(
                assemblyName: "DcsvIo.D2.Result",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();
        var second = RunGenerator(
                assemblyName: "DcsvIo.D2.Result",
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

        var generator = new ErrorCodesGenerator().AsSourceGenerator();

        var additionalTexts = specJson is null
            ? ImmutableArray<AdditionalText>.Empty
            : ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
                "error-codes.spec.json",
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
