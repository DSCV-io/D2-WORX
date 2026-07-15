// -----------------------------------------------------------------------
// <copyright file="InProcessKeysGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.InProcessKeys;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.InProcessKeys.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests — drive
/// <see cref="InProcessKeysGenerator"/> via a synthetic
/// <see cref="CSharpGeneratorDriver"/> rather than the build pipeline.
/// Asserts assembly-name dispatch (http vs grpc vs unrecognized),
/// AdditionalFiles wiring, missing-spec + malformed-spec degradation paths,
/// and cache stability — covering the generator pipeline surface that the
/// emitter unit tests do not reach.
/// </summary>
public sealed class InProcessKeysGeneratorTests
{
    private const string _SAMPLE_SPEC = """
    {
      "keys": [
        {
          "constName": "REQUEST_CONTEXT",
          "value": "d2.request_context",
          "purpose": "Per-request context slot.",
          "bindings": ["http", "grpc"]
        },
        {
          "constName": "HTTP_ONLY",
          "value": "d2.http_only",
          "purpose": "Http-only slot.",
          "bindings": ["http"]
        },
        {
          "constName": "GRPC_ONLY",
          "value": "d2.grpc_only",
          "purpose": "Grpc-only slot.",
          "bindings": ["grpc"]
        }
      ]
    }
    """;

    [Fact]
    public void Generator_HttpConsumingAssembly_EmitsHttpContextItemsOnly()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);

        var generated = result.GeneratedTrees.Single();
        Path.GetFileName(generated.FilePath).Should().Be("D2HttpContextItems.g.cs");

        var src = generated.ToString();
        src.Should().Contain("namespace DcsvIo.D2.Auth.Abstractions.Http");
        src.Should().Contain("public static class D2HttpContextItems");
        src.Should().Contain("REQUEST_CONTEXT = \"d2.request_context\"");
        src.Should().Contain("HTTP_ONLY = \"d2.http_only\"");
        src.Should().NotContain("GRPC_ONLY");
    }

    [Fact]
    public void Generator_GrpcConsumingAssembly_EmitsGrpcUserStateKeysOnly()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Grpc",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();
        result.GeneratedTrees.Should().HaveCount(1);

        var generated = result.GeneratedTrees.Single();
        Path.GetFileName(generated.FilePath).Should().Be("D2GrpcUserStateKeys.g.cs");

        var src = generated.ToString();
        src.Should().Contain("namespace DcsvIo.D2.Auth.Grpc.Interceptors");
        src.Should().Contain("internal static class D2GrpcUserStateKeys");
        src.Should().Contain("REQUEST_CONTEXT = \"d2.request_context\"");
        src.Should().Contain("GRPC_ONLY = \"d2.grpc_only\"");
        src.Should().NotContain("HTTP_ONLY");
    }

    [Fact]
    public void Generator_UnrecognizedAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        // Generator is a strict per-assembly dispatcher — non-target assemblies
        // produce zero output and zero diagnostics (no "missing spec" complaint
        // for an assembly that wasn't asking for the catalog).
        result.GeneratedTrees.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpec_EmitsMissingSpecDiagnostic()
    {
        // No AdditionalText supplied — generator must fire D2IPK004 (MissingSpec)
        // because the consuming assembly DOES expect the catalog.
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: null);

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MissingSpec);
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_MalformedSpec_EmitsMalformedSpecDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: "{not valid");

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Generator_SpecWithInvalidConstName_EmitsInvalidConstNameDiagnostic()
    {
        const string spec = """
        {
          "keys": [
            {
              "constName": "lower_bad",
              "value": "d2.bad",
              "purpose": "Test.",
              "bindings": ["http"]
            }
          ]
        }
        """;

        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: spec);

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.InvalidConstName);
    }

    [Fact]
    public void Generator_SpecWithUnknownBinding_EmitsUnknownBindingDiagnostic()
    {
        const string spec = """
        {
          "keys": [
            {
              "constName": "BAD_BINDING",
              "value": "d2.bad",
              "purpose": "Test.",
              "bindings": ["websocket"]
            }
          ]
        }
        """;

        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Auth.Abstractions",
            specJson: spec);

        var result = driver.GetRunResult();
        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.UnknownBinding);
    }

    [Fact]
    public void Generator_RunTwice_SameInputs_ProducesIdenticalOutput()
    {
        // Cache stability — identical inputs must produce identical generator
        // output (otherwise downstream incremental builds re-run unnecessarily).
        var first = RunGenerator(
                assemblyName: "DcsvIo.D2.Auth.Abstractions",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees.Single().ToString();

        var second = RunGenerator(
                assemblyName: "DcsvIo.D2.Auth.Abstractions",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees.Single().ToString();

        Normalize(second).Should().Be(Normalize(first));
    }

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

        var generator = new InProcessKeysGenerator().AsSourceGenerator();

        var additionalTexts = specJson is null
            ? ImmutableArray<AdditionalText>.Empty
            : ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
                "keys.spec.json",
                specJson));

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts);

        return driver.RunGenerators(compilation);
    }

    private static string Normalize(string s) => s.Replace("\r\n", "\n").Trim();

    /// <summary>
    /// Minimal AdditionalText shim for synthesizing AdditionalFiles in
    /// generator tests without filesystem I/O.
    /// </summary>
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
