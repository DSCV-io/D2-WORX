// -----------------------------------------------------------------------
// <copyright file="KeyCustodianErrorCodesOutputParityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.SpecsConsistency;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.ErrorCodes.SourceGen;
using D2.Edge.Tests.Unit.KeyCustodian.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// CI-enforced byte-parity gate: regenerates the committed
/// <c>KeyCustodianErrorCodes.g.cs</c>, <c>KeyCustodianFailures.g.cs</c>, and
/// <c>KeyCustodianFailures.Generic.g.cs</c> from the real spec (via the same
/// generator + emitter path used by the Roslyn build) and asserts the result
/// equals the committed file byte-for-byte after normalizing line endings to
/// LF. A failure means the committed file is stale and must be regenerated via
/// <c>dotnet build</c>.
/// </summary>
/// <remarks>
/// This test is the CI equivalent of the <c>git diff --stat</c> byte-parity
/// gate run manually during EXECUTE. Running it in <c>dotnet test</c> turns
/// "guarded by discipline" into "guarded by a test".
/// </remarks>
public sealed class KeyCustodianErrorCodesOutputParityTests
{
    private const string _ASSEMBLY = "D2.Edge.KeyCustodian.Domain";
    private const string _SPEC_NAME = "keycustodian-error-codes.spec.json";
    private const string _CATEGORY_SPEC_NAME = "error-category.spec.json";

    [Theory]
    [InlineData("KeyCustodianErrorCodes.g.cs")]
    [InlineData("KeyCustodianFailures.g.cs")]
    [InlineData("KeyCustodianFailures.Generic.g.cs")]
    public void KeyCustodianGeneratedFile_RegeneratedOutput_MatchesCommittedFile(string fileName)
    {
        var regenerated = Regenerate()[fileName];
        var committed = File.ReadAllText(
            Path.Combine(TestPaths.KeyCustodianGeneratedDir(), fileName));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                $"the committed {fileName} must be byte-identical to a fresh "
                + "generation from the spec; run dotnet build to regenerate");
    }

    private static Dictionary<string, string> Regenerate()
    {
        var specJson = File.ReadAllText(TestPaths.KeyCustodianErrorCodesSpec());
        var enUsJson = File.ReadAllText(TestPaths.EnUsMessages());
        var categoryJson = File.ReadAllText(TestPaths.ErrorCategorySpec());

        // en-US.json drives the engine's D2ERC002 TK-existence cross-check;
        // error-category.spec.json drives the spec-derived category-membership
        // check — mirror the real build's AdditionalFiles so an unresolved
        // userMessageKey OR an unknown category would fail the run (the
        // byte-parity regeneration thus EXERCISES the FIX-B category path).
        return RunGenerator(
            _ASSEMBLY,
            new ErrorCodesGenerator().AsSourceGenerator(),
            additionalTexts:
            [
                new FileBackedAdditionalText(_SPEC_NAME, specJson),
                new FileBackedAdditionalText("messages/en-US.json", enUsJson),
                new FileBackedAdditionalText(_CATEGORY_SPEC_NAME, categoryJson),
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

        // Any D2ERC/D2KEC diagnostic surfaced here means the spec itself is
        // invalid — fail loudly rather than silently compare wrong output.
        result.GetRunResult().Diagnostics.Should().BeEmpty(
            because: "a clean spec must produce no build-time diagnostics");

        return result.GetRunResult().GeneratedTrees
            .ToDictionary(
                t => Path.GetFileName(t.FilePath),
                t => t.GetText().ToString());
    }

    /// <summary>
    /// Normalizes line endings to LF so the comparison is line-ending-agnostic
    /// (the harness emits LF; a Windows git checkout may introduce CRLF).
    /// </summary>
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
