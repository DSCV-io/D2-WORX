// -----------------------------------------------------------------------
// <copyright file="WireShapesOutputParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using D2.Shared.Tests.Unit.Auth;
using D2.Shared.WireShapes.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// CI-enforced byte-parity gates: regenerates the committed
/// <c>TkMessageWireShape.g.cs</c> and <c>InputErrorWireShape.g.cs</c> from
/// their respective real specs (via the same <see cref="WireShapesGenerator"/>
/// path used by the Roslyn build) and asserts each result equals the committed
/// file byte-for-byte after normalizing line endings to LF. A failure means
/// the committed file is stale and must be regenerated via
/// <c>dotnet build</c>.
/// </summary>
/// <remarks>
/// <see cref="WireShapesGenerator"/> dispatches by assembly name: the
/// <c>D2.Shared.I18n.Abstractions</c> assembly receives
/// <c>TkMessageWireShape.g.cs</c> from <c>tk-message.spec.json</c>, and the
/// <c>D2.Shared.Result</c> assembly receives <c>InputErrorWireShape.g.cs</c>
/// from <c>input-error.spec.json</c>. Each target requires a separate driver
/// invocation.
/// </remarks>
public sealed class WireShapesOutputParityTests
{
    private const string _TK_MESSAGE_ASSEMBLY = "D2.Shared.I18n.Abstractions";
    private const string _INPUT_ERROR_ASSEMBLY = "D2.Shared.Result";

    private static readonly string sr_i18nGeneratedBase =
        Path.Combine(
            TestPaths.RepoRoot(),
            "public",
            "packages",
            "dotnet",
            "i18n",
            "abstractions",
            "Generated",
            "D2.Shared.WireShapes.SourceGen",
            "D2.Shared.WireShapes.SourceGen.WireShapesGenerator");

    private static readonly string sr_resultGeneratedBase =
        Path.Combine(
            TestPaths.RepoRoot(),
            "public",
            "packages",
            "dotnet",
            "result",
            "core",
            "Generated",
            "D2.Shared.WireShapes.SourceGen",
            "D2.Shared.WireShapes.SourceGen.WireShapesGenerator");

    [Fact]
    public void TkMessageWireShape_RegeneratedOutput_MatchesCommittedFile()
    {
        var specPath = Path.Combine(
            TestPaths.RepoRoot(),
            "public",
            "contracts",
            "tk-message",
            "tk-message.spec.json");

        var regenerated = RunGenerator(
            _TK_MESSAGE_ASSEMBLY,
            new WireShapesGenerator().AsSourceGenerator(),
            additionalTexts:
            [
                new FileBackedAdditionalText(
                    Path.GetFileName(specPath),
                    File.ReadAllText(specPath)),
            ])["TkMessageWireShape.g.cs"];

        var committed = File.ReadAllText(
            Path.Combine(sr_i18nGeneratedBase, "TkMessageWireShape.g.cs"));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "the committed TkMessageWireShape.g.cs must be byte-identical to a "
                + "fresh generation from tk-message.spec.json; run dotnet build "
                + "to regenerate");
    }

    [Fact]
    public void InputErrorWireShape_RegeneratedOutput_MatchesCommittedFile()
    {
        var specPath = Path.Combine(
            TestPaths.RepoRoot(),
            "public",
            "contracts",
            "input-error",
            "input-error.spec.json");

        var regenerated = RunGenerator(
            _INPUT_ERROR_ASSEMBLY,
            new WireShapesGenerator().AsSourceGenerator(),
            additionalTexts:
            [
                new FileBackedAdditionalText(
                    Path.GetFileName(specPath),
                    File.ReadAllText(specPath)),
            ])["InputErrorWireShape.g.cs"];

        var committed = File.ReadAllText(
            Path.Combine(sr_resultGeneratedBase, "InputErrorWireShape.g.cs"));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "the committed InputErrorWireShape.g.cs must be byte-identical to a "
                + "fresh generation from input-error.spec.json; run dotnet build "
                + "to regenerate");
    }

    private static Dictionary<string, string> RunGenerator(
        string assemblyName,
        ISourceGenerator generator,
        AdditionalText[] additionalTexts)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts.ToImmutableArray());

        var result = driver.RunGenerators(compilation);

        result.GetRunResult().Diagnostics.Should().BeEmpty(
            because: "a clean wire-shape spec must produce no diagnostics");

        return result.GetRunResult().GeneratedTrees
            .ToDictionary(
                t => Path.GetFileName(t.FilePath),
                t => t.GetText().ToString());
    }

    private static string Normalize(string source) =>
        source.Replace("\r\n", "\n").Replace("\r", "\n");

    private sealed class FileBackedAdditionalText : AdditionalText
    {
        private readonly SourceText r_text;

        public FileBackedAdditionalText(string path, string content)
        {
            Path = path;
            r_text = SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText GetText(
            CancellationToken cancellationToken = default) => r_text;
    }
}
