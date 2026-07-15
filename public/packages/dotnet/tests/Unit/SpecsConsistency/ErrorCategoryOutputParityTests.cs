// -----------------------------------------------------------------------
// <copyright file="ErrorCategoryOutputParityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias CategorySourceGen;

namespace D2.Shared.Tests.Unit.SpecsConsistency;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using CategorySourceGen::D2.Shared.ErrorCodes.Category.SourceGen;
using D2.Shared.Tests.Unit.Auth;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// CI-enforced byte-parity gate: regenerates the committed
/// <c>ErrorCategory.g.cs</c> from the real <c>error-category.spec.json</c>
/// (via the same <see cref="ErrorCategoryGenerator"/> path used by the Roslyn
/// build) and asserts the result equals the committed file byte-for-byte after
/// normalizing line endings to LF. A failure means the committed file is stale
/// and must be regenerated via <c>dotnet build</c>.
/// </summary>
public sealed class ErrorCategoryOutputParityTests
{
    private const string _TARGET_ASSEMBLY = "D2.Shared.ErrorCodes.Category";

    private static readonly string sr_generatedBase =
        Path.Combine(
            TestPaths.RepoRoot(),
            "public",
            "packages",
            "dotnet",
            "error-codes",
            "category",
            "Generated",
            "D2.Shared.ErrorCodes.Category.SourceGen",
            "D2.Shared.ErrorCodes.Category.SourceGen.ErrorCategoryGenerator");

    [Fact]
    public void ErrorCategory_RegeneratedOutput_MatchesCommittedFile()
    {
        var regenerated = RegenerateCategory()["ErrorCategory.g.cs"];
        var committed = File.ReadAllText(Path.Combine(sr_generatedBase, "ErrorCategory.g.cs"));

        Normalize(regenerated).Should().Be(
            Normalize(committed),
            because:
                "the committed ErrorCategory.g.cs must be byte-identical to a "
                + "fresh generation from error-category.spec.json; run dotnet build "
                + "to regenerate");
    }

    private static Dictionary<string, string> RegenerateCategory()
    {
        var specPath = Path.Combine(
            TestPaths.RepoRoot(),
            "public",
            "contracts",
            "error-category",
            "error-category.spec.json");

        var additionalTexts = new List<AdditionalText>
        {
            new FileBackedAdditionalText(
                Path.GetFileName(specPath), File.ReadAllText(specPath)),
        };

        return RunGenerator(
            _TARGET_ASSEMBLY,
            new ErrorCategoryGenerator().AsSourceGenerator(),
            additionalTexts: [.. additionalTexts]);
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

        result.GetRunResult().Diagnostics.Should().BeEmpty(
            because: "a clean error-category spec must produce no diagnostics");

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
            CancellationToken cancellationToken = default) => r_text;
    }
}
