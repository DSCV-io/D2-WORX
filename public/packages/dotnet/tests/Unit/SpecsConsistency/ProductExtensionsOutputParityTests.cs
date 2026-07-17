// -----------------------------------------------------------------------
// <copyright file="ProductExtensionsOutputParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Audiences.SourceGen;
using DcsvIo.D2.Auth.Scopes.SourceGen;
using DcsvIo.D2.EncryptionDomains.SourceGen;
using DcsvIo.D2.I18n.SourceGen;
using DcsvIo.D2.Tests.Unit.Auth;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// T3.8 — committed Product*.g.cs under private Extensions hosts byte-match a
/// fresh multi-spec (public∪private) regeneration under the Extensions assembly
/// names. Deliberate-drift case proves the gate is non-vacuous.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProductExtensionsOutputParityTests
{
    private static readonly string sr_authAbstractionsBase =
        Path.Combine(
            TestPaths.RepoRoot(), "public", "packages", "dotnet", "auth", "abstractions");

    private static readonly string sr_productScopesPath =
        Path.Combine(
            TestPaths.RepoRoot(),
            "private",
            "packages",
            "dotnet",
            "auth",
            "abstractions-extensions",
            "Generated",
            "DcsvIo.D2.Auth.Scopes.SourceGen",
            "DcsvIo.D2.Auth.Scopes.SourceGen.ScopesGenerator",
            "ProductScopes.g.cs");

    private static readonly string sr_productAudiencesPath =
        Path.Combine(
            TestPaths.RepoRoot(),
            "private",
            "packages",
            "dotnet",
            "auth",
            "abstractions-extensions",
            "Generated",
            "DcsvIo.D2.Auth.Audiences.SourceGen",
            "DcsvIo.D2.Auth.Audiences.SourceGen.AudiencesGenerator",
            "ProductAudiences.g.cs");

    private static readonly string sr_productEncryptionPath =
        Path.Combine(
            TestPaths.RepoRoot(),
            "private",
            "packages",
            "dotnet",
            "encryption",
            "extensions",
            "Generated",
            "DcsvIo.D2.EncryptionDomains.SourceGen",
            "DcsvIo.D2.EncryptionDomains.SourceGen.EncryptionDomainsGenerator",
            "ProductEncryptionDomains.g.cs");

    private static readonly string sr_productTkPath =
        Path.Combine(
            TestPaths.RepoRoot(),
            "private",
            "packages",
            "dotnet",
            "i18n",
            "keys-extensions",
            "Generated",
            "DcsvIo.D2.I18n.SourceGen",
            "DcsvIo.D2.I18n.SourceGen.TKGenerator",
            "ProductTK.g.cs");

    [Fact]
    public void ProductScopes_RegeneratedOutput_MatchesCommittedFile()
    {
        File.Exists(sr_productScopesPath).Should().BeTrue(
            "ProductScopes.g.cs must be committed under Auth.Abstractions.Extensions");

        var publicSpec = File.ReadAllText(
            Path.Combine(TestPaths.PublicContractsRoot(), "auth-scopes", "scopes.spec.json"));
        var privateSpec = File.ReadAllText(
            Path.Combine(TestPaths.PrivateContractsRoot(), "auth-scopes", "scopes.spec.json"));

        var regenerated = RunScopesGenerator(publicSpec, privateSpec)["ProductScopes.g.cs"];
        var committed = File.ReadAllText(sr_productScopesPath);

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "committed ProductScopes.g.cs must match public∪private regeneration "
                + "under DcsvIo.D2.Private.Auth.Abstractions.Extensions; run dotnet build to regenerate");
    }

    [Fact]
    public void ProductAudiences_RegeneratedOutput_MatchesCommittedFile()
    {
        File.Exists(sr_productAudiencesPath).Should().BeTrue();

        var publicSpec = File.ReadAllText(
            Path.Combine(TestPaths.PublicContractsRoot(), "auth-audiences", "audiences.spec.json"));
        var privateSpec = File.ReadAllText(
            Path.Combine(TestPaths.PrivateContractsRoot(), "auth-audiences", "audiences.spec.json"));

        var regenerated = RunAudiencesGenerator(publicSpec, privateSpec)["ProductAudiences.g.cs"];
        var committed = File.ReadAllText(sr_productAudiencesPath);

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "committed ProductAudiences.g.cs must match public∪private regeneration "
                + "under DcsvIo.D2.Private.Auth.Abstractions.Extensions");
    }

    [Fact]
    public void ProductEncryptionDomains_RegeneratedOutput_MatchesCommittedFile()
    {
        File.Exists(sr_productEncryptionPath).Should().BeTrue();

        var publicSpec = File.ReadAllText(
            Path.Combine(
                TestPaths.PublicContractsRoot(),
                "encryption-domains",
                "encryption-domains.spec.json"));
        var privateSpec = File.ReadAllText(
            Path.Combine(
                TestPaths.PrivateContractsRoot(),
                "encryption-domains",
                "encryption-domains.spec.json"));

        var regenerated = RunEncryptionGenerator(publicSpec, privateSpec)[
            "ProductEncryptionDomains.g.cs"];
        var committed = File.ReadAllText(sr_productEncryptionPath);

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "committed ProductEncryptionDomains.g.cs must match public∪private regeneration "
                + "under DcsvIo.D2.Private.Encryption.Extensions");
    }

    [Fact]
    public void ProductTK_RegeneratedOutput_MatchesCommittedFile()
    {
        File.Exists(sr_productTkPath).Should().BeTrue();

        var publicEn = File.ReadAllText(
            Path.Combine(TestPaths.MessagesDirectory(), "en-US.json"));
        var privateEn = File.ReadAllText(
            Path.Combine(TestPaths.PrivateMessagesDirectory(), "en-US.json"));

        var regenerated = RunTkGenerator(publicEn, privateEn)["ProductTK.g.cs"];
        var committed = File.ReadAllText(sr_productTkPath);

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "committed ProductTK.g.cs must match public∪private regeneration "
                + "under DcsvIo.D2.Private.I18n.Keys.Extensions");
    }

    [Fact]
    public void ProductScopes_DriftedPrivateSpec_DoesNotMatchCommittedFile()
    {
        File.Exists(sr_productScopesPath).Should().BeTrue();

        var publicSpec = File.ReadAllText(
            Path.Combine(TestPaths.PublicContractsRoot(), "auth-scopes", "scopes.spec.json"));
        var privateSpec = File.ReadAllText(
            Path.Combine(TestPaths.PrivateContractsRoot(), "auth-scopes", "scopes.spec.json"));

        // Deliberate drift: append a marker into the first description value.
        const string marker = " DRIFT-TEST-MARKER.";
        var idx = privateSpec.IndexOf("\"description\"", System.StringComparison.Ordinal);
        idx.Should().BeGreaterThanOrEqualTo(
            0,
            because: "private scopes catalog must contain descriptions for drift mutation");
        var colon = privateSpec.IndexOf(':', idx);
        var openQuote = privateSpec.IndexOf('"', colon + 1);
        var closeQuote = privateSpec.IndexOf('"', openQuote + 1);
        var drifted = string.Concat(
            privateSpec.AsSpan(0, closeQuote),
            marker,
            privateSpec.AsSpan(closeQuote));

        var regenerated = RunScopesGenerator(publicSpec, drifted)["ProductScopes.g.cs"];
        var committed = File.ReadAllText(sr_productScopesPath);

        Normalize(regenerated).Should().NotBe(
            Normalize(committed),
            because:
                "a deliberately drifted private scopes.spec must produce ProductScopes "
                + "output that differs from the committed file — proves parity is non-vacuous");
    }

    private static Dictionary<string, string> RunScopesGenerator(
        string publicSpec,
        string privateSpec)
    {
        var orgTypeSource = File.ReadAllText(Path.Combine(sr_authAbstractionsBase, "OrgType.cs"));
        var roleSource = File.ReadAllText(Path.Combine(sr_authAbstractionsBase, "Role.cs"));

        var compilation = CSharpCompilation.Create(
            assemblyName: "DcsvIo.D2.Private.Auth.Abstractions.Extensions",
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
            new InMemoryAdditionalText("public/scopes.spec.json", publicSpec),
            new InMemoryAdditionalText("private/scopes.spec.json", privateSpec),
        ];

        var driver = CSharpGeneratorDriver.Create(
            generators: [new ScopesGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts.ToImmutableArray());

        var result = driver.RunGenerators(compilation).GetRunResult();
        result.Diagnostics.Should().BeEmpty(
            because: "clean public∪private scopes must produce no diagnostics");

        return result.GeneratedTrees.ToDictionary(
            t => Path.GetFileName(t.FilePath),
            t => t.GetText().ToString());
    }

    private static Dictionary<string, string> RunAudiencesGenerator(
        string publicSpec,
        string privateSpec)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "DcsvIo.D2.Private.Auth.Abstractions.Extensions",
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        AdditionalText[] additionalTexts =
        [
            new InMemoryAdditionalText("public/audiences.spec.json", publicSpec),
            new InMemoryAdditionalText("private/audiences.spec.json", privateSpec),
        ];

        var driver = CSharpGeneratorDriver.Create(
            generators: [new AudiencesGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts.ToImmutableArray());

        var result = driver.RunGenerators(compilation).GetRunResult();
        result.Diagnostics.Should().BeEmpty();

        return result.GeneratedTrees.ToDictionary(
            t => Path.GetFileName(t.FilePath),
            t => t.GetText().ToString());
    }

    private static Dictionary<string, string> RunEncryptionGenerator(
        string publicSpec,
        string privateSpec)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "DcsvIo.D2.Private.Encryption.Extensions",
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        AdditionalText[] additionalTexts =
        [
            new InMemoryAdditionalText("public/encryption-domains.spec.json", publicSpec),
            new InMemoryAdditionalText("private/encryption-domains.spec.json", privateSpec),
        ];

        var driver = CSharpGeneratorDriver.Create(
            generators: [new EncryptionDomainsGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts.ToImmutableArray());

        var result = driver.RunGenerators(compilation).GetRunResult();
        result.Diagnostics.Should().BeEmpty();

        return result.GeneratedTrees.ToDictionary(
            t => Path.GetFileName(t.FilePath),
            t => t.GetText().ToString());
    }

    private static Dictionary<string, string> RunTkGenerator(
        string publicEnUs,
        string privateEnUs)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: "DcsvIo.D2.Private.I18n.Keys.Extensions",
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        AdditionalText[] additionalTexts =
        [
            new InMemoryAdditionalText("public/contracts/messages/en-US.json", publicEnUs),
            new InMemoryAdditionalText("private/contracts/messages/en-US.json", privateEnUs),
        ];

        var driver = CSharpGeneratorDriver.Create(
            generators: [new TKGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts.ToImmutableArray());

        var result = driver.RunGenerators(compilation).GetRunResult();
        result.Diagnostics.Should().BeEmpty();

        return result.GeneratedTrees.ToDictionary(
            t => Path.GetFileName(t.FilePath),
            t => t.GetText().ToString());
    }

    private static string Normalize(string source) =>
        source.Replace("\r\n", "\n").Replace("\r", "\n");

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText r_text;

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            r_text = SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText GetText(
            System.Threading.CancellationToken cancellationToken = default) => r_text;
    }
}
