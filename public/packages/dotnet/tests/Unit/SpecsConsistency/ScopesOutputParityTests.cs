// -----------------------------------------------------------------------
// <copyright file="ScopesOutputParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using D2.Shared.Auth.Scopes.SourceGen;
using D2.Shared.Tests.Unit.Auth;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// CI-enforced byte-parity gate: regenerates the committed <c>Scopes.g.cs</c> from
/// the real <c>contracts/auth-scopes/scopes.spec.json</c> AND the real
/// <c>OrgType</c> / <c>Role</c> enum sources (via the same <see cref="ScopesGenerator"/>
/// path used by the Roslyn build — wildcard <c>grantedTo</c> expansion reads the enum
/// members straight from the compilation) and asserts the result equals the committed
/// file byte-for-byte after normalizing line endings to LF. A failure means the
/// committed file is stale and must be regenerated (<c>dotnet build</c>).
/// </summary>
public sealed class ScopesOutputParityTests
{
    private const string _TARGET_ASSEMBLY = "D2.Shared.Auth.Abstractions";
    private const string _SPEC_FILE_NAME = "scopes.spec.json";

    private static readonly string sr_generatedBase =
        Path.Combine(
            TestPaths.RepoRoot(),
            "public",
            "packages",
            "dotnet",
            "auth",
            "abstractions",
            "Generated",
            "D2.Shared.Auth.Scopes.SourceGen",
            "D2.Shared.Auth.Scopes.SourceGen.ScopesGenerator");

    private static readonly string sr_authAbstractionsBase =
        Path.Combine(
            TestPaths.RepoRoot(), "public", "packages", "dotnet", "auth", "abstractions");

    [Fact]
    public void Scopes_RegeneratedOutput_MatchesCommittedFile()
    {
        var specJson = File.ReadAllText(TestPaths.AuthScopesSpec());

        var regenerated = RunGenerator(specJson)["Scopes.g.cs"];
        var committed = File.ReadAllText(Path.Combine(sr_generatedBase, "Scopes.g.cs"));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "the committed Scopes.g.cs must be byte-identical to a fresh generation "
                + "from contracts/auth-scopes/scopes.spec.json; run dotnet build to "
                + "regenerate");
    }

    /// <summary>
    /// Deliberate-drift fail-path proof: mutates a scope description in the real
    /// <c>scopes.spec.json</c> and asserts the regenerated output DIFFERS from the
    /// committed file. This proves the parity check would catch a real drift, not
    /// just pass vacuously.
    /// </summary>
    [Fact]
    public void Scopes_DriftedSpec_DoesNotMatchCommittedFile()
    {
        var specJson = File.ReadAllText(TestPaths.AuthScopesSpec());

        // Spec JSON may store apostrophe as \u0027; match the committed fragment.
        const string original =
            "Read the caller\\u0027s own user data (profile, preferences, contact details).";
        specJson.Should().Contain(
            original,
            because:
                "the drift test mutates this exact scope description — if the real "
                + "spec's wording changed, update this literal too");

        var drifted = specJson.Replace(original, original + " DRIFT-TEST-MARKER.");

        var regenerated = RunGenerator(drifted)["Scopes.g.cs"];
        var committed = File.ReadAllText(Path.Combine(sr_generatedBase, "Scopes.g.cs"));

        Normalize(regenerated).Should().NotBe(
            Normalize(committed),
            because:
                "a deliberately drifted spec must produce output that differs from the "
                + "committed file — proves the parity check is not vacuous");
    }

    private static Dictionary<string, string> RunGenerator(string specJson)
    {
        // The wildcard grantedTo expansion reads OrgType + Role members straight from
        // the compilation (ExtractEnumMembers) — feed the REAL enum sources rather than
        // a synthetic stand-in so a real-world enum-member addition surfaces as drift
        // here too, not just in ScopesGeneratorTests' synthetic-enum unit coverage.
        var orgTypeSource = File.ReadAllText(
            Path.Combine(sr_authAbstractionsBase, "OrgType.cs"));
        var roleSource = File.ReadAllText(
            Path.Combine(sr_authAbstractionsBase, "Role.cs"));

        var compilation = CSharpCompilation.Create(
            assemblyName: _TARGET_ASSEMBLY,
            syntaxTrees:
            [
                CSharpSyntaxTree.ParseText(orgTypeSource),
                CSharpSyntaxTree.ParseText(roleSource),
            ],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        AdditionalText[] additionalTexts =
        [
            new FileBackedAdditionalText(_SPEC_FILE_NAME, specJson),
        ];

        var driver = CSharpGeneratorDriver.Create(
            generators: [new ScopesGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts.ToImmutableArray());

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
