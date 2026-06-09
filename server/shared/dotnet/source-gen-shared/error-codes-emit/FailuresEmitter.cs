// -----------------------------------------------------------------------
// <copyright file="FailuresEmitter.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ErrorCodes.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using D2.Shared.SourceGen;

/// <summary>
/// Shared logic for emitting a per-domain catalog's DELEGATING failure
/// classes (<see cref="FactoryHost.Domain"/>) from a parsed
/// <see cref="ErrorCodesSpec"/> + a <see cref="CatalogConfig"/>. Emits BOTH
/// the non-generic <c>&lt;Domain&gt;Failures</c> class (→ <c>D2Result</c>) and
/// the generic <c>&lt;Domain&gt;Failures&lt;T&gt;</c> class (→
/// <c>D2Result&lt;T&gt;</c>), both carrying identical method names. Stateless
/// and unit-testable in isolation. Validation happens upstream in
/// <see cref="ConstantsEmitter.Validate"/>; this emitter consumes the same
/// valid-entry walk so the constants + failures files agree.
/// </summary>
/// <remarks>
/// <para>
/// The base <c>D2Result</c> factory is selected by <c>httpStatus</c>
/// (401 → <c>Unauthorized</c>, 503 → <c>ServiceUnavailable</c>, …) per the
/// canonical status→factory delegation map. The call SIGNATURE is driven by
/// <c>factoryShape</c> (<c>with_error_code</c> → <c>(messages:, errorCode:)</c>;
/// <c>none</c> → constant + boolean only, no factory emitted).
/// </para>
/// <para>
/// Both the non-generic <c>&lt;Domain&gt;Failures</c> method and its generic
/// <c>&lt;Domain&gt;Failures&lt;T&gt;</c> twin are emitted for every
/// factory-bearing entry — the non-generic delegates to <c>D2Result.X(...)</c>,
/// the generic delegates to the typed <c>D2Result&lt;T&gt;.X(...)</c> base
/// factory (the constructing <c>&lt;TData&gt;</c> twin the generic catalog's
/// base-mode pass emits). The two classes are distinct types (arity differs),
/// exactly as <c>D2Result</c> / <c>D2Result&lt;TData&gt;</c> coexist.
/// </para>
/// <para>
/// The delegating path emits only the <c>with_error_code</c> shape (auth's
/// entire set). <c>standard</c> and <c>validation</c> delegating bodies are not
/// yet emitted; an entry with those shapes produces a
/// <see cref="EngineDiagnosticIds.UnsupportedFactoryShape"/> error. The generic
/// constructing catalog (<see cref="FactoryHost.Base"/>) implements all four
/// shapes; this guard is the delegating-path equivalent.
/// </para>
/// </remarks>
internal static class FailuresEmitter
{
    private const int _HTTP_SERVICE_UNAVAILABLE = 503;
    private const string _SHAPE_WITH_ERROR_CODE = "with_error_code";
    private const string _SHAPE_NONE = "none";

    /// <summary>
    /// Emits the non-generic <c>&lt;Domain&gt;Failures</c> class source.
    /// Reports <see cref="EngineDiagnosticIds.UnsupportedFactoryShape"/> for any
    /// entry whose <c>factoryShape</c> is not yet emitted on the delegating path
    /// (<c>standard</c> or <c>validation</c>). Entries with shape <c>none</c>
    /// are silently skipped.
    /// </summary>
    /// <param name="spec">Parsed error-codes spec.</param>
    /// <param name="config">The catalog configuration.</param>
    /// <returns>Generated source + any diagnostics.</returns>
    public static EmitResult Emit(ErrorCodesSpec spec, CatalogConfig config)
    {
        var discard = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var validEntries = ConstantsEmitter.Validate(spec, config, discard);

        var diagnostics = CollectUnsupportedShapeDiagnostics(validEntries);
        var source = EmitSource(validEntries, config, generic: false);
        return new EmitResult(source, diagnostics);
    }

    /// <summary>
    /// Emits the generic <c>&lt;Domain&gt;Failures&lt;T&gt;</c> class source —
    /// the typed twin of <see cref="Emit"/>. Same method names, delegating to
    /// the typed <c>D2Result&lt;T&gt;</c> base factories. Diagnostics are
    /// suppressed here (the non-generic <see cref="Emit"/> already surfaced any
    /// unsupported-shape error for the same entry set — emitting it twice would
    /// double-report).
    /// </summary>
    /// <param name="spec">Parsed error-codes spec.</param>
    /// <param name="config">The catalog configuration.</param>
    /// <returns>Generated source.</returns>
    public static EmitResult EmitGeneric(ErrorCodesSpec spec, CatalogConfig config)
    {
        var discard = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var validEntries = ConstantsEmitter.Validate(spec, config, discard);
        var source = EmitSource(validEntries, config, generic: true);
        return new EmitResult(source, ImmutableArray<EmitDiagnostic>.Empty);
    }

    /// <summary>
    /// The canonical base <c>D2Result</c> delegation factory for an HTTP
    /// status. Auth uses only 401/503 today; the map covers the full
    /// per-domain delegation set so future per-domain catalogs reuse it.
    /// </summary>
    /// <param name="httpStatus">The HTTP status from the spec entry.</param>
    /// <returns>The <c>D2Result</c> factory method name to delegate to.</returns>
    internal static string BaseFactory(int httpStatus) => httpStatus switch
    {
        400 => "ValidationFailed",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "NotFound",
        409 => "Conflict",
        413 => "PayloadTooLarge",
        429 => "TooManyRequests",
        500 => "UnhandledException",
        503 => "ServiceUnavailable",
        _ => "UnhandledException",
    };

    private static ImmutableArray<EmitDiagnostic> CollectUnsupportedShapeDiagnostics(
        List<ErrorCodeEntry> entries)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        foreach (var entry in entries)
        {
            var shape = entry.FactoryShape;
            if (shape is not null
                && shape != _SHAPE_WITH_ERROR_CODE
                && shape != _SHAPE_NONE)
                diagnostics.Add(EngineDiagnostics.UnsupportedFactoryShape(shape));
        }

        return diagnostics.ToImmutable();
    }

    private static string EmitSource(List<ErrorCodeEntry> entries, CatalogConfig config, bool generic)
    {
        var banner = config.FailuresBanner!;
        var summary = generic ? config.GenericFailuresSummary! : config.FailuresSummary!;
        var classDecl = generic
            ? $"public static class {config.FailuresClassName}<T>"
            : $"public static class {config.FailuresClassName}";

        var sb = new StringBuilder();
        EmitBlock(sb, banner);
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using D2.Shared.ErrorCodes.Category;");
        sb.AppendLine("using D2.Shared.I18n;");
        sb.AppendLine("using D2.Shared.Result;");
        sb.AppendLine();
        sb.AppendLine($"namespace {config.RootNamespace};");
        sb.AppendLine();
        EmitBlock(sb, summary);
        sb.AppendLine(classDecl);
        sb.AppendLine("{");

        foreach (var entry in entries)
        {
            // factoryShape "none" → constant + boolean only; no factory emitted.
            // factoryShape "standard"/"validation" → unsupported on the delegating
            // path (D2ERC003 reported in Emit); skip to avoid malformed source.
            if (entry.FactoryShape != _SHAPE_WITH_ERROR_CODE)
                continue;

            if (generic)
                EmitGenericFactory(sb, entry, config);
            else
                EmitFactory(sb, entry, config);

            sb.AppendLine();

            // The non-generic class additionally carries the legacy typed <T>
            // overload on 503 entries (the non-generic factory surface) so existing
            // callers of e.g. AuthFailures.JwksUnavailable<T>() keep compiling.
            // The generic <Domain>Failures<T> class needs no such per-entry
            // overload — the class itself is generic.
            if (!generic && entry.HttpStatus == _HTTP_SERVICE_UNAVAILABLE)
            {
                EmitTypedFactory(sb, entry, config);
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitFactory(StringBuilder sb, ErrorCodeEntry entry, CatalogConfig config)
    {
        var baseFactory = BaseFactory(entry.HttpStatus);
        var categoryMember = BaseFactoriesEmitter.CategoryMemberName(entry.Category!);
        sb.AppendLine($"    /// <summary>{EscapeXmlDoc(entry.Doc)}</summary>");
        sb.AppendLine(
            "    /// <returns>A pre-built <see cref=\"D2Result\"/> failure.</returns>");
        sb.AppendLine($"    public static D2Result {entry.FactoryName}() =>");
        sb.AppendLine($"        D2Result.{baseFactory}(");
        sb.AppendLine($"            messages: [{entry.UserMessageKey}],");
        sb.AppendLine(
            $"            errorCode: {config.ConstantsClassName}.{entry.Code},");
        sb.AppendLine(
            $"            category: ErrorCategory.{categoryMember});");
    }

    private static void EmitGenericFactory(
        StringBuilder sb, ErrorCodeEntry entry, CatalogConfig config)
    {
        var baseFactory = BaseFactory(entry.HttpStatus);
        var categoryMember = BaseFactoriesEmitter.CategoryMemberName(entry.Category!);
        sb.AppendLine($"    /// <summary>{EscapeXmlDoc(entry.Doc)} Typed result.</summary>");
        sb.AppendLine(
            "    /// <returns>A pre-built typed <see cref=\"D2Result{T}\"/> failure.</returns>");
        sb.AppendLine($"    public static D2Result<T> {entry.FactoryName}() =>");
        sb.AppendLine($"        D2Result<T>.{baseFactory}(");
        sb.AppendLine($"            messages: [{entry.UserMessageKey}],");
        sb.AppendLine(
            $"            errorCode: {config.ConstantsClassName}.{entry.Code},");
        sb.AppendLine(
            $"            category: ErrorCategory.{categoryMember});");
    }

    private static void EmitTypedFactory(
        StringBuilder sb, ErrorCodeEntry entry, CatalogConfig config)
    {
        var baseFactory = BaseFactory(entry.HttpStatus);
        var categoryMember = BaseFactoriesEmitter.CategoryMemberName(entry.Category!);
        sb.AppendLine($"    /// <summary>{EscapeXmlDoc(entry.Doc)} Typed overload.</summary>");
        sb.AppendLine(
            "    /// <typeparam name=\"T\">The payload type the caller would have returned on "
            + "success.</typeparam>");
        sb.AppendLine(
            "    /// <returns>A pre-built typed <see cref=\"D2Result{T}\"/> failure.</returns>");
        sb.AppendLine($"    public static D2Result<T> {entry.FactoryName}<T>() =>");
        sb.AppendLine($"        D2Result<T>.{baseFactory}(");
        sb.AppendLine($"            messages: [{entry.UserMessageKey}],");
        sb.AppendLine(
            $"            errorCode: {config.ConstantsClassName}.{entry.Code},");
        sb.AppendLine(
            $"            category: ErrorCategory.{categoryMember});");
    }

    /// <summary>
    /// Appends a newline-delimited config block one line at a time via
    /// <see cref="StringBuilder.AppendLine()"/> so every emitted line uses the
    /// same line terminator as the rest of the generated source.
    /// </summary>
    private static void EmitBlock(StringBuilder sb, string block)
    {
        var lines = block.Split('\n');
        var count = lines.Length;
        if (count > 0 && lines[count - 1].Length == 0)
            count--;

        for (var i = 0; i < count; i++)
            sb.AppendLine(lines[i]);
    }

    private static string EscapeXmlDoc(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
