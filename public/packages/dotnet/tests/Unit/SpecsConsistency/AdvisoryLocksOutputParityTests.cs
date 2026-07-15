// -----------------------------------------------------------------------
// <copyright file="AdvisoryLocksOutputParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SpecsConsistency;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.AdvisoryLocks.SourceGen;
using DcsvIo.D2.Tests.Unit.Auth;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// CI-enforced byte-parity gate for <c>AdvisoryLocks.g.cs</c>: regenerates
/// the committed file from <c>contracts/advisory-locks/advisory-locks.spec.json</c>
/// via the same generator + emitter path used by the Roslyn build and asserts
/// the result equals the committed file (LF-normalised). A failure means the
/// committed file is stale and must be regenerated (<c>dotnet build</c>).
/// </summary>
public sealed class AdvisoryLocksOutputParityTests
{
    private const string _ASSEMBLY = "DcsvIo.D2.Private.Edge.KeyCustodian.Infra";
    private const string _SPEC_FILE_NAME = "advisory-locks.spec.json";

    private static readonly string sr_generatedBase =
        Path.Combine(
            TestPaths.RepoRoot(),
            "private",
            "services",
            "edge",
            "key-custodian",
            "infra",
            "Generated",
            "DcsvIo.D2.AdvisoryLocks.SourceGen",
            "DcsvIo.D2.AdvisoryLocks.SourceGen.AdvisoryLocksGenerator");

    [Fact]
    public void AdvisoryLocks_RegeneratedOutput_MatchesCommittedFile()
    {
        var specPath = Path.Combine(
            TestPaths.RepoRoot(),
            "private",
            "contracts",
            "advisory-locks",
            "advisory-locks.spec.json");

        var specJson = File.ReadAllText(specPath);

        var regenerated = RunGenerator(specJson)["AdvisoryLocks.g.cs"];
        var committed = File.ReadAllText(
            Path.Combine(sr_generatedBase, "AdvisoryLocks.g.cs"));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "the committed AdvisoryLocks.g.cs must be byte-identical to a "
                + "fresh generation from the spec; run dotnet build to regenerate");
    }

    /// <summary>
    /// Deliberate-drift fail-path proof: mutates the spec (adds a dummy entry)
    /// and asserts the regenerated output DIFFERS from the committed file.
    /// This proves the parity check would catch a real drift, not just pass
    /// vacuously.
    /// </summary>
    [Fact]
    public void AdvisoryLocks_DriftedSpec_DoesNotMatchCommittedFile()
    {
        // Inject an extra entry so the regenerated output differs.
        const string driftedSpec = """
            {
              "locks": [
                {
                  "constName": "MIGRATOR",
                  "database": "d2-keycustodian",
                  "key": 1001001001,
                  "doc": "Migration lock."
                },
                {
                  "constName": "DRIFTED_ENTRY",
                  "database": "d2-keycustodian",
                  "key": 3003003003,
                  "doc": "This entry is NOT in the real spec."
                }
              ]
            }
            """;

        var regenerated = RunGenerator(driftedSpec)["AdvisoryLocks.g.cs"];
        var committed = File.ReadAllText(
            Path.Combine(sr_generatedBase, "AdvisoryLocks.g.cs"));

        Normalize(regenerated).Should().NotBe(
            Normalize(committed),
            because:
                "a deliberately drifted spec must produce output that differs "
                + "from the committed file — proves the parity check is not vacuous");
    }

    private static System.Collections.Generic.Dictionary<string, string> RunGenerator(
        string specJson)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: _ASSEMBLY,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(
            generators: [new AdvisoryLocksGenerator().AsSourceGenerator()],
            additionalTexts: ImmutableArray.Create<AdditionalText>(
                new FileBackedAdditionalText(_SPEC_FILE_NAME, specJson)));

        var result = driver.RunGenerators(compilation);

        result.GetRunResult().Diagnostics.Should().BeEmpty(
            because: "a clean spec must produce no build-time diagnostics");

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
            System.Threading.CancellationToken cancellationToken = default) => r_text;
    }
}
