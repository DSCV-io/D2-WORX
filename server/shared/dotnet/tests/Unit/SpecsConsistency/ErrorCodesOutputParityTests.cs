// -----------------------------------------------------------------------
// <copyright file="ErrorCodesOutputParityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

extern alias ResultErrorCodesSourceGen;

namespace D2.Shared.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using D2.Shared.Auth.ErrorCodes.SourceGen;
using D2.Shared.Tests.Unit.Auth;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using ResultErrorCodesGenerator =
    ResultErrorCodesSourceGen::D2.Shared.ResultErrorCodes.SourceGen.ErrorCodesGenerator;

/// <summary>
/// CI-enforced byte-parity gate: regenerates the committed
/// <c>AuthErrorCodes.g.cs</c>, <c>AuthFailures.g.cs</c>, and
/// <c>ErrorCodes.g.cs</c> from their respective specs (via the same
/// generator + emitter path used by the Roslyn build) and asserts the
/// result equals the committed file byte-for-byte after normalizing
/// line endings to LF. A failure means the committed file is stale and
/// must be regenerated.
/// </summary>
/// <remarks>
/// This test is the CI equivalent of the <c>git diff --stat</c> byte-parity
/// gate run manually during EXECUTE. Running it in <c>dotnet test</c> turns
/// "guarded by discipline" into "guarded by a test".
/// </remarks>
public sealed class ErrorCodesOutputParityTests
{
    private const string _AUTH_ASSEMBLY = "D2.Shared.Auth";
    private const string _AUTH_SPEC_NAME = "auth-error-codes.spec.json";
    private const string _RESULT_ASSEMBLY = "D2.Shared.Result";
    private const string _RESULT_SPEC_NAME = "error-codes.spec.json";

    private static readonly string sr_generatedAuthBase =
        Path.Combine(
            TestPaths.RepoRoot(),
            "server",
            "shared",
            "dotnet",
            "auth",
            "core",
            "Generated",
            "D2.Shared.Auth.ErrorCodes.SourceGen",
            "D2.Shared.Auth.ErrorCodes.SourceGen.ErrorCodesGenerator");

    private static readonly string sr_generatedResultBase =
        Path.Combine(
            TestPaths.RepoRoot(),
            "server",
            "shared",
            "dotnet",
            "result",
            "core",
            "Generated",
            "D2.Shared.Result.ErrorCodes.SourceGen",
            "D2.Shared.ResultErrorCodes.SourceGen.ErrorCodesGenerator");

    [Fact]
    public void AuthErrorCodes_RegeneratedOutput_MatchesCommittedFile()
    {
        var regenerated = RegenerateAuth()["AuthErrorCodes.g.cs"];
        var committed = ReadCommitted(Path.Combine(sr_generatedAuthBase, "AuthErrorCodes.g.cs"));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "the committed AuthErrorCodes.g.cs must be byte-identical to a "
                + "fresh generation from the spec; run dotnet build to regenerate");
    }

    [Fact]
    public void AuthFailures_RegeneratedOutput_MatchesCommittedFile()
    {
        var regenerated = RegenerateAuth()["AuthFailures.g.cs"];
        var committed = ReadCommitted(Path.Combine(sr_generatedAuthBase, "AuthFailures.g.cs"));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "the committed AuthFailures.g.cs must be byte-identical to a "
                + "fresh generation from the spec; run dotnet build to regenerate");
    }

    [Fact]
    public void ErrorCodes_RegeneratedOutput_MatchesCommittedFile()
    {
        var regenerated = RegenerateResult()["ErrorCodes.g.cs"];
        var committed = ReadCommitted(Path.Combine(sr_generatedResultBase, "ErrorCodes.g.cs"));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "the committed ErrorCodes.g.cs must be byte-identical to a "
                + "fresh generation from the spec; run dotnet build to regenerate");
    }

    /// <summary>
    /// Determinism pin for every generated file the result catalog emits — the
    /// constants, the constructing non-generic + generic factories, and the
    /// per-code booleans. Each regenerated file must be byte-identical to its
    /// committed counterpart.
    /// </summary>
    /// <param name="fileName">The generated <c>.g.cs</c> file name to compare.</param>
    [Theory]
    [InlineData("ErrorCodes.g.cs")]
    [InlineData("D2Result.Factories.g.cs")]
    [InlineData("D2Result.Generic.Factories.g.cs")]
    [InlineData("D2Result.Booleans.g.cs")]
    public void ResultCatalog_EveryGeneratedFile_MatchesCommittedFile(string fileName)
    {
        var regenerated = RegenerateResult()[fileName];
        var committed = ReadCommitted(Path.Combine(sr_generatedResultBase, fileName));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                $"the committed {fileName} must be byte-identical to a fresh "
                + "generation from the spec; run dotnet build to regenerate");
    }

    /// <summary>
    /// Determinism pin for the new generic auth failures sibling file
    /// (<c>AuthFailures&lt;T&gt;</c>). The existing <c>AuthFailures.g.cs</c> stays
    /// byte-identical (covered above); this is its typed twin.
    /// </summary>
    [Fact]
    public void AuthFailuresGeneric_RegeneratedOutput_MatchesCommittedFile()
    {
        var regenerated = RegenerateAuth()["AuthFailures.Generic.g.cs"];
        var committed = ReadCommitted(
            Path.Combine(sr_generatedAuthBase, "AuthFailures.Generic.g.cs"));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "the committed AuthFailures.Generic.g.cs must be byte-identical to a "
                + "fresh generation from the spec; run dotnet build to regenerate");
    }

    private static Dictionary<string, string> RegenerateAuth()
    {
        var specPath = Path.Combine(
            TestPaths.RepoRoot(), "contracts", "auth-error-codes", "auth-error-codes.spec.json");
        var enUsPath = Path.Combine(
            TestPaths.RepoRoot(), "contracts", "messages", "en-US.json");

        var specJson = File.ReadAllText(specPath);
        var enUsJson = File.ReadAllText(enUsPath);

        return RunGenerator(
            _AUTH_ASSEMBLY,
            new ErrorCodesGenerator().AsSourceGenerator(),
            additionalTexts:
            [
                new FileBackedAdditionalText(_AUTH_SPEC_NAME, specJson),
                new FileBackedAdditionalText("messages/en-US.json", enUsJson),
            ]);
    }

    private static Dictionary<string, string> RegenerateResult()
    {
        var specPath = Path.Combine(
            TestPaths.RepoRoot(), "contracts", "error-codes", "error-codes.spec.json");
        var enUsPath = Path.Combine(
            TestPaths.RepoRoot(), "contracts", "messages", "en-US.json");

        var specJson = File.ReadAllText(specPath);
        var enUsJson = File.ReadAllText(enUsPath);

        // en-US.json drives the engine's D2ERC002 TK-existence cross-check now
        // that the generic catalog is factory-bearing — mirror the real build's
        // AdditionalFiles so an unresolved userMessageKey would fail the run.
        return RunGenerator(
            _RESULT_ASSEMBLY,
            new ResultErrorCodesGenerator().AsSourceGenerator(),
            additionalTexts:
            [
                new FileBackedAdditionalText(_RESULT_SPEC_NAME, specJson),
                new FileBackedAdditionalText("messages/en-US.json", enUsJson),
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

        // Any D2ERC/D2AEC/D2EC diagnostic surfaced here means the spec itself
        // is invalid — fail loudly rather than silently compare wrong output.
        result.GetRunResult().Diagnostics.Should().BeEmpty(
            because: "a clean spec must produce no build-time diagnostics");

        return result.GetRunResult().GeneratedTrees
            .ToDictionary(
                t => Path.GetFileName(t.FilePath),
                t => t.GetText().ToString());
    }

    private static string ReadCommitted(string path) => File.ReadAllText(path);

    /// <summary>
    /// Normalizes line endings to LF so the comparison is line-ending-agnostic
    /// (the harness emits LF; Windows git checkout may introduce CRLF).
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
