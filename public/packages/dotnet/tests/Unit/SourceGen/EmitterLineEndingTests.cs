// -----------------------------------------------------------------------
// <copyright file="EmitterLineEndingTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

extern alias ResultErrorCodesSourceGen;
extern alias RegistrySourceGen;

namespace DcsvIo.D2.Tests.Unit.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.I18n.SourceGen;
using Xunit;
using BaseFactoriesEmitter =
    ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.BaseFactoriesEmitter;
using CatalogConfig = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.CatalogConfig;
using ConstantsEmitter = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.ConstantsEmitter;
using ErrorCodeEntry = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.ErrorCodeEntry;
using ErrorCodesGenerator =
    ResultErrorCodesSourceGen::DcsvIo.D2.ResultErrorCodes.SourceGen.ErrorCodesGenerator;
using ErrorCodesSpec = ResultErrorCodesSourceGen::DcsvIo.D2.ErrorCodes.SourceGen.ErrorCodesSpec;
using RegistryEmitter = RegistrySourceGen::DcsvIo.D2.ErrorCodes.Registry.SourceGen.RegistryEmitter;
using RegistrySpecEntry =
    RegistrySourceGen::DcsvIo.D2.ErrorCodes.Registry.SourceGen.RegistrySpecEntry;

/// <summary>
/// Drift guard for the LF line-ending discipline every D² Roslyn emitter must
/// hold. Each emitter builds its source with
/// <see cref="System.Text.StringBuilder.AppendLine()"/>, which appends
/// <see cref="System.Environment.NewLine"/> — CRLF on Windows. The repo policy
/// is LF everywhere (<c>.gitattributes</c> <c>eol=lf</c>, <c>.editorconfig</c>
/// <c>end_of_line = lf</c>) and the committed <c>EmitCompilerGeneratedFiles</c>
/// output is LF, so every emitter routes its returned source through
/// <c>SourceTextExt.LfNormalized()</c>. These tests assert the returned source
/// carries no bare <c>\r</c>.
/// </summary>
/// <remarks>
/// <para>
/// NON-VACUITY: each fixture below contains multiple lines, so every emitter's
/// builder runs many <c>AppendLine</c> calls. On the Windows build host
/// <c>AppendLine</c> emits <c>\r\n</c>; without the <c>LfNormalized()</c> call
/// at the emitter's source funnel the returned string WOULD contain <c>\r</c>
/// and these assertions WOULD fail. (The assertion is also meaningful on a
/// Linux host where <c>AppendLine</c> already emits <c>\n</c> — it pins the
/// invariant so a future emitter that hard-codes <c>\r\n</c> is caught
/// everywhere.) The coverage spans the three distinct emitter return shapes:
/// the i18n <c>EmitResult</c> path (<see cref="TKEmitter"/>), the shared
/// error-codes engine that backs the constructing <c>D2Result</c> factories +
/// the per-domain <c>&lt;Domain&gt;Failures.g.cs</c> fleet
/// (<see cref="ConstantsEmitter"/> / <see cref="BaseFactoriesEmitter"/>), and
/// the raw-string registry path (<see cref="RegistryEmitter"/>).
/// </para>
/// </remarks>
public sealed class EmitterLineEndingTests
{
    private const string _MULTI_KEY_JSON =
        @"{""common_errors_NOT_FOUND"": ""Not found."", ""auth_errors_UNAUTHORIZED"": ""No.""}";

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "ReSharper",
        "CollectionNeverUpdated.Local",
        Justification = "Intentionally empty fixture; TKEmitter consumes it read-only.")]
    private static readonly Dictionary<string, string> sr_noOtherLocales = new();

    private static CatalogConfig ErrorCodesConfig => ErrorCodesGenerator.Config;

    [Fact]
    public void TkEmitter_GeneratedSource_ContainsNoCarriageReturn()
    {
        var result = TKEmitter.Emit(_MULTI_KEY_JSON, sr_noOtherLocales);

        // Guard against a vacuous pass: the source must be non-trivial (the
        // emitter ran its AppendLine-heavy body), so a NotContain over an empty
        // string can't masquerade as a pass.
        result.GeneratedSource.Should().Contain("public static partial class TK");
        result.GeneratedSource.Should().NotContain("\r");
    }

    [Fact]
    public void ConstantsEmitter_GeneratedSource_ContainsNoCarriageReturn()
    {
        var spec = MakeSpec(new ErrorCodeEntry("X_THING", 404, "X thing doc."));

        var result = ConstantsEmitter.Emit(spec, ErrorCodesConfig, ImmutableHashSet<string>.Empty);

        result.GeneratedSource.Should().Contain("public static class ErrorCodes");
        result.GeneratedSource.Should().NotContain("\r");
    }

    [Fact]
    public void BaseFactoriesEmitter_AllSources_ContainNoCarriageReturn()
    {
        var spec = MakeSpec(new ErrorCodeEntry(
            Code: "NOT_FOUND",
            HttpStatus: 404,
            Doc: "Doc.",
            Category: "not_found",
            UserMessageKey: "TK.Common.Errors.NOT_FOUND",
            FactoryName: "NotFound",
            FactoryShape: "standard"));

        var factories = BaseFactoriesEmitter.EmitFactories(spec, ErrorCodesConfig).GeneratedSource;
        var generic = BaseFactoriesEmitter.EmitGenericFactories(spec, ErrorCodesConfig)
            .GeneratedSource;
        var booleans = BaseFactoriesEmitter.EmitBooleans(spec, ErrorCodesConfig).GeneratedSource;

        factories.Should().Contain("public partial class D2Result");
        factories.Should().NotContain("\r");
        generic.Should().NotContain("\r");
        booleans.Should().NotContain("\r");
    }

    [Fact]
    public void RegistryEmitter_GeneratedSource_ContainsNoCarriageReturn()
    {
        var entries = new List<RegistrySpecEntry>
        {
            new(
                Code: "NOT_FOUND",
                HttpStatus: 404,
                Category: "not_found",
                UserMessageKey: "TK.Common.Errors.NOT_FOUND",
                FactoryName: "NotFound",
                FactoryShape: "standard",
                Doc: "Doc.",
                Domain: "common",
                SpecFileName: "common-error-codes.spec.json"),
        };

        var source = RegistryEmitter.Emit(entries);

        source.Should().Contain("public static class ErrorCodeRegistry");
        source.Should().NotContain("\r");
    }

    private static ErrorCodesSpec MakeSpec(params ErrorCodeEntry[] entries) =>
        new(entries.ToImmutableArray());
}
