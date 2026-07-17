// -----------------------------------------------------------------------
// <copyright file="SealedFrameGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Encryption.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.EncryptionFrame.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests for the SealedFrame source-gen
/// arm — drives <see cref="SealedFrameGenerator"/> via a synthetic
/// <see cref="CSharpGeneratorDriver"/> rather than the build pipeline, and
/// pins that the sealed generator never reads the symmetric spec (and vice
/// versa — different file-name filters).
/// </summary>
public sealed class SealedFrameGeneratorTests
{
    private const string _SAMPLE_SPEC = """
    {
      "version": 2,
      "fields": [
        {
          "constName": "VERSION",
          "offset": 0,
          "length": 1,
          "kind": "byte_fixed",
          "doc": "doc"
        }
      ],
      "constraints": {
        "minKidLength": 1,
        "maxKidLength": 64,
        "ephPubLengthPrefixSize": 2,
        "maxEphPubLength": 256,
        "nonceLength": 12,
        "tagLength": 16,
        "minFrameSize": 34
      }
    }
    """;

    [Fact]
    public void Generator_TargetAssemblyWithSpec_EmitsSealedFrameLayoutGeneratedSource()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Encryption",
            specFileName: "encryption-frame-sealed.spec.json",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().HaveCount(1);
        Path.GetFileName(result.GeneratedTrees[0].FilePath)
            .Should().Be("SealedFrameLayout.g.cs");
    }

    [Fact]
    public void Generator_NonTargetAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specFileName: "encryption-frame-sealed.spec.json",
            specJson: _SAMPLE_SPEC);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpec_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Encryption",
            specFileName: null,
            specJson: null);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_SymmetricSpecFileNameOnly_EmitsNothing()
    {
        // The sealed generator filters on the SEALED spec file name — the
        // symmetric catalog must never feed it.
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Encryption",
            specFileName: "encryption-frame.spec.json",
            specJson: _SAMPLE_SPEC);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_MalformedSpec_EmitsSealedMalformedSpecDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Encryption",
            specFileName: "encryption-frame-sealed.spec.json",
            specJson: "{not valid");

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.SealedMalformedSpec);
    }

    [Fact]
    public void Generator_RunTwice_SameInputs_ProducesIdenticalOutput()
    {
        var first = RunGenerator(
                assemblyName: "DcsvIo.D2.Encryption",
                specFileName: "encryption-frame-sealed.spec.json",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();
        var second = RunGenerator(
                assemblyName: "DcsvIo.D2.Encryption",
                specFileName: "encryption-frame-sealed.spec.json",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();

        second.Count.Should().Be(first.Count);

        for (var i = 0; i < first.Count; i++)
            Normalize(second[i]).Should().Be(Normalize(first[i]));
    }

    private static GeneratorDriver RunGenerator(
        string assemblyName, string? specFileName, string? specJson)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new SealedFrameGenerator().AsSourceGenerator();

        var additionalTexts = specFileName is null || specJson is null
            ? ImmutableArray<AdditionalText>.Empty
            : ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
                specFileName,
                specJson));

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts);

        return driver.RunGenerators(compilation);
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();

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
