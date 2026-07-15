// -----------------------------------------------------------------------
// <copyright file="TKGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.I18n.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.I18n.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests for dual-target TK emission —
/// public <c>DcsvIo.D2.I18n.Keys</c> → <c>TK</c>; private
/// <c>DcsvIo.D2.Private.I18n.Keys.Extensions</c> → <c>ProductTK</c> (distinct FQN).
/// </summary>
public sealed class TKGeneratorTests
{
    private const string _PUBLIC_EN_US = """
    {
      "common_errors_NOT_FOUND": "Not found."
    }
    """;

    private const string _PRIVATE_EN_US = """
    {
      "keycustodian_errors_KID_INVALID": "Kid invalid.",
      "files_errors_NOT_FOUND": "File not found."
    }
    """;

    [Fact]
    public void Generator_PublicKeysAssembly_EmitsTKUnderSharedI18n()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.I18n.Keys",
            catalogs:
            [
                ("contracts/messages/en-US.json", _PUBLIC_EN_US),
            ]);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        Path.GetFileName(result.GeneratedTrees.Single().FilePath).Should().Be("TK.g.cs");

        var src = result.GeneratedTrees.Single().ToString();
        src.Should().Contain("namespace DcsvIo.D2.I18n;");
        src.Should().Contain("public static partial class TK");
        src.Should().Contain("NOT_FOUND");
    }

    [Fact]
    public void Generator_PrivateI18nKeysExtensions_MultiCatalog_EmitsProductTKUnion()
    {
        // Private host merges public∪private en-US catalogs into ProductTK.
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Private.I18n.Keys.Extensions",
            catalogs:
            [
                ("public/contracts/messages/en-US.json", _PUBLIC_EN_US),
                ("private/contracts/messages/en-US.json", _PRIVATE_EN_US),
            ]);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        Path.GetFileName(result.GeneratedTrees.Single().FilePath)
            .Should().Be("ProductTK.g.cs");

        var src = result.GeneratedTrees.Single().ToString();
        src.Should().Contain("namespace DcsvIo.D2.Private.I18n;");
        src.Should().Contain("public static partial class ProductTK");
        src.Should().Contain("NOT_FOUND");
        src.Should().Contain("KID_INVALID");
        src.Should().NotContain("public static partial class TK");
        src.Should().NotContain("namespace DcsvIo.D2.I18n;");
    }

    [Fact]
    public void Generator_WrongAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            catalogs:
            [
                ("contracts/messages/en-US.json", _PUBLIC_EN_US),
            ]);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_PrivateHost_MissingEnUs_EmitsMissingEnUsDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Private.I18n.Keys.Extensions",
            catalogs: []);

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MissingEnUsJson);
        result.GeneratedTrees.Should().HaveCount(1);
        result.GeneratedTrees.Single().ToString()
            .Should().Contain("public static partial class ProductTK");
    }

    private static GeneratorDriver RunGenerator(
        string assemblyName,
        (string Path, string Content)[] catalogs)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TKGenerator().AsSourceGenerator();

        var additionalTexts = ImmutableArray.CreateRange<AdditionalText>(
            catalogs.Select(c => new InMemoryAdditionalText(c.Path, c.Content)));

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts);

        return driver.RunGenerators(compilation);
    }

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
