// -----------------------------------------------------------------------
// <copyright file="ErrorCodesEngineTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.SourceGen;

using System.Collections.Immutable;
using AwesomeAssertions;
using D2.Shared.Auth.ErrorCodes.SourceGen;
using D2.Shared.ErrorCodes.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

/// <summary>
/// Engine-level tests for the catalog-neutral pieces of the unified
/// error-codes engine: the inverse-<c>KeyDecomposer</c> TK transform, the
/// <c>factoryShape</c>-driven failures signature, and the
/// userMessageKey → en-US.json TK-existence diagnostic (<c>D2ERC002</c>)
/// surfaced through the auth generator with a synthetic en-US.json key set.
/// </summary>
public sealed class ErrorCodesEngineTests
{
    private const string _AUTH_ASSEMBLY = "D2.Shared.Auth";
    private const string _AUTH_SPEC_NAME = "auth-error-codes.spec.json";

    [Theory]
    [InlineData("TK.Auth.Errors.UNAUTHORIZED", "auth_errors_UNAUTHORIZED")]
    [InlineData("TK.Auth.Errors.TEMPORARILY_UNAVAILABLE", "auth_errors_TEMPORARILY_UNAVAILABLE")]
    [InlineData("TK.Common.Errors.NOT_FOUND", "common_errors_NOT_FOUND")]
    [InlineData("TK.Geo.Errors.SUBDIVISION_NOT_FOUND", "geo_errors_SUBDIVISION_NOT_FOUND")]
    public void TkKeyTransform_RoundTripsInverseOfKeyDecomposer(string tkPath, string expectedSnake)
    {
        TkKeyTransform.ToSnakeKey(tkPath).Should().Be(expectedSnake);
    }

    [Theory]
    [InlineData("auth_errors_UNAUTHORIZED")]
    [InlineData("not_a_tk_path")]
    [InlineData("TK.OnlyTwo.Segments")]
    [InlineData("TK.Four.Dot.Segments.Here")]
    public void TkKeyTransform_NonConformingInput_ReturnsNull(string input)
    {
        TkKeyTransform.ToSnakeKey(input).Should().BeNull();
    }

    [Fact]
    public void TkKeyTransform_NullInput_ReturnsNull()
    {
        TkKeyTransform.ToSnakeKey(null).Should().BeNull();
    }

    [Fact]
    public void TkKeyTransform_EmptyString_ReturnsNull()
    {
        TkKeyTransform.ToSnakeKey(string.Empty).Should().BeNull();
    }

    [Fact]
    public void TkKeyTransform_ZeroLengthDomain_ReturnsNull()
    {
        // "TK..Errors.X" — empty domain segment
        TkKeyTransform.ToSnakeKey("TK..Errors.X").Should().BeNull();
    }

    [Fact]
    public void TkKeyTransform_ZeroLengthCategory_ReturnsNull()
    {
        // "TK.Auth..X" — empty category segment
        TkKeyTransform.ToSnakeKey("TK.Auth..X").Should().BeNull();
    }

    [Fact]
    public void TkKeyTransform_MultiCharDomain_LowercasesOnlyFirstChar()
    {
        // "TK.KeyCustodian.Errors.FOO" — first char 'K' lowercased; rest unchanged.
        TkKeyTransform.ToSnakeKey("TK.KeyCustodian.Errors.FOO")
            .Should().Be("keyCustodian_errors_FOO");
    }

    [Fact]
    public void FactoryShapeStandard_EmitsMessagesAndErrorCodeNamedArguments()
    {
        // The universal standard shape → optional messages override defaulting to
        // the spec TK, threaded as (messages: messages, errorCode:).
        var spec = MakeSpec(new ErrorCodeEntry(
            Code: "AUTH_X",
            HttpStatus: 401,
            Doc: "X doc.",
            Category: "validation_failure",
            UserMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
            FactoryName: "X",
            FactoryShape: "standard"));

        var result = FailuresEmitter.Emit(spec, ErrorCodesGenerator.Config);

        result.GeneratedSource.Should().Contain("messages ??= [TK.Auth.Errors.UNAUTHORIZED];");
        result.GeneratedSource.Should().Contain("messages: messages");
        result.GeneratedSource.Should().Contain("errorCode: AuthErrorCodes.AUTH_X");
    }

    [Fact]
    public void Generator_UserMessageKeyMissingFromEnUs_FiresTkKeyNotFound()
    {
        const string spec = """
        {
          "errorCodes": [
            {
              "code": "AUTH_X",
              "httpStatus": 401,
              "category": "validation_failure",
              "userMessageKey": "TK.Auth.Errors.DOES_NOT_EXIST",
              "factoryName": "X",
              "factoryShape": "standard",
              "doc": "X."
            }
          ]
        }
        """;
        const string enUs = """{ "auth_errors_UNAUTHORIZED": "Authentication required." }""";

        var driver = RunGenerator(spec, enUs);

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == EngineDiagnosticIds.TkKeyNotFound);
    }

    [Fact]
    public void Generator_UserMessageKeyPresentInEnUs_DoesNotFireTkKeyNotFound()
    {
        const string spec = """
        {
          "errorCodes": [
            {
              "code": "AUTH_X",
              "httpStatus": 401,
              "category": "validation_failure",
              "userMessageKey": "TK.Auth.Errors.UNAUTHORIZED",
              "factoryName": "X",
              "factoryShape": "standard",
              "doc": "X."
            }
          ]
        }
        """;
        const string enUs = """{ "auth_errors_UNAUTHORIZED": "Authentication required." }""";

        var driver = RunGenerator(spec, enUs);

        driver.GetRunResult().Diagnostics.Should()
            .NotContain(d => d.Id == EngineDiagnosticIds.TkKeyNotFound);
    }

    [Fact]
    public void Generator_NonAuthPrefixedCode_FiresDomainPrefixViolation()
    {
        const string spec = """
        {
          "errorCodes": [
            {
              "code": "BEARER_MISSING",
              "httpStatus": 401,
              "category": "validation_failure",
              "userMessageKey": "TK.Auth.Errors.UNAUTHORIZED",
              "factoryName": "X",
              "factoryShape": "standard",
              "doc": "X."
            }
          ]
        }
        """;
        const string enUs = """{ "auth_errors_UNAUTHORIZED": "Authentication required." }""";

        var driver = RunGenerator(spec, enUs);

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == EngineDiagnosticIds.DomainPrefixViolation);
    }

    [Fact]
    public void Generator_UnsupportedFactoryShape_FiresUnsupportedFactoryShape()
    {
        // The schema constrains factoryShape to {standard, none}; a hand-malformed
        // spec carrying a removed / unknown value (here the retired "validation")
        // must fail loudly on the delegating path (D2ERC003).
        const string spec = """
        {
          "errorCodes": [
            {
              "code": "AUTH_X",
              "httpStatus": 401,
              "category": "validation_failure",
              "userMessageKey": "TK.Auth.Errors.UNAUTHORIZED",
              "factoryName": "X",
              "factoryShape": "validation",
              "doc": "X."
            }
          ]
        }
        """;
        const string enUs = """{ "auth_errors_UNAUTHORIZED": "Authentication required." }""";

        var driver = RunGenerator(spec, enUs);

        driver.GetRunResult().Diagnostics.Should()
            .Contain(d => d.Id == EngineDiagnosticIds.UnsupportedFactoryShape);
    }

    [Fact]
    public void Generator_FactoryShapeNone_EmitsConstantButNoFactory()
    {
        // factoryShape "none" → constant + boolean only; NO factory method emitted.
        var spec = MakeSpec(new ErrorCodeEntry(
            Code: "AUTH_X",
            HttpStatus: 401,
            Doc: "X doc.",
            Category: "validation_failure",
            UserMessageKey: "TK.Auth.Errors.UNAUTHORIZED",
            FactoryName: "X",
            FactoryShape: "none"));

        var constantsResult = ConstantsEmitter.Emit(
            spec, ErrorCodesGenerator.Config, ImmutableHashSet<string>.Empty);
        var failuresResult = FailuresEmitter.Emit(spec, ErrorCodesGenerator.Config);

        // The constant is emitted on the constants side.
        constantsResult.GeneratedSource.Should().Contain("AUTH_X");

        // No D2ERC003 fires for "none".
        failuresResult.Diagnostics.Should()
            .NotContain(d => d.DescriptorId == EngineDiagnosticIds.UnsupportedFactoryShape);

        // No factory body is emitted (no method declaration for AUTH_X).
        failuresResult.GeneratedSource.Should().NotContain(
            "public static D2Result X(IReadOnlyList<TKMessage>? messages = null)");
    }

    [Fact]
    public void MessageKeySet_Parse_MalformedJson_ReturnsEmpty()
    {
        var result = MessageKeySet.Parse("{not valid json");

        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void MessageKeySet_Parse_NonObjectRoot_ReturnsEmpty()
    {
        var result = MessageKeySet.Parse("[\"a\",\"b\"]");

        result.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void MessageKeySet_Parse_ValidObject_ContainsKeys()
    {
        var result = MessageKeySet.Parse(
            """{ "auth_errors_UNAUTHORIZED": "v", "common_errors_NOT_FOUND": "v2" }""");

        result.IsEmpty.Should().BeFalse();
        result.Contains("auth_errors_UNAUTHORIZED").Should().BeTrue();
        result.Contains("common_errors_NOT_FOUND").Should().BeTrue();
        result.Contains("missing_key").Should().BeFalse();
    }

    [Fact]
    public void Generator_AbsentEnUs_DoesNotFireTkKeyNotFound()
    {
        // When en-US.json is absent from AdditionalFiles the engine skips the
        // TK cross-check (no key set → no false positives).
        const string spec = """
        {
          "errorCodes": [
            {
              "code": "AUTH_X",
              "httpStatus": 401,
              "category": "validation_failure",
              "userMessageKey": "TK.Auth.Errors.DOES_NOT_EXIST",
              "factoryName": "X",
              "factoryShape": "standard",
              "doc": "X."
            }
          ]
        }
        """;

        var compilation = CSharpCompilation.Create(
            assemblyName: _AUTH_ASSEMBLY,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ErrorCodesGenerator().AsSourceGenerator();

        // Only the spec AdditionalText — no en-US.json.
        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(_AUTH_SPEC_NAME, spec));

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts);

        var result = driver.RunGenerators(compilation);

        result.GetRunResult().Diagnostics.Should()
            .NotContain(d => d.Id == EngineDiagnosticIds.TkKeyNotFound);
    }

    [Fact]
    public void Generator_MalformedEnUs_DoesNotFireTkKeyNotFound()
    {
        // When en-US.json is malformed the engine falls back to an empty key set
        // and skips the cross-check — no false diagnostic fired.
        const string spec = """
        {
          "errorCodes": [
            {
              "code": "AUTH_X",
              "httpStatus": 401,
              "category": "validation_failure",
              "userMessageKey": "TK.Auth.Errors.DOES_NOT_EXIST",
              "factoryName": "X",
              "factoryShape": "standard",
              "doc": "X."
            }
          ]
        }
        """;

        var driver = RunGenerator(spec, "{malformed-json");

        driver.GetRunResult().Diagnostics.Should()
            .NotContain(d => d.Id == EngineDiagnosticIds.TkKeyNotFound);
    }

    private static GeneratorDriver RunGenerator(string specJson, string enUsJson)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: _AUTH_ASSEMBLY,
            syntaxTrees: [],
            references:
            [
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            ],
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new ErrorCodesGenerator().AsSourceGenerator();

        var additionalTexts = ImmutableArray.Create<AdditionalText>(
            new InMemoryAdditionalText(_AUTH_SPEC_NAME, specJson),
            new InMemoryAdditionalText("messages/en-US.json", enUsJson));

        var driver = CSharpGeneratorDriver.Create(
            generators: [generator],
            additionalTexts: additionalTexts);

        return driver.RunGenerators(compilation);
    }

    private static ErrorCodesSpec MakeSpec(params ErrorCodeEntry[] entries) =>
        new(entries.ToImmutableArray());

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
