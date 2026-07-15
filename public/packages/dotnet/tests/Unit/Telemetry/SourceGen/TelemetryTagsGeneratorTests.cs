// -----------------------------------------------------------------------
// <copyright file="TelemetryTagsGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Telemetry.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Telemetry.Tags.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests for the telemetry-tags SrcGen.
/// </summary>
public sealed class TelemetryTagsGeneratorTests
{
    private const string _SAMPLE_SPEC = """
    {
      "meters": [
        {
          "meter": "TestMeter",
          "consumingAssembly": "Asm.A",
          "tagsClassName": "TestTags",
          "tagsNamespace": "Asm.A",
          "instruments": [
            {
              "name": "test.counter",
              "constName": "TestCounter",
              "kind": "counter",
              "description": "x",
              "tags": [
                { "name": "outcome", "values": ["ok", "err"] }
              ]
            }
          ]
        }
      ]
    }
    """;

    [Fact]
    public void Generator_TargetAssembly_EmitsTypedConstantsFile()
    {
        var driver = RunGenerator(
            assemblyName: "Asm.A",
            telemetrySpec: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().HaveCount(1);
        var generated = result.GeneratedTrees.Single();
        Path.GetFileName(generated.FilePath).Should().Be("TestTags.g.cs");
    }

    [Fact]
    public void Generator_NonTargetAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            telemetrySpec: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_NoSpec_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Asm.A",
            telemetrySpec: null);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_MalformedSpec_EmitsMalformedSpecDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: "Asm.A",
            telemetrySpec: "{not valid");

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Generator_DuplicateMeter_EmitsDuplicateMeterDiagnostic()
    {
        var dupSpec = """
        {
          "meters": [
            {
              "meter": "Dup",
              "consumingAssembly": "Asm.A",
              "instruments": [
                { "name": "x", "kind": "counter", "description": "x" }
              ]
            },
            {
              "meter": "Dup",
              "consumingAssembly": "Asm.A",
              "instruments": [
                { "name": "y", "kind": "counter", "description": "y" }
              ]
            }
          ]
        }
        """;

        var driver = RunGenerator("Asm.A", dupSpec);

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.DuplicateMeter);
    }

    [Fact]
    public void Generator_CrossSpecResolutionWithSiblingPresent_EmitsExpectedFile()
    {
        const string telemetrySpec = """
        {
          "meters": [
            {
              "meter": "M",
              "consumingAssembly": "Asm.A",
              "tagsClassName": "MTags",
              "tagsNamespace": "Asm.A",
              "instruments": [
                {
                  "name": "x",
                  "constName": "X",
                  "kind": "counter",
                  "description": "x",
                  "tags": [
                    { "name": "code", "valuesFromSpec": "auth-error-codes" }
                  ]
                }
              ]
            }
          ]
        }
        """;

        const string siblingSpec = """
        {
          "errorCodes": [
            { "code": "AUTH_A", "httpStatus": 401, "category": "validation_failure",
              "userMessageKey": "TK", "factoryName": "A", "doc": "A" }
          ]
        }
        """;

        var driver = RunGeneratorWithSibling(
            "Asm.A", telemetrySpec, siblingSpec);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);
        var src = result.GeneratedTrees.Single().ToString();

        // Cross-spec tag emits TAG_CODE constant only — no nested values class
        // (consumers reference AuthErrorCodes.* directly).
        src.Should().Contain("public const string TAG_CODE = \"code\";");
        src.Should().NotContain("public static class Code");
    }

    [Fact]
    public void Generator_CrossSpecResolutionWithoutSibling_EmitsCrossSpecDiagnostic()
    {
        const string telemetrySpec = """
        {
          "meters": [
            {
              "meter": "M",
              "consumingAssembly": "Asm.A",
              "instruments": [
                {
                  "name": "x",
                  "kind": "counter",
                  "description": "x",
                  "tags": [
                    { "name": "code", "valuesFromSpec": "auth-error-codes" }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var driver = RunGenerator("Asm.A", telemetrySpec);

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.CrossSpecInconsistency);
    }

    private static GeneratorDriver RunGenerator(string assemblyName, string? telemetrySpec)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TelemetryTagsGenerator().AsSourceGenerator();

        var additionalTexts = telemetrySpec is null
            ? ImmutableArray<AdditionalText>.Empty
            : ImmutableArray.Create<AdditionalText>(
                new InMemoryAdditionalText("telemetry.spec.json", telemetrySpec));

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts);

        return driver.RunGenerators(compilation);
    }

    private static GeneratorDriver RunGeneratorWithSibling(
        string assemblyName, string telemetrySpec, string authErrorCodesSpec)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references: [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TelemetryTagsGenerator().AsSourceGenerator();

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText("telemetry.spec.json", telemetrySpec),
            new InMemoryAdditionalText("auth-error-codes.spec.json", authErrorCodesSpec));

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
