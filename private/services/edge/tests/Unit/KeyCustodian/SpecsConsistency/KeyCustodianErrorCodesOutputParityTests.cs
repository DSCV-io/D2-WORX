// -----------------------------------------------------------------------
// <copyright file="KeyCustodianErrorCodesOutputParityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.SpecsConsistency;

using System.Collections.Immutable;
using D2.Edge.KeyCustodian.ErrorCodes.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

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

    /// <summary>
    /// Deliberate-drift fail-path proof: mutates the spec (adds a dummy entry)
    /// and asserts the regenerated output DIFFERS from the committed file.
    /// This proves the parity check would catch a real drift, not just pass
    /// vacuously.
    /// </summary>
    [Fact]
    public void KeyCustodianErrorCodes_DriftedSpec_DoesNotMatchCommittedFile()
    {
        // Inject an extra entry so the regenerated output differs.
        const string driftedSpec = """
            {
              "$schema": "./schema.json",
              "errorCodes": [
                {
                  "code": "KEYCUSTODIAN_KID_INVALID",
                  "httpStatus": 400,
                  "category": "validation_failure",
                  "userMessageKey": "TK.Common.Validation.ID_INVALID",
                  "factoryName": "KidInvalid",
                  "factoryShape": "standard",
                  "doc": "The key identifier is null, empty, whitespace, or contains characters outside the JWKS-safe charset [A-Za-z0-9_-]."
                },
                {
                  "code": "KEYCUSTODIAN_DRIFTED_ENTRY",
                  "httpStatus": 400,
                  "category": "validation_failure",
                  "userMessageKey": "TK.Common.Validation.ID_INVALID",
                  "factoryName": "DriftedEntry",
                  "factoryShape": "standard",
                  "doc": "This entry is NOT in the real spec."
                }
              ]
            }
            """;

        var publicEnUsJson = File.ReadAllText(TestPaths.PublicEnUsMessages());
        var privateEnUsJson = File.ReadAllText(TestPaths.EnUsMessages());
        var categoryJson = File.ReadAllText(TestPaths.ErrorCategorySpec());

        var regenerated = RunGenerator(
            _ASSEMBLY,
            new ErrorCodesGenerator().AsSourceGenerator(),
            additionalTexts:
            [
                new FileBackedAdditionalText(_SPEC_NAME, driftedSpec),
                new FileBackedAdditionalText(
                    "public/contracts/messages/en-US.json",
                    publicEnUsJson),
                new FileBackedAdditionalText(
                    "private/contracts/messages/en-US.json",
                    privateEnUsJson),
                new FileBackedAdditionalText(_CATEGORY_SPEC_NAME, categoryJson),
            ])["KeyCustodianErrorCodes.g.cs"];

        var committed = File.ReadAllText(
            Path.Combine(TestPaths.KeyCustodianGeneratedDir(), "KeyCustodianErrorCodes.g.cs"));

        Normalize(regenerated).Should().NotBe(
            Normalize(committed),
            because:
                "a deliberately drifted spec must produce output that differs "
                + "from the committed file — proves the parity check is not vacuous");
    }

    private static Dictionary<string, string> Regenerate()
    {
        var specJson = File.ReadAllText(TestPaths.KeyCustodianErrorCodesSpec());
        var publicEnUsJson = File.ReadAllText(TestPaths.PublicEnUsMessages());
        var privateEnUsJson = File.ReadAllText(TestPaths.EnUsMessages());
        var categoryJson = File.ReadAllText(TestPaths.ErrorCategorySpec());

        // Dual message roots (public∪private) drive the engine's D2ERC002
        // TK-existence cross-check — mirror the real Domain csproj AdditionalFiles
        // (D2PublicContractsRoot + D2PrivateContractsRoot messages/en-US.json).
        // error-category.spec.json drives category-membership check.
        return RunGenerator(
            _ASSEMBLY,
            new ErrorCodesGenerator().AsSourceGenerator(),
            additionalTexts:
            [
                new FileBackedAdditionalText(_SPEC_NAME, specJson),
                new FileBackedAdditionalText(
                    "public/contracts/messages/en-US.json",
                    publicEnUsJson),
                new FileBackedAdditionalText(
                    "private/contracts/messages/en-US.json",
                    privateEnUsJson),
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
