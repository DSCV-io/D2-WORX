// -----------------------------------------------------------------------
// <copyright file="ContextOutputParityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using D2.Shared.Context.SourceGen;
using D2.Shared.Tests.Unit.Auth;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// CI-enforced byte-parity gate: regenerates the committed context-abstractions
/// output (<c>IRequestContext.g.cs</c>, <c>MutableRequestContext.g.cs</c>,
/// <c>PropagatedContext.g.cs</c>, <c>PropagatedContextExtensions.g.cs</c>,
/// <c>PropagatedContextSerializer.g.cs</c>) from the real
/// <c>contracts/{auth,request}-context/*.spec.json</c> files (via the same
/// <see cref="ContextGenerator"/> path used by the Roslyn build) and asserts each
/// result equals its committed file byte-for-byte after normalizing line endings to
/// LF. A failure means the committed file is stale and must be regenerated
/// (<c>dotnet build</c>).
/// </summary>
public sealed class ContextOutputParityTests
{
    private const string _TARGET_ASSEMBLY = "D2.Shared.Context.Abstractions";

    private static readonly string sr_generatedBase =
        Path.Combine(
            TestPaths.RepoRoot(),
            "server",
            "shared",
            "dotnet",
            "context",
            "abstractions",
            "Generated",
            "D2.Shared.Context.SourceGen",
            "D2.Shared.Context.SourceGen.ContextGenerator");

    /// <summary>
    /// Determinism pin for every file the combined context emitter produces for the
    /// <c>D2.Shared.Context.Abstractions</c> target. Each regenerated file must be
    /// byte-identical to its committed counterpart.
    /// </summary>
    /// <param name="fileName">The generated <c>.g.cs</c> file name to compare.</param>
    [Theory]
    [InlineData("IRequestContext.g.cs")]
    [InlineData("MutableRequestContext.g.cs")]
    [InlineData("PropagatedContext.g.cs")]
    [InlineData("PropagatedContextExtensions.g.cs")]
    [InlineData("PropagatedContextSerializer.g.cs")]
    public void ContextAbstractions_EveryGeneratedFile_MatchesCommittedFile(string fileName)
    {
        var regenerated = RegenerateContext()[fileName];
        var committed = File.ReadAllText(Path.Combine(sr_generatedBase, fileName));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                $"the committed {fileName} must be byte-identical to a fresh generation "
                + "from the real auth-context + request-context specs; run dotnet build "
                + "to regenerate");
    }

    /// <summary>
    /// Deliberate-drift fail-path proof: mutates a doc string in the real
    /// <c>IRequestContext.spec.json</c> and asserts the regenerated
    /// <c>IRequestContext.g.cs</c> DIFFERS from the committed file. This proves the
    /// parity check would catch a real drift, not just pass vacuously.
    /// </summary>
    [Fact]
    public void IRequestContext_DriftedSpec_DoesNotMatchCommittedFile()
    {
        var authSpecJson = File.ReadAllText(TestPaths.AuthContextSpec());
        var requestSpecJson = File.ReadAllText(TestPaths.RequestContextSpec());

        const string original = "Trace identifier for this request.";
        requestSpecJson.Should().Contain(
            original,
            because:
                "the drift test mutates this exact doc string — if the real spec's "
                + "wording changed, update this literal too");

        var drifted = requestSpecJson.Replace(original, original + " DRIFT-TEST-MARKER.");

        var regenerated = RunGenerator(authSpecJson, drifted)["IRequestContext.g.cs"];
        var committed = File.ReadAllText(
            Path.Combine(sr_generatedBase, "IRequestContext.g.cs"));

        Normalize(regenerated).Should().NotBe(
            Normalize(committed),
            because:
                "a deliberately drifted spec must produce output that differs from the "
                + "committed file — proves the parity check is not vacuous");
    }

    private static Dictionary<string, string> RegenerateContext()
    {
        var authSpecJson = File.ReadAllText(TestPaths.AuthContextSpec());
        var requestSpecJson = File.ReadAllText(TestPaths.RequestContextSpec());

        return RunGenerator(authSpecJson, requestSpecJson);
    }

    private static Dictionary<string, string> RunGenerator(
        string authSpecJson, string requestSpecJson)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: _TARGET_ASSEMBLY,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        AdditionalText[] additionalTexts =
        [
            new FileBackedAdditionalText("IAuthContext.spec.json", authSpecJson),
            new FileBackedAdditionalText("IRequestContext.spec.json", requestSpecJson),
        ];

        var driver = CSharpGeneratorDriver.Create(
            generators: [new ContextGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts.ToImmutableArray());

        var result = driver.RunGenerators(compilation);

        result.GetRunResult().Diagnostics.Should().BeEmpty(
            because: "clean auth-context + request-context specs must produce no "
                + "build-time diagnostics");

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
