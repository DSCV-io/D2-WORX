// -----------------------------------------------------------------------
// <copyright file="KeyCustodianErrorCodesGeneratorTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Linq;
using D2.Edge.KeyCustodian.ErrorCodes.SourceGen;
using D2.Shared.ErrorCodes.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// IIncrementalGenerator integration + diagnostic tests for the
/// KeyCustodianErrorCodes SrcGen — drive
/// <see cref="ErrorCodesGenerator"/> via a synthetic
/// <see cref="CSharpGeneratorDriver"/> rather than the build pipeline. Each
/// adversarial planted-bad-spec asserts the matching per-catalog (<c>D2KEC*</c>)
/// or engine-neutral (<c>D2ERC*</c>) diagnostic fires loudly.
/// </summary>
public sealed class KeyCustodianErrorCodesGeneratorTests
{
    private const string _ASSEMBLY = "D2.Edge.KeyCustodian.Domain";
    private const string _SPEC_NAME = "keycustodian-error-codes.spec.json";

    private const string _SAMPLE_SPEC = """
    {
      "errorCodes": [
        {
          "code": "KEYCUSTODIAN_TEST",
          "httpStatus": 400,
          "category": "validation_failure",
          "userMessageKey": "TK.Keycustodian.Validation.SOAK_NOT_ELAPSED",
          "factoryName": "Test",
          "factoryShape": "standard",
          "doc": "Test entry."
        }
      ]
    }
    """;

    private const string _EN_US =
        """{ "keycustodian_validation_SOAK_NOT_ELAPSED": "Soak not elapsed." }""";

    [Fact]
    public void Generator_TargetAssemblyWithSpec_EmitsConstantsAndBothFailuresClasses()
    {
        var driver = RunGenerator(_ASSEMBLY, _SAMPLE_SPEC, _EN_US);

        var result = driver.GetRunResult();

        // The keycustodian catalog (FactoryHost.Domain) emits the constants file
        // plus BOTH the non-generic KeyCustodianFailures class AND the generic
        // KeyCustodianFailures<T> twin (in a distinct sibling file so the
        // existing KeyCustodianFailures.g.cs stays byte-identical).
        result.GeneratedTrees.Should().HaveCount(3);
        var fileNames = result.GeneratedTrees
            .Select(t => Path.GetFileName(t.FilePath))
            .OrderBy(n => n)
            .ToList();
        fileNames.Should().BeEquivalentTo(
            new[]
            {
                "KeyCustodianErrorCodes.g.cs",
                "KeyCustodianFailures.Generic.g.cs",
                "KeyCustodianFailures.g.cs",
            });
    }

    [Fact]
    public void Generator_NonTargetAssembly_EmitsNothing()
    {
        var driver = RunGenerator("Some.Other.Assembly", _SAMPLE_SPEC, _EN_US);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_TargetAssemblyButNoSpec_EmitsNothing()
    {
        // No AdditionalText supplied — generator silently no-ops (no spec).
        var driver = RunGenerator(_ASSEMBLY, specJson: null, enUsJson: null);

        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_CleanSpec_ProducesNoDiagnostics()
    {
        var driver = RunGenerator(_ASSEMBLY, _SAMPLE_SPEC, _EN_US);

        driver.GetRunResult().Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Generator_MalformedSpec_EmitsMalformedSpecDiagnostic()
    {
        var driver = RunGenerator(_ASSEMBLY, "{not valid", _EN_US);

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Generator_NonKeyCustodianPrefixedCode_FiresDomainPrefixViolation()
    {
        const string spec = """
        {
          "errorCodes": [
            {
              "code": "SOAK_NOT_ELAPSED",
              "httpStatus": 400,
              "category": "validation_failure",
              "userMessageKey": "TK.Keycustodian.Validation.SOAK_NOT_ELAPSED",
              "factoryName": "X",
              "factoryShape": "standard",
              "doc": "X."
            }
          ]
        }
        """;

        var driver = RunGenerator(_ASSEMBLY, spec, _EN_US);

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == EngineDiagnosticIds.DomainPrefixViolation);
    }

    [Fact]
    public void Generator_DuplicateCode_FiresDuplicateCode()
    {
        const string spec = """
        {
          "errorCodes": [
            {
              "code": "KEYCUSTODIAN_DUP",
              "httpStatus": 400,
              "category": "validation_failure",
              "userMessageKey": "TK.Keycustodian.Validation.SOAK_NOT_ELAPSED",
              "factoryName": "DupA",
              "factoryShape": "standard",
              "doc": "A."
            },
            {
              "code": "KEYCUSTODIAN_DUP",
              "httpStatus": 400,
              "category": "validation_failure",
              "userMessageKey": "TK.Keycustodian.Validation.SOAK_NOT_ELAPSED",
              "factoryName": "DupB",
              "factoryShape": "standard",
              "doc": "B."
            }
          ]
        }
        """;

        var driver = RunGenerator(_ASSEMBLY, spec, _EN_US);

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.DuplicateCode);
    }

    [Fact]
    public void Generator_DuplicateFactoryName_FiresDuplicateFactoryName()
    {
        const string spec = """
        {
          "errorCodes": [
            {
              "code": "KEYCUSTODIAN_A",
              "httpStatus": 400,
              "category": "validation_failure",
              "userMessageKey": "TK.Keycustodian.Validation.SOAK_NOT_ELAPSED",
              "factoryName": "SameName",
              "factoryShape": "standard",
              "doc": "A."
            },
            {
              "code": "KEYCUSTODIAN_B",
              "httpStatus": 400,
              "category": "validation_failure",
              "userMessageKey": "TK.Keycustodian.Validation.SOAK_NOT_ELAPSED",
              "factoryName": "SameName",
              "factoryShape": "standard",
              "doc": "B."
            }
          ]
        }
        """;

        var driver = RunGenerator(_ASSEMBLY, spec, _EN_US);

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.DuplicateFactoryName);
    }

    [Fact]
    public void Generator_UnsupportedHttpStatus_FiresInvalidHttpStatus()
    {
        // 599 is not in the engine's supported status set → D2KEC005.
        const string spec = """
        {
          "errorCodes": [
            {
              "code": "KEYCUSTODIAN_X",
              "httpStatus": 599,
              "category": "validation_failure",
              "userMessageKey": "TK.Keycustodian.Validation.SOAK_NOT_ELAPSED",
              "factoryName": "X",
              "factoryShape": "standard",
              "doc": "X."
            }
          ]
        }
        """;

        var driver = RunGenerator(_ASSEMBLY, spec, _EN_US);

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.InvalidHttpStatus);
    }

    [Fact]
    public void Generator_UnknownCategory_FiresUnknownCategoryEnum()
    {
        const string spec = """
        {
          "errorCodes": [
            {
              "code": "KEYCUSTODIAN_X",
              "httpStatus": 400,
              "category": "not_a_real_category",
              "userMessageKey": "TK.Keycustodian.Validation.SOAK_NOT_ELAPSED",
              "factoryName": "X",
              "factoryShape": "standard",
              "doc": "X."
            }
          ]
        }
        """;

        var driver = RunGenerator(_ASSEMBLY, spec, _EN_US);

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == DiagnosticIds.UnknownCategoryEnum);
    }

    [Fact]
    public void Generator_UserMessageKeyMissingFromEnUs_FiresTkKeyNotFound()
    {
        const string spec = """
        {
          "errorCodes": [
            {
              "code": "KEYCUSTODIAN_X",
              "httpStatus": 400,
              "category": "validation_failure",
              "userMessageKey": "TK.Keycustodian.Validation.DOES_NOT_EXIST",
              "factoryName": "X",
              "factoryShape": "standard",
              "doc": "X."
            }
          ]
        }
        """;

        var driver = RunGenerator(_ASSEMBLY, spec, _EN_US);

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == EngineDiagnosticIds.TkKeyNotFound);
    }

    [Fact]
    public void Generator_RemovedFactoryShape_FiresUnsupportedFactoryShape()
    {
        // The delegating per-domain emitter implements only the universal standard
        // shape + none. The retired "validation" value must fail loudly (D2ERC003)
        // — the non-vacuous proof that standard (not the removed shapes) is the
        // correct KC shape.
        const string spec = """
        {
          "errorCodes": [
            {
              "code": "KEYCUSTODIAN_X",
              "httpStatus": 400,
              "category": "validation_failure",
              "userMessageKey": "TK.Keycustodian.Validation.SOAK_NOT_ELAPSED",
              "factoryName": "X",
              "factoryShape": "validation",
              "doc": "X."
            }
          ]
        }
        """;

        var driver = RunGenerator(_ASSEMBLY, spec, _EN_US);

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == EngineDiagnosticIds.UnsupportedFactoryShape);
    }

    [Fact]
    public void Generator_RunTwice_SameInputs_ProducesIdenticalOutput()
    {
        var first = RunGenerator(_ASSEMBLY, _SAMPLE_SPEC, _EN_US)
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();
        var second = RunGenerator(_ASSEMBLY, _SAMPLE_SPEC, _EN_US)
            .GetRunResult().GeneratedTrees
            .OrderBy(t => t.FilePath)
            .Select(t => t.ToString())
            .ToList();

        second.Count.Should().Be(first.Count);
        for (var i = 0; i < first.Count; i++)
            Normalize(second[i]).Should().Be(Normalize(first[i]));
    }

    private static GeneratorDriver RunGenerator(
        string assemblyName, string? specJson, string? enUsJson)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ErrorCodesGenerator().AsSourceGenerator();

        var additionalTexts = ImmutableArray.CreateBuilder<AdditionalText>();
        if (specJson is not null)
            additionalTexts.Add(new InMemoryAdditionalText(_SPEC_NAME, specJson));

        if (enUsJson is not null)
            additionalTexts.Add(new InMemoryAdditionalText("messages/en-US.json", enUsJson));

        // The real error-category spec surfaces the closed category set so the
        // spec-derived category-membership check (D2KEC002) is exercised (mirrors
        // the build's AdditionalFiles); an unknown category fires loudly rather
        // than degrading to a no-op.
        additionalTexts.Add(new InMemoryAdditionalText(
            "error-category.spec.json",
            File.ReadAllText(TestPaths.ErrorCategorySpec())));

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts.ToImmutable());

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
