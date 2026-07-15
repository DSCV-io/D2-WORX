// -----------------------------------------------------------------------
// <copyright file="ProductEncryptionDomainsOutputParityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Private.Packages.Tests.Unit.Encryption;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using AwesomeAssertions;
using D2.Shared.EncryptionDomains.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// CI-enforced byte-parity gate for committed
/// <c>ProductEncryptionDomains.g.cs</c> under Encryption.Extensions:
/// regenerates from public∪private dual-values specs via the same generator
/// path and asserts LF-normalized equality with the committed file.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProductEncryptionDomainsOutputParityTests
{
    private const string _TARGET_ASSEMBLY = "D2.Shared.Encryption.Extensions";
    private const string _FILE_NAME = "ProductEncryptionDomains.g.cs";

    [Fact]
    public void ProductEncryptionDomains_RegeneratedOutput_MatchesCommittedFile()
    {
        var regenerated = RunGenerator(
            File.ReadAllText(PublicSpecPath()),
            File.ReadAllText(PrivateSpecPath()))[_FILE_NAME];
        var committed = File.ReadAllText(CommittedGeneratedPath());

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "committed ProductEncryptionDomains.g.cs must match a fresh generation from "
                + "public∪private encryption-domains specs; run dotnet build on "
                + "D2.Shared.Encryption.Extensions to regenerate");
    }

    [Fact]
    public void ProductEncryptionDomains_DriftedPrivateSpec_DoesNotMatchCommittedFile()
    {
        var privateSpec = File.ReadAllText(PrivateSpecPath());

        // Private dual-values spec uses PowerShell-ish spacing: `"constName":  "AUDIT"`.
        const string originalConst = "\"constName\":  \"AUDIT\"";
        privateSpec.Should().Contain(
            originalConst,
            because: "drift pin mutates the real AUDIT constName token");

        var drifted = privateSpec.Replace(
            originalConst,
            "\"constName\":  \"AUDIT_DRIFT_MARKER\"");

        var regenerated = RunGenerator(
            File.ReadAllText(PublicSpecPath()),
            drifted)[_FILE_NAME];
        var committed = File.ReadAllText(CommittedGeneratedPath());

        Normalize(regenerated).Should().NotBe(
            Normalize(committed),
            because: "a drifted private spec must change generated output (parity not vacuous)");
    }

    private static string RepoRootFixturePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "D2.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate monorepo root from " + AppContext.BaseDirectory);
    }

    private static string CommittedGeneratedPath()
    {
        var root = RepoRootFixturePath();

        return Path.Combine(
            root,
            "private",
            "packages",
            "dotnet",
            "encryption",
            "extensions",
            "Generated",
            "D2.Shared.EncryptionDomains.SourceGen",
            "D2.Shared.EncryptionDomains.SourceGen.EncryptionDomainsGenerator",
            _FILE_NAME);
    }

    private static string PublicSpecPath()
    {
        return Path.Combine(
            RepoRootFixturePath(),
            "public",
            "contracts",
            "encryption-domains",
            "encryption-domains.spec.json");
    }

    private static string PrivateSpecPath()
    {
        return Path.Combine(
            RepoRootFixturePath(),
            "private",
            "contracts",
            "encryption-domains",
            "encryption-domains.spec.json");
    }

    private static Dictionary<string, string> RunGenerator(string publicSpec, string privateSpec)
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
                publicSpec),
            new InMemoryAdditionalText(
                "private/contracts/encryption-domains/encryption-domains.spec.json",
                privateSpec));

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
