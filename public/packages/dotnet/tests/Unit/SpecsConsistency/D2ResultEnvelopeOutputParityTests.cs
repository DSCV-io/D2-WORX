// -----------------------------------------------------------------------
// <copyright file="D2ResultEnvelopeOutputParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using D2.Shared.Result.Envelope.SourceGen;
using D2.Shared.Tests.Unit.Auth;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// CI-enforced byte-parity gate: regenerates the committed
/// <c>D2ResultEnvelopeFieldNames.g.cs</c> from the real
/// <c>contracts/d2result-envelope/d2result-envelope.spec.json</c> (via the
/// same <see cref="D2ResultEnvelopeGenerator"/> path used by the Roslyn
/// build) and asserts the result equals the committed file byte-for-byte
/// after normalizing line endings to LF. A failure means the committed file
/// is stale and must be regenerated via <c>dotnet build</c>.
/// </summary>
public sealed class D2ResultEnvelopeOutputParityTests
{
    private const string _TARGET_ASSEMBLY = "D2.Shared.Result";

    private static readonly string sr_generatedBase =
        Path.Combine(
            TestPaths.RepoRoot(),
            "public",
            "packages",
            "dotnet",
            "result",
            "core",
            "Generated",
            "D2.Shared.Result.Envelope.SourceGen",
            "D2.Shared.Result.Envelope.SourceGen.D2ResultEnvelopeGenerator");

    [Fact]
    public void D2ResultEnvelopeFieldNames_RegeneratedOutput_MatchesCommittedFile()
    {
        var regenerated = RegenerateEnvelope()["D2ResultEnvelopeFieldNames.g.cs"];
        var committed = File.ReadAllText(
            Path.Combine(sr_generatedBase, "D2ResultEnvelopeFieldNames.g.cs"));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "the committed D2ResultEnvelopeFieldNames.g.cs must be byte-identical "
                + "to a fresh generation from d2result-envelope.spec.json; run "
                + "dotnet build to regenerate");
    }

    private static Dictionary<string, string> RegenerateEnvelope()
    {
        var specPath = Path.Combine(
            TestPaths.RepoRoot(),
            "public",
            "contracts",
            "d2result-envelope",
            "d2result-envelope.spec.json");

        return RunGenerator(
            _TARGET_ASSEMBLY,
            new D2ResultEnvelopeGenerator().AsSourceGenerator(),
            additionalTexts:
            [
                new FileBackedAdditionalText(
                    Path.GetFileName(specPath),
                    File.ReadAllText(specPath)),
            ]);
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
            because: "a clean d2result-envelope spec must produce no diagnostics");

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
