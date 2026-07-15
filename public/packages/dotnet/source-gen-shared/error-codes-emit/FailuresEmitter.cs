// -----------------------------------------------------------------------
// <copyright file="FailuresEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using DcsvIo.D2.SourceGen;

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
/// <c>factoryShape</c> (<c>standard</c> → an optional
/// <c>IReadOnlyList&lt;TKMessage&gt;? messages = null</c> override that, when
/// omitted, defaults to the spec's <c>userMessageKey</c>; when supplied,
/// replaces it — so a caller can bind the offending argument via
/// <c>TKMessage.With(...)</c>; <c>none</c> → constant + boolean only, no
/// factory emitted).
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
/// The delegating path emits the universal <c>standard</c> shape (every domain
/// catalog's entire set) and skips <c>none</c>. Any other (malformed /
/// unknown) <c>factoryShape</c> value produces a
/// <see cref="EngineDiagnosticIds.UnsupportedFactoryShape"/> error — the schema
/// constrains <c>factoryShape</c> to <c>standard</c> / <c>none</c>, so this
/// guard only fires on a hand-malformed spec.
/// </para>
/// </remarks>
internal static class FailuresEmitter
{
    private const int _HTTP_SERVICE_UNAVAILABLE = 503;
    private const string _SHAPE_STANDARD = "standard";
    private const string _SHAPE_NONE = "none";

    /// <summary>
    /// Emits the non-generic <c>&lt;Domain&gt;Failures</c> class source.
    /// Reports <see cref="EngineDiagnosticIds.UnsupportedFactoryShape"/> for any
    /// entry whose <c>factoryShape</c> is neither the universal <c>standard</c>
    /// shape nor <c>none</c> (a hand-malformed spec). Entries with shape
    /// <c>none</c> are silently skipped.
    /// </summary>
    /// <param name="spec">Parsed error-codes spec.</param>
    /// <param name="config">The catalog configuration.</param>
    /// <returns>Generated source + any diagnostics.</returns>
    public static EmitResult Emit(ErrorCodesSpec spec, CatalogConfig config)
    {
        var discard = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var validEntries = ConstantsEmitter.Validate(
            spec, config, ImmutableHashSet<string>.Empty, discard);

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
        var validEntries = ConstantsEmitter.Validate(
            spec, config, ImmutableHashSet<string>.Empty, discard);
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
                && shape != _SHAPE_STANDARD
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

        // Namespace usings first, then alias last (SA1209 / SA1210). Never interleave.
        sb.AppendLine("using DcsvIo.D2.ErrorCodes.Category;");
        sb.AppendLine($"using {config.MessageKeyUsingNamespace};");
        sb.AppendLine("using DcsvIo.D2.Result;");

        // TKMessage lives on the public Abstractions assembly; keep a type alias
        // when the message-key host namespace is the private ProductTK package.
        if (!string.Equals(
                config.MessageKeyUsingNamespace,
                "DcsvIo.D2.I18n",
                System.StringComparison.Ordinal))
        {
            sb.AppendLine("using TKMessage = DcsvIo.D2.I18n.TKMessage;");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {config.RootNamespace};");
        sb.AppendLine();
        EmitBlock(sb, summary);
        sb.AppendLine(classDecl);
        sb.AppendLine("{");

        foreach (var entry in entries)
        {
            // factoryShape "none" → constant + boolean only; no factory emitted.
            // Any other (malformed) value → unsupported on the delegating path
            // (D2ERC003 reported in Emit); skip to avoid emitting malformed source.
            if (entry.FactoryShape != _SHAPE_STANDARD)
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
        return sb.ToString().LfNormalized();
    }

    private static void EmitFactory(StringBuilder sb, ErrorCodeEntry entry, CatalogConfig config)
    {
        var baseFactory = BaseFactory(entry.HttpStatus);
        var categoryMember = BaseFactoriesEmitter.CategoryMemberName(entry.Category!);
        sb.AppendLine($"    /// <summary>{EscapeXmlDoc(entry.Doc)}</summary>");
        sb.AppendLine(
            $"    /// <param name=\"messages\">Optional translation messages; defaults to "
            + $"<c>[{entry.UserMessageKey}]</c>. Pass a message bound via "
            + "<c>TKMessage.With(...)</c> to name the offending argument.</param>");
        sb.AppendLine(
            "    /// <returns>A pre-built <see cref=\"D2Result\"/> failure.</returns>");
        if (entry.Deprecated)
        {
            sb.AppendLine(
                $"    [System.Obsolete({ConstantsEmitter.ObsoleteMessageLiteral(entry)})]");
        }

        sb.AppendLine(
            $"    public static D2Result {entry.FactoryName}(IReadOnlyList<TKMessage>? messages = null)");
        sb.AppendLine("    {");
        sb.AppendLine(
            $"        messages ??= [{MapUserMessageKey(entry.UserMessageKey, config)}];");
        sb.AppendLine($"        return D2Result.{baseFactory}(");
        sb.AppendLine("            messages: messages,");
        sb.AppendLine(
            $"            errorCode: {config.ConstantsClassName}.{entry.Code},");
        sb.AppendLine(
            $"            category: ErrorCategory.{categoryMember});");
        sb.AppendLine("    }");
    }

    private static void EmitGenericFactory(
        StringBuilder sb, ErrorCodeEntry entry, CatalogConfig config)
    {
        var baseFactory = BaseFactory(entry.HttpStatus);
        var categoryMember = BaseFactoriesEmitter.CategoryMemberName(entry.Category!);
        sb.AppendLine($"    /// <summary>{EscapeXmlDoc(entry.Doc)} Typed result.</summary>");
        sb.AppendLine(
            $"    /// <param name=\"messages\">Optional translation messages; defaults to "
            + $"<c>[{entry.UserMessageKey}]</c>. Pass a message bound via "
            + "<c>TKMessage.With(...)</c> to name the offending argument.</param>");
        sb.AppendLine(
            "    /// <returns>A pre-built typed <see cref=\"D2Result{T}\"/> failure.</returns>");
        if (entry.Deprecated)
        {
            sb.AppendLine(
                $"    [System.Obsolete({ConstantsEmitter.ObsoleteMessageLiteral(entry)})]");
        }

        sb.AppendLine(
            $"    public static D2Result<T> {entry.FactoryName}(IReadOnlyList<TKMessage>? messages = null)");
        sb.AppendLine("    {");
        sb.AppendLine($"        messages ??= [{MapUserMessageKey(entry.UserMessageKey, config)}];");
        sb.AppendLine($"        return D2Result<T>.{baseFactory}(");
        sb.AppendLine("            messages: messages,");
        sb.AppendLine(
            $"            errorCode: {config.ConstantsClassName}.{entry.Code},");
        sb.AppendLine(
            $"            category: ErrorCategory.{categoryMember});");
        sb.AppendLine("    }");
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
            $"    /// <param name=\"messages\">Optional translation messages; defaults to "
            + $"<c>[{entry.UserMessageKey}]</c>. Pass a message bound via "
            + "<c>TKMessage.With(...)</c> to name the offending argument.</param>");
        sb.AppendLine(
            "    /// <returns>A pre-built typed <see cref=\"D2Result{T}\"/> failure.</returns>");
        if (entry.Deprecated)
        {
            sb.AppendLine(
                $"    [System.Obsolete({ConstantsEmitter.ObsoleteMessageLiteral(entry)})]");
        }

        sb.AppendLine(
            $"    public static D2Result<T> {entry.FactoryName}<T>(IReadOnlyList<TKMessage>? messages = null)");
        sb.AppendLine("    {");
        sb.AppendLine($"        messages ??= [{MapUserMessageKey(entry.UserMessageKey, config)}];");
        sb.AppendLine($"        return D2Result<T>.{baseFactory}(");
        sb.AppendLine("            messages: messages,");
        sb.AppendLine(
            $"            errorCode: {config.ConstantsClassName}.{entry.Code},");
        sb.AppendLine(
            $"            category: ErrorCategory.{categoryMember});");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Appends a newline-delimited config block one line at a time via
    /// <see cref="StringBuilder.AppendLine()"/>. Every emitted line is later
    /// collapsed to a bare LF at the source funnel (<see cref="EmitSource"/>'s
    /// <c>LfNormalized()</c> return), so the generated source is LF-only
    /// regardless of the build host's <c>Environment.NewLine</c>.
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

    private static string MapUserMessageKey(string? userMessageKey, CatalogConfig config)
    {
        // Specs keep TK.* paths for inverse-resolve against snake message keys.
        // Private product catalogs emit ProductTK.* (dual-type).
        if (userMessageKey is null)
            return string.Empty;

        if (string.Equals(config.MessageKeyClassName, "TK", System.StringComparison.Ordinal))
            return userMessageKey;

        if (userMessageKey.StartsWith("TK.", System.StringComparison.Ordinal))
            return config.MessageKeyClassName + userMessageKey.Substring(2);

        return userMessageKey;
    }
}
