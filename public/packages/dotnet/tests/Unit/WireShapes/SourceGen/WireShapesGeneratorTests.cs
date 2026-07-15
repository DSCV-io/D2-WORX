// -----------------------------------------------------------------------
// <copyright file="WireShapesGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.WireShapes.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.WireShapes.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests for the wire-shapes SrcGen —
/// drive <see cref="WireShapesGenerator"/> via a synthetic
/// <see cref="CSharpGeneratorDriver"/> rather than the build pipeline.
/// Asserts the multi-target dispatch behavior: DcsvIo.D2.I18n.Abstractions
/// gets TkMessageWireShape from tk-message.spec.json; DcsvIo.D2.Result
/// gets InputErrorWireShape from input-error.spec.json; anything else
/// emits nothing.
/// </summary>
public sealed class WireShapesGeneratorTests
{
    private const string _TK_MESSAGE_ASSEMBLY = "DcsvIo.D2.I18n.Abstractions";
    private const string _INPUT_ERROR_ASSEMBLY = "DcsvIo.D2.Result";

    private const string _TK_MESSAGE_SPEC = """
    {
      "properties": [
        { "constName": "KEY", "value": "key", "doc": "Key property." },
        { "constName": "PARAMS", "value": "params", "doc": "Params property." }
      ]
    }
    """;

    private const string _INPUT_ERROR_SPEC = """
    {
      "properties": [
        { "constName": "FIELD", "value": "field", "doc": "Field property." },
        { "constName": "ERRORS", "value": "errors", "doc": "Errors property." }
      ]
    }
    """;

    [Fact]
    public void Generator_I18nAbstractions_EmitsTkMessageWireShapeFromTkMessageSpec()
    {
        var driver = RunGenerator(
            assemblyName: _TK_MESSAGE_ASSEMBLY,
            specs: new[] { ("tk-message.spec.json", _TK_MESSAGE_SPEC) });

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        Path.GetFileName(result.GeneratedTrees[0].FilePath)
            .Should().Be("TkMessageWireShape.g.cs");
        result.GeneratedTrees[0].ToString().Should().Contain(
            "namespace DcsvIo.D2.I18n;");
        result.GeneratedTrees[0].ToString().Should().Contain(
            "public const string KEY = \"key\";");
    }

    [Fact]
    public void Generator_Result_EmitsInputErrorWireShapeFromInputErrorSpec()
    {
        var driver = RunGenerator(
            assemblyName: _INPUT_ERROR_ASSEMBLY,
            specs: new[] { ("input-error.spec.json", _INPUT_ERROR_SPEC) });

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        Path.GetFileName(result.GeneratedTrees[0].FilePath)
            .Should().Be("InputErrorWireShape.g.cs");
        result.GeneratedTrees[0].ToString().Should().Contain(
            "namespace DcsvIo.D2.Result;");
        result.GeneratedTrees[0].ToString().Should().Contain(
            "public const string FIELD = \"field\";");
    }

    [Fact]
    public void Generator_NonTargetAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specs: new[]
            {
                ("tk-message.spec.json", _TK_MESSAGE_SPEC),
                ("input-error.spec.json", _INPUT_ERROR_SPEC),
            });

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButWrongSpec_EmitsMissingSpecDiagnostic()
    {
        // Generator target is DcsvIo.D2.I18n.Abstractions which expects
        // tk-message.spec.json — but consumer only supplied
        // input-error.spec.json. The filename-match dispatch fails →
        // D2WS005 fires.
        var driver = RunGenerator(
            assemblyName: _TK_MESSAGE_ASSEMBLY,
            specs: new[] { ("input-error.spec.json", _INPUT_ERROR_SPEC) });

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MissingSpec);
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpec_EmitsMissingSpecDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: _TK_MESSAGE_ASSEMBLY,
            specs: System.Array.Empty<(string, string)>());

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MissingSpec);
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_MalformedSpec_EmitsMalformedSpecDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: _TK_MESSAGE_ASSEMBLY,
            specs: new[] { ("tk-message.spec.json", "{not valid") });

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Generator_BothSpecsSupplied_RoutesByAssemblyName()
    {
        // Both spec files supplied — the I18n consumer should route to
        // TkMessageWireShape and the Result consumer should route to
        // InputErrorWireShape; cross-routing is impossible.
        var i18nDriver = RunGenerator(
            assemblyName: _TK_MESSAGE_ASSEMBLY,
            specs: new[]
            {
                ("tk-message.spec.json", _TK_MESSAGE_SPEC),
                ("input-error.spec.json", _INPUT_ERROR_SPEC),
            });
        var resultDriver = RunGenerator(
            assemblyName: _INPUT_ERROR_ASSEMBLY,
            specs: new[]
            {
                ("tk-message.spec.json", _TK_MESSAGE_SPEC),
                ("input-error.spec.json", _INPUT_ERROR_SPEC),
            });

        var i18nFile = Path.GetFileName(
            i18nDriver.GetRunResult().GeneratedTrees[0].FilePath);
        var resultFile = Path.GetFileName(
            resultDriver.GetRunResult().GeneratedTrees[0].FilePath);

        i18nFile.Should().Be("TkMessageWireShape.g.cs");
        resultFile.Should().Be("InputErrorWireShape.g.cs");
    }

    [Fact]
    public void Generator_RunTwiceSameInputs_ProducesIdenticalOutput()
    {
        var first = RunGenerator(
                assemblyName: _TK_MESSAGE_ASSEMBLY,
                specs: new[] { ("tk-message.spec.json", _TK_MESSAGE_SPEC) })
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();
        var second = RunGenerator(
                assemblyName: _TK_MESSAGE_ASSEMBLY,
                specs: new[] { ("tk-message.spec.json", _TK_MESSAGE_SPEC) })
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

        var generator = new WireShapesGenerator().AsSourceGenerator();

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
