// -----------------------------------------------------------------------
// <copyright file="AdvisoryLocksGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AdvisoryLocks.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.AdvisoryLocks.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// <see cref="IIncrementalGenerator"/> integration tests for
/// <see cref="AdvisoryLocksGenerator"/>: drives the generator via a synthetic
/// <see cref="CSharpGeneratorDriver"/> rather than the build pipeline.
/// Verifies single-target dispatch, spec-not-found guard, malformed-spec
/// diagnostics, and output determinism.
/// </summary>
public sealed class AdvisoryLocksGeneratorTests
{
    private const string _SAMPLE_SPEC = """
        {
          "locks": [
            {
              "constName": "MIGRATOR",
              "database": "d2-keycustodian",
              "key": 1001001001,
              "doc": "Migration lock."
            }
          ]
        }
        """;

    // =========================================================================
    // Target-assembly dispatch
    // =========================================================================

    [Fact]
    public void Generator_TargetAssemblyWithSpec_EmitsAdvisoryLocksGeneratedSource()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Private.Edge.KeyCustodian.Infra",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        var fileName = Path.GetFileName(result.GeneratedTrees[0].FilePath);
        fileName.Should().Be("AdvisoryLocks.g.cs");
    }

    [Fact]
    public void Generator_NonTargetAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specJson: _SAMPLE_SPEC);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpec_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Private.Edge.KeyCustodian.Infra",
            specJson: null);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    // =========================================================================
    // Diagnostics surfaced through generator pipeline
    // =========================================================================

    [Fact]
    public void Generator_MalformedSpec_EmitsMalformedSpecDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Private.Edge.KeyCustodian.Infra",
            specJson: "{bad json");

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Generator_SpecWithDuplicateKey_EmitsDuplicateKeyDiagnostic()
    {
        const string spec = """
            {
              "locks": [
                {"constName": "A", "database": "db", "key": 100, "doc": "d"},
                {"constName": "B", "database": "db", "key": 100, "doc": "d"}
              ]
            }
            """;

        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Private.Edge.KeyCustodian.Infra",
            specJson: spec);

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.DuplicateKeyInDatabase);
    }

    // =========================================================================
    // Output determinism
    // =========================================================================

    [Fact]
    public void Generator_RunTwice_SameInputs_ProducesIdenticalOutput()
    {
        var first = RunGenerator(
                assemblyName: "DcsvIo.D2.Private.Edge.KeyCustodian.Infra",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();

        var second = RunGenerator(
                assemblyName: "DcsvIo.D2.Private.Edge.KeyCustodian.Infra",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();

        first.Should().BeEquivalentTo(second);
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private static GeneratorDriver RunGenerator(string assemblyName, string? specJson)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var additionalTexts = specJson is null
            ? ImmutableArray<AdditionalText>.Empty
            : ImmutableArray.Create<AdditionalText>(
                new InMemoryAdditionalText("advisory-locks.spec.json", specJson));

        var driver = CSharpGeneratorDriver.Create(
            generators: [new AdvisoryLocksGenerator().AsSourceGenerator()],
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
