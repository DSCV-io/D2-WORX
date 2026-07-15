// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsOutputParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Encryption;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using D2.Shared.EncryptionDomains.SourceGen;
using D2.Shared.Tests.Unit.Auth;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// CI-enforced byte-parity gate for committed public
/// <c>EncryptionDomains.g.cs</c> under <c>D2.Shared.Encryption</c>:
/// regenerates from the public-only encryption-domains spec via the same
/// generator path and asserts LF-normalized equality with the committed file.
/// </summary>
public sealed class EncryptionDomainsOutputParityTests
{
    private const string _TARGET_ASSEMBLY = "D2.Shared.Encryption";
    private const string _FILE_NAME = "EncryptionDomains.g.cs";

    [Fact]
    public void EncryptionDomains_RegeneratedOutput_MatchesCommittedFile()
    {
        var regenerated = RunGenerator(File.ReadAllText(PublicSpecPath()))[_FILE_NAME];
        var committed = File.ReadAllText(CommittedGeneratedPath());

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "committed EncryptionDomains.g.cs must match a fresh generation from "
                + "public/contracts/encryption-domains; run dotnet build on "
                + "D2.Shared.Encryption to regenerate");
    }

    [Fact]
    public void EncryptionDomains_DriftedPublicSpec_DoesNotMatchCommittedFile()
    {
        var publicSpec = File.ReadAllText(PublicSpecPath());

        const string originalConst = "\"constName\": \"FIXTURE_SEALED\"";
        publicSpec.Should().Contain(
            originalConst,
            because: "drift pin mutates the real FIXTURE_SEALED constName token");

        var drifted = publicSpec.Replace(
            originalConst,
            "\"constName\": \"FIXTURE_SEALED_DRIFT_MARKER\"");

        var regenerated = RunGenerator(drifted)[_FILE_NAME];
        var committed = File.ReadAllText(CommittedGeneratedPath());

        Normalize(regenerated).Should().NotBe(
            Normalize(committed),
            because: "a drifted public spec must change generated output (parity not vacuous)");
    }

    private static string CommittedGeneratedPath() =>
        Path.Combine(
            TestPaths.RepoRoot(),
            "public",
            "packages",
            "dotnet",
            "encryption",
            "core",
            "Generated",
            "D2.Shared.EncryptionDomains.SourceGen",
            "D2.Shared.EncryptionDomains.SourceGen.EncryptionDomainsGenerator",
            _FILE_NAME);

    private static string PublicSpecPath() =>
        Path.Combine(
            TestPaths.PublicContractsRoot(),
            "encryption-domains",
            "encryption-domains.spec.json");

    private static Dictionary<string, string> RunGenerator(string publicSpec)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: _TARGET_ASSEMBLY,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(
                "public/contracts/encryption-domains/encryption-domains.spec.json",
                publicSpec));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new EncryptionDomainsGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation, out _, out _);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().NotBeEmpty();

        return result.GeneratedTrees.ToDictionary(
            t => Path.GetFileName(t.FilePath),
            t => t.GetText(System.Threading.CancellationToken.None).ToString(),
            StringComparer.Ordinal);
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd();

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(
            System.Threading.CancellationToken cancellationToken = default)
            => SourceText.From(text, Encoding.UTF8);
    }
}
