// -----------------------------------------------------------------------
// <copyright file="ContextGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Context.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DcsvIo.D2.Context.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests for <see cref="ContextGenerator"/>.
/// Drives synthetic compilations targeting each of the three recognized
/// assembly names and asserts the per-target dispatch (interface emission vs
/// mutable emission vs no-op).
/// </summary>
public sealed class ContextGeneratorTests
{
    private const string _AUTH_SPEC = """
    {
      "name": "IAuthContext",
      "namespace": "DcsvIo.D2.AuthContext.Abstractions",
      "extends": null,
      "sections": [
        {
          "name": "Token",
          "properties": [
            { "name": "IsAuthenticated", "type": "bool?", "trinaryAuth": true },
            { "name": "Subject", "type": "string?", "claim": "sub" }
          ]
        }
      ]
    }
    """;

    private const string _REQUEST_SPEC = """
    {
      "name": "IRequestContext",
      "namespace": "DcsvIo.D2.Context.Abstractions",
      "extends": "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
      "sections": [
        {
          "name": "Tracing",
          "properties": [ { "name": "TraceId", "type": "string?" } ]
        }
      ]
    }
    """;

    private const string _REQUEST_SPEC_WITH_ESTABLISHMENT = """
    {
      "name": "IRequestContext",
      "namespace": "DcsvIo.D2.Context.Abstractions",
      "extends": "DcsvIo.D2.AuthContext.Abstractions.IAuthContext",
      "sections": [
        {
          "name": "Tracing",
          "properties": [ { "name": "TraceId", "type": "string?" } ]
        },
        {
          "name": "Establishment",
          "properties": [
            { "name": "Origin", "type": "RequestOrigin" },
            { "name": "ImmediateCaller", "type": "string?" },
            {
              "name": "CallPath",
              "type": "IReadOnlyList<CallPathEntry>",
              "propagate": true,
              "maxLength": 16,
              "entryIdMaxLength": 128
            }
          ]
        }
      ]
    }
    """;

    [Fact]
    public void Generator_AuthContextAbstractionsAssembly_EmitsIAuthContextOnly()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.AuthContext.Abstractions",
            authSpec: _AUTH_SPEC,
            requestSpec: _REQUEST_SPEC);

        var trees = driver.GetRunResult().GeneratedTrees;
        trees.Should().HaveCount(1);
        Path.GetFileName(trees.Single().FilePath).Should().Be("IAuthContext.g.cs");

        trees.Single().ToString().Should().Contain("public interface IAuthContext");
    }

    [Fact]
    public void Generator_ContextAbstractionsAssembly_EmitsIRequestContextWithExtendsClause()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Context.Abstractions",
            authSpec: _AUTH_SPEC,
            requestSpec: _REQUEST_SPEC);

        var trees = driver.GetRunResult().GeneratedTrees;
        var iRequest = trees.Single(
            t => Path.GetFileName(t.FilePath) == "IRequestContext.g.cs");
        iRequest.ToString().Should().Contain(
            "public interface IRequestContext : " +
            "global::DcsvIo.D2.AuthContext.Abstractions.IAuthContext");
    }

    [Fact]
    public void Generator_ContextAbstractionsAssembly_EmitsAllRequestSideArtifacts()
    {
        // The unified abstractions target receives the IRequestContext
        // interface PLUS MutableRequestContext + the PropagatedContext trio
        // (record / extensions / serializer) — five files in total.
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Context.Abstractions",
            authSpec: _AUTH_SPEC,
            requestSpec: _REQUEST_SPEC);

        var trees = driver.GetRunResult().GeneratedTrees.ToArray();
        var fileNames = trees.Select(t => Path.GetFileName(t.FilePath)).ToArray();
        fileNames.Should().Contain("IRequestContext.g.cs");
        fileNames.Should().Contain("MutableRequestContext.g.cs");
        fileNames.Should().Contain("PropagatedContext.g.cs");
        fileNames.Should().Contain("PropagatedContextExtensions.g.cs");
        fileNames.Should().Contain("PropagatedContextSerializer.g.cs");
    }

    [Fact]
    public void Generator_EstablishmentSpec_EmitsThreeFieldsOnInterfaceAndMutable()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Context.Abstractions",
            authSpec: _AUTH_SPEC,
            requestSpec: _REQUEST_SPEC_WITH_ESTABLISHMENT);

        var trees = driver.GetRunResult().GeneratedTrees.ToArray();

        var iRequest = trees
            .Single(t => Path.GetFileName(t.FilePath) == "IRequestContext.g.cs").ToString();
        iRequest.Should().Contain("RequestOrigin Origin { get; }");
        iRequest.Should().Contain("string? ImmediateCaller { get; }");
        iRequest.Should().Contain("IReadOnlyList<CallPathEntry> CallPath { get; }");

        var mutable = trees
            .Single(t => Path.GetFileName(t.FilePath) == "MutableRequestContext.g.cs").ToString();

        // Origin defaults to the fail-closed Unestablished; CallPath to an empty
        // list; ImmediateCaller to null.
        mutable.Should().Contain(
            "public RequestOrigin Origin { get; set; } = RequestOrigin.Unestablished;");
        mutable.Should().Contain("public IReadOnlyList<CallPathEntry> CallPath { get; set; } = [];");
        mutable.Should().Contain("public string? ImmediateCaller { get; set; } = null;");
    }

    [Fact]
    public void Generator_EstablishmentSpec_PropagatedContextHasCallPathButNotOriginOrCaller()
    {
        // Anti-spoofing invariant #1 at the generator level: only propagate:true
        // fields enter PropagatedContext. Origin + ImmediateCaller (non-propagated,
        // authority-grade) must NOT appear; CallPath (telemetry) must.
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Context.Abstractions",
            authSpec: _AUTH_SPEC,
            requestSpec: _REQUEST_SPEC_WITH_ESTABLISHMENT);

        var record = driver.GetRunResult().GeneratedTrees
            .Single(t => Path.GetFileName(t.FilePath) == "PropagatedContext.g.cs").ToString();

        record.Should().Contain("public IReadOnlyList<CallPathEntry>? CallPath { get; init; }");
        record.Should().NotContain("Origin");
        record.Should().NotContain("ImmediateCaller");
    }

    [Fact]
    public void Generator_OtherAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Random.Assembly",
            authSpec: _AUTH_SPEC,
            requestSpec: _REQUEST_SPEC);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_RequestContextAssemblyMissingAuthSpec_EmitsMissingSpecFileDiagnostic()
    {
        // The combined emitter requires BOTH specs — auth is needed even when
        // the target is RequestContext. If only request spec is present, fire
        // D2CTX006.
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.Context.Abstractions",
            authSpec: null,
            requestSpec: _REQUEST_SPEC);

        var diagnostics = driver.GetRunResult().Diagnostics;
        diagnostics.Should().Contain(d => d.Id == DiagnosticIds.MissingSpecFile);
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpecsAtAll_EmitsMissingSpecFileDiagnostic()
    {
        var driver = RunGenerator(
            assemblyName: "DcsvIo.D2.AuthContext.Abstractions",
            authSpec: null,
            requestSpec: null);

        var diagnostics = driver.GetRunResult().Diagnostics;
        diagnostics.Should().Contain(d => d.Id == DiagnosticIds.MissingSpecFile);
    }

    [Fact]
    public void Generator_RunTwice_SameInputs_ProducesIdenticalOutput()
    {
        // Cache stability — identical inputs must produce identical generator
        // output (so downstream incremental builds can reuse cached results).
        var firstSrc = RunGenerator(
                assemblyName: "DcsvIo.D2.Context.Abstractions",
                authSpec: _AUTH_SPEC,
                requestSpec: _REQUEST_SPEC)
            .GetRunResult().GeneratedTrees
                .Single(t => Path.GetFileName(t.FilePath) == "MutableRequestContext.g.cs")
            .ToString();

        var secondSrc = RunGenerator(
                assemblyName: "DcsvIo.D2.Context.Abstractions",
                authSpec: _AUTH_SPEC,
                requestSpec: _REQUEST_SPEC)
            .GetRunResult().GeneratedTrees
                .Single(t => Path.GetFileName(t.FilePath) == "MutableRequestContext.g.cs")
            .ToString();

        Normalize(secondSrc).Should().Be(Normalize(firstSrc));
    }

    private static GeneratorDriver RunGenerator(
        string assemblyName, string? authSpec, string? requestSpec)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ContextGenerator().AsSourceGenerator();

        var additionalTexts = ImmutableArray.CreateBuilder<AdditionalText>();
        if (authSpec is not null)
            additionalTexts.Add(new InMemoryAdditionalText("IAuthContext.spec.json", authSpec));
        if (requestSpec is not null)
        {
            additionalTexts.Add(
                new InMemoryAdditionalText("IRequestContext.spec.json", requestSpec));
        }

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts.ToImmutable());

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
