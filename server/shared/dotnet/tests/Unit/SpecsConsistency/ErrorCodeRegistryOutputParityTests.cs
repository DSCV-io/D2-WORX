// -----------------------------------------------------------------------
// <copyright file="ErrorCodeRegistryOutputParityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

extern alias RegistrySourceGen;

namespace D2.Shared.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using D2.Shared.Tests.Unit.Auth;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using RegistrySourceGen::D2.Shared.ErrorCodes.Registry.SourceGen;
using Xunit;

/// <summary>
/// CI-enforced byte-parity gate: regenerates the committed
/// <c>ErrorCodeRegistry.g.cs</c> from the real spec files (via the same
/// <see cref="RegistryGenerator"/> path used by the Roslyn build) and
/// asserts the result equals the committed file byte-for-byte after
/// normalizing line endings to LF. A failure means the committed file
/// is stale and must be regenerated via <c>dotnet build</c>.
/// </summary>
public sealed class ErrorCodeRegistryOutputParityTests
{
    private const string _TARGET_ASSEMBLY = "D2.Shared.ErrorCodes.Registry";

    private static readonly string sr_generatedBase =
        Path.Combine(
            TestPaths.RepoRoot(),
            "server",
            "shared",
            "dotnet",
            "error-codes",
            "registry",
            "Generated",
            "D2.Shared.ErrorCodes.Registry.SourceGen",
            "D2.Shared.ErrorCodes.Registry.SourceGen.RegistryGenerator");

    [Fact]
    public void ErrorCodeRegistry_RegeneratedOutput_MatchesCommittedFile()
    {
        var regenerated = RegenerateRegistry()["ErrorCodeRegistry.g.cs"];
        var committed = ReadCommitted(Path.Combine(sr_generatedBase, "ErrorCodeRegistry.g.cs"));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "the committed ErrorCodeRegistry.g.cs must be byte-identical to a "
                + "fresh generation from the specs; run dotnet build to regenerate");
    }

    private static Dictionary<string, string> RegenerateRegistry()
    {
        var contractsRoot = Path.Combine(TestPaths.RepoRoot(), "contracts");

        // Mirror the production csproj AdditionalFiles glob:
        //   $(D2ContractsRoot)**\error-codes.spec.json
        //   $(D2ContractsRoot)**\*-error-codes.spec.json
        // so a future per-domain spec flows into the parity test automatically.
        var additionalTexts = new List<AdditionalText>();

        var genericSpec = Path.Combine(contractsRoot, "error-codes", "error-codes.spec.json");
        if (File.Exists(genericSpec))
        {
            additionalTexts.Add(
                new FileBackedAdditionalText(
                    Path.GetFileName(genericSpec),
                    File.ReadAllText(genericSpec)));
        }

        foreach (var path in Directory.GetFiles(
            contractsRoot, "*-error-codes.spec.json", SearchOption.AllDirectories))
        {
            additionalTexts.Add(
                new FileBackedAdditionalText(
                    Path.GetFileName(path),
                    File.ReadAllText(path)));
        }

        return RunGenerator(
            _TARGET_ASSEMBLY,
            new RegistryGenerator().AsSourceGenerator(),
            additionalTexts: [.. additionalTexts]);
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
            because: "clean specs must produce no diagnostics");

        return result.GetRunResult().GeneratedTrees
            .ToDictionary(
                t => Path.GetFileName(t.FilePath),
                t => t.GetText().ToString());
    }

    private static string ReadCommitted(string path) => File.ReadAllText(path);

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
