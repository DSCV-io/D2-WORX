// -----------------------------------------------------------------------
// <copyright file="ScopesGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using D2.Shared.Auth.Scopes.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests — drive <see cref="ScopesGenerator"/>
/// via a synthetic <see cref="CSharpGeneratorDriver"/> rather than the build
/// pipeline. Asserts the assembly-name gate, AdditionalFiles wiring, and that
/// wildcard expansion respects the OrgType / Role enum members surfaced by the
/// compilation symbol.
/// </summary>
public sealed class ScopesGeneratorTests
{
    private const string _SAMPLE_SPEC = """
    {
      "scopes": [
        {
          "name": "self.read",
          "actionSensitivity": "Routine",
          "impersonationBlocked": false,
          "grantedTo": { "*": ["*"] }
        },
        {
          "name": "anon.public.health",
          "actionSensitivity": "Routine",
          "impersonationBlocked": false
        }
      ]
    }
    """;

    private const string _ENUM_SOURCE = """
    namespace D2.Shared.Auth.Abstractions
    {
        public enum OrgType { Admin, Customer }
        public enum Role { Owner, Agent }
        public enum ActionSensitivity { Routine, Sensitive, Critical }
    }
    """;

    [Fact]
    public void Generator_TargetAssemblyWithSpec_EmitsScopesGeneratedSource()
    {
        var driver = RunGenerator(
            assemblyName: "D2.Shared.Auth.Abstractions",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().NotBeEmpty();
        var generated = result.GeneratedTrees.Single();
        Path.GetFileName(generated.FilePath).Should().Be("Scopes.g.cs");

        var src = generated.ToString();
        src.Should().Contain("public static partial class Scopes");
        src.Should().Contain("\"self.read\"");
        src.Should().Contain("\"anon.public.health\"");
    }

    [Fact]
    public void Generator_NonTargetAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            specJson: _SAMPLE_SPEC);

        var result = driver.GetRunResult();

        // Generator no-ops for non-target assemblies — no Scopes.g.cs produced.
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpec_EmitsMissingSpecFileDiagnostic()
    {
        // No AdditionalText supplied — generator must fire D2SCP009.
        var driver = RunGenerator(
            assemblyName: "D2.Shared.Auth.Abstractions",
            specJson: null);

        var result = driver.GetRunResult();

        var diagnostics = result.Diagnostics;
        diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MissingSpecFile);
    }

    [Fact]
    public void Generator_MalformedSpec_EmitsMalformedSpecDiagnosticAndStillProducesEmptyShell()
    {
        var driver = RunGenerator(
            assemblyName: "D2.Shared.Auth.Abstractions",
            specJson: "{not valid");

        var result = driver.GetRunResult();

        result.Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);

        // Even on malformed input, an empty shell file is emitted so downstream
        // compilation can still see the Scopes type (avoids cascade errors).
        result.GeneratedTrees.Should().HaveCount(1);
    }

    [Fact]
    public void Generator_WildcardExpansion_UsesEnumMembersFromCompilation()
    {
        // Synthetic compilation supplies CUSTOM enum members (Admin + Customer
        // for OrgType; Owner + Agent for Role). Wildcard expansion must use
        // exactly these — not a hard-coded list inside the SrcGen.
        var driver = RunGenerator(
            assemblyName: "D2.Shared.Auth.Abstractions",
            specJson: _SAMPLE_SPEC);

        var src = driver.GetRunResult().GeneratedTrees.Single().ToString();

        // Cartesian product of {Admin, Customer} × {Owner, Agent} should appear.
        src.Should().Contain("[(OrgType.Admin, Role.Owner)]");
        src.Should().Contain("[(OrgType.Admin, Role.Agent)]");
        src.Should().Contain("[(OrgType.Customer, Role.Owner)]");
        src.Should().Contain("[(OrgType.Customer, Role.Agent)]");

        // No mention of enum members the synthetic compilation does NOT define.
        src.Should().NotContain("OrgType.Support");
        src.Should().NotContain("Role.Auditor");
    }

    [Fact]
    public void Generator_RunTwice_SameInputs_ProducesIdenticalOutput()
    {
        // Cache stability — identical inputs must produce identical generator
        // output (otherwise downstream incremental builds re-run unnecessarily).
        var first = RunGenerator(
                assemblyName: "D2.Shared.Auth.Abstractions",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees.Single().ToString();

        var second = RunGenerator(
                assemblyName: "D2.Shared.Auth.Abstractions",
                specJson: _SAMPLE_SPEC)
            .GetRunResult().GeneratedTrees.Single().ToString();

        Normalize(second).Should().Be(Normalize(first));
    }

    private static GeneratorDriver RunGenerator(string assemblyName, string? specJson)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [CSharpSyntaxTree.ParseText(_ENUM_SOURCE)],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ScopesGenerator().AsSourceGenerator();

        var additionalTexts = specJson is null
            ? ImmutableArray<AdditionalText>.Empty
            : ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
                "scopes.spec.json",
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

        public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default) => r_text;
    }
}
