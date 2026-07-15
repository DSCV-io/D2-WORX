// -----------------------------------------------------------------------
// <copyright file="RegistryGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias RegistrySourceGen;

namespace DcsvIo.D2.Tests.Unit.ErrorCodesRegistry;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using RegistrySourceGen::DcsvIo.D2.ErrorCodes.Registry.SourceGen;
using Xunit;

/// <summary>
/// IIncrementalGenerator integration tests for <see cref="RegistryGenerator"/>.
/// Drives the generator via a synthetic <see cref="CSharpGeneratorDriver"/> over
/// planted <see cref="AdditionalText"/> inputs. Covers:
/// <list type="bullet">
///   <item>Normal emission into the target assembly.</item>
///   <item>No emission for non-target assemblies.</item>
///   <item>
///     Collision-fails-build: cross-catalog duplicate code fires
///     <see cref="RegistryDiagnosticIds.CrossCatalogDuplicateCode"/>
///     (<c>D2ERC004</c>) and suppresses emission.
///   </item>
///   <item>
///     Reserved-namespace violation: unprefixed code in a per-domain spec
///     fires <see cref="RegistryDiagnosticIds.ReservedNamespaceViolation"/>
///     (<c>D2ERC005</c>).
///   </item>
/// </list>
/// </summary>
public sealed class RegistryGeneratorTests
{
    private const string _TARGET_ASSEMBLY = "DcsvIo.D2.ErrorCodes.Registry";

    // Minimal valid generic spec (unprefixed codes, common domain).
    private const string _GENERIC_SPEC = """
    {
      "errorCodes": [
        {
          "code": "NOT_FOUND",
          "httpStatus": 404,
          "category": "not_found",
          "userMessageKey": "TK.Common.Errors.NOT_FOUND",
          "factoryName": "NotFound",
          "factoryShape": "standard",
          "doc": "Resource not found."
        }
      ]
    }
    """;

    // Minimal valid auth spec (AUTH_ prefixed codes).
    private const string _AUTH_SPEC = """
    {
      "errorCodes": [
        {
          "code": "AUTH_BEARER_MISSING",
          "httpStatus": 401,
          "category": "validation_failure",
          "userMessageKey": "TK.Auth.Errors.UNAUTHORIZED",
          "factoryName": "BearerMissing",
          "factoryShape": "standard",
          "doc": "Bearer missing."
        }
      ]
    }
    """;

    // Auth spec whose code collides with NOT_FOUND in the generic spec.
    private const string _AUTH_SPEC_WITH_COLLISION = """
    {
      "errorCodes": [
        {
          "code": "NOT_FOUND",
          "httpStatus": 401,
          "category": "validation_failure",
          "userMessageKey": "TK.Auth.Errors.UNAUTHORIZED",
          "factoryName": "NotFound",
          "factoryShape": "standard",
          "doc": "Collision."
        }
      ]
    }
    """;

    // Per-domain spec with an unprefixed code (no AUTH_).
    private const string _AUTH_SPEC_UNPREFIXED_CODE = """
    {
      "errorCodes": [
        {
          "code": "UNPREFIXED_CODE",
          "httpStatus": 401,
          "category": "validation_failure",
          "userMessageKey": "TK.Auth.Errors.UNAUTHORIZED",
          "factoryName": "UnprefixedCode",
          "factoryShape": "standard",
          "doc": "Missing prefix."
        }
      ]
    }
    """;

    // The closed error-category spec (the nine canonical wire strings).
    private const string _CATEGORY_SPEC = """
    {
      "categories": [
        { "wire": "validation_failure", "doc": "v" },
        { "wire": "not_found", "doc": "n" },
        { "wire": "conflict", "doc": "c" },
        { "wire": "policy_denied", "doc": "p" },
        { "wire": "rate_limited", "doc": "r" },
        { "wire": "payload_too_large", "doc": "p" },
        { "wire": "infrastructure_unavailable", "doc": "i" },
        { "wire": "internal_error", "doc": "i" },
        { "wire": "partial_success", "doc": "p" }
      ]
    }
    """;

    // Generic spec whose code references a category NOT in the closed set.
    private const string _GENERIC_SPEC_UNKNOWN_CATEGORY = """
    {
      "errorCodes": [
        {
          "code": "WEIRD_CODE",
          "httpStatus": 404,
          "category": "made_up_category",
          "userMessageKey": "TK.Common.Errors.NOT_FOUND",
          "factoryName": "WeirdCode",
          "factoryShape": "standard",
          "doc": "Unknown category."
        }
      ]
    }
    """;

    [Fact]
    public void Generator_TargetAssembly_EmitsRegistryFile()
    {
        var driver = RunGenerator(
            assemblyName: _TARGET_ASSEMBLY,
            additionalTexts:
            [
                new InMemoryAdditionalText("error-codes.spec.json", _GENERIC_SPEC),
                new InMemoryAdditionalText("auth-error-codes.spec.json", _AUTH_SPEC),
            ]);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().BeEmpty(
            because: "valid non-colliding specs must produce no diagnostics");
        result.GeneratedTrees.Should().HaveCount(1);
        Path.GetFileName(result.GeneratedTrees[0].FilePath).Should().Be("ErrorCodeRegistry.g.cs");
    }

    [Fact]
    public void Generator_NonTargetAssembly_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: "Some.Other.Assembly",
            additionalTexts:
            [
                new InMemoryAdditionalText("error-codes.spec.json", _GENERIC_SPEC),
            ]);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_NoSpecFiles_EmitsNothing()
    {
        var driver = RunGenerator(
            assemblyName: _TARGET_ASSEMBLY,
            additionalTexts: []);

        var result = driver.GetRunResult();

        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_CrossCatalogDuplicateCode_EmitsD2ERC004_AndNoRegistry()
    {
        var driver = RunGenerator(
            assemblyName: _TARGET_ASSEMBLY,
            additionalTexts:
            [
                new InMemoryAdditionalText("error-codes.spec.json", _GENERIC_SPEC),
                new InMemoryAdditionalText("auth-error-codes.spec.json", _AUTH_SPEC_WITH_COLLISION),
            ]);

        var result = driver.GetRunResult();

        result.Diagnostics
            .Should().Contain(
                d => d.Id == RegistryDiagnosticIds.CrossCatalogDuplicateCode,
                because: "two catalogs declaring the same code must fire D2ERC004");
        result.GeneratedTrees.Should().BeEmpty(
            because: "no registry must be emitted when there is a collision");
    }

    [Fact]
    public void Generator_ReservedNamespaceViolation_UnprefixedInDomainSpec_EmitsD2ERC005()
    {
        var driver = RunGenerator(
            assemblyName: _TARGET_ASSEMBLY,
            additionalTexts:
            [
                new InMemoryAdditionalText("error-codes.spec.json", _GENERIC_SPEC),
                new InMemoryAdditionalText("auth-error-codes.spec.json", _AUTH_SPEC_UNPREFIXED_CODE),
            ]);

        var result = driver.GetRunResult();

        result.Diagnostics
            .Should().Contain(
                d => d.Id == RegistryDiagnosticIds.ReservedNamespaceViolation,
                because: "an unprefixed code in a per-domain spec must fire D2ERC005");
        result.GeneratedTrees.Should().BeEmpty(
            because: "no registry must be emitted when there is a reserved-namespace violation");
    }

    [Fact]
    public void Generator_KnownCategoryWithCategorySpec_EmitsRegistry()
    {
        // The category spec is present and every code's category is a member —
        // the membership check (D2ERC007) must NOT fire and the registry emits.
        var driver = RunGenerator(
            assemblyName: _TARGET_ASSEMBLY,
            additionalTexts:
            [
                new InMemoryAdditionalText("error-codes.spec.json", _GENERIC_SPEC),
                new InMemoryAdditionalText("auth-error-codes.spec.json", _AUTH_SPEC),
                new InMemoryAdditionalText("error-category.spec.json", _CATEGORY_SPEC),
            ]);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().BeEmpty(
            because: "every code's category is a member of the closed set");
        result.GeneratedTrees.Should().HaveCount(1);
    }

    [Fact]
    public void Generator_UnknownCategoryWithCategorySpec_EmitsD2ERC007_AndNoRegistry()
    {
        var driver = RunGenerator(
            assemblyName: _TARGET_ASSEMBLY,
            additionalTexts:
            [
                new InMemoryAdditionalText("error-codes.spec.json", _GENERIC_SPEC_UNKNOWN_CATEGORY),
                new InMemoryAdditionalText("error-category.spec.json", _CATEGORY_SPEC),
            ]);

        var result = driver.GetRunResult();

        result.Diagnostics
            .Should().Contain(
                d => d.Id == RegistryDiagnosticIds.UnknownCategory,
                because: "a code referencing a category not in the closed set must fire D2ERC007");
        result.GeneratedTrees.Should().BeEmpty(
            because: "no registry must be emitted when a category is unknown");
    }

    [Fact]
    public void Generator_UnknownCategoryWithoutCategorySpec_DoesNotFireD2ERC007()
    {
        // Without the category spec the membership check degrades to a no-op
        // (the per-spec schema validation already covers malformed categories).
        var driver = RunGenerator(
            assemblyName: _TARGET_ASSEMBLY,
            additionalTexts:
            [
                new InMemoryAdditionalText("error-codes.spec.json", _GENERIC_SPEC_UNKNOWN_CATEGORY),
            ]);

        var result = driver.GetRunResult();

        result.Diagnostics
            .Should().NotContain(d => d.Id == RegistryDiagnosticIds.UnknownCategory);
        result.GeneratedTrees.Should().HaveCount(1);
    }

    [Fact]
    public void Generator_RunTwice_SameInputs_ProducesIdenticalOutput()
    {
        var additionalTexts = new AdditionalText[]
        {
            new InMemoryAdditionalText("error-codes.spec.json", _GENERIC_SPEC),
            new InMemoryAdditionalText("auth-error-codes.spec.json", _AUTH_SPEC),
        };

        var first = RunGenerator(_TARGET_ASSEMBLY, additionalTexts)
            .GetRunResult().GeneratedTrees
            .Select(t => Normalize(t.GetText().ToString()))
            .ToList();

        var second = RunGenerator(_TARGET_ASSEMBLY, additionalTexts)
            .GetRunResult().GeneratedTrees
            .Select(t => Normalize(t.GetText().ToString()))
            .ToList();

        second.Count.Should().Be(first.Count);
        for (var i = 0; i < first.Count; i++)
            second[i].Should().Be(first[i]);
    }

    private static GeneratorDriver RunGenerator(
        string assemblyName, AdditionalText[] additionalTexts)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new RegistryGenerator().AsSourceGenerator();

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts.ToImmutableArray());

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
            CancellationToken cancellationToken = default) => r_text;
    }
}
