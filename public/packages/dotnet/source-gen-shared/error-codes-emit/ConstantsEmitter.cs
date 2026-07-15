// -----------------------------------------------------------------------
// <copyright file="ConstantsEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Shared logic for emitting the per-catalog constants static class source
/// (e.g. <c>AuthErrorCodes</c> / <c>ErrorCodes</c>) from a parsed
/// <see cref="ErrorCodesSpec"/> + a <see cref="CatalogConfig"/>. Stateless
/// and unit-testable in isolation. Runs the superset of validations gated by
/// the catalog config so each catalog reproduces its existing diagnostic
/// surface byte-for-byte. The Roslyn host (<c>ErrorCodesGenerator</c> shell)
/// calls this and translates emit-side <see cref="EmitDiagnostic"/> records
/// into Roslyn <c>Diagnostic</c> instances.
/// </summary>
internal static class ConstantsEmitter
{
    /// <summary>
    /// HTTP statuses the codegen mapping supports. Superset across every
    /// catalog — the auth catalog further narrows the accepted set via its
    /// schema's <c>[401, 503]</c> enum at edit time, but the engine accepts
    /// the full range so any per-domain catalog may use any standard status.
    /// </summary>
    public static readonly ImmutableArray<int> SR_SupportedHttpStatuses =
        ImmutableArray.Create(200, 206, 207, 400, 401, 403, 404, 409, 413, 429, 500, 503);

    /// <summary>
    /// SCREAMING_SNAKE code-shape validator. Bucket 1 (no backtracking) — anchored
    /// character-class match; no timeout needed.
    /// </summary>
    private static readonly Regex sr_codePattern =
        new("^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Emits the constants class source plus diagnostics for the supplied
    /// spec + catalog config.
    /// </summary>
    /// <param name="spec">Parsed error-codes spec.</param>
    /// <param name="config">The catalog configuration.</param>
    /// <param name="categoryWireSet">
    /// The spec-derived closed set of valid <c>category</c> wire strings (from
    /// <c>error-category.spec.json</c>). When empty (spec absent / malformed)
    /// the category-membership check degrades to a no-op. Non-engine callers
    /// (the base/domain failures emitters) pass
    /// <see cref="ImmutableHashSet{T}.Empty"/> — category validation already
    /// ran in the constants pass for the same entry set.
    /// </param>
    /// <returns>Generated source + diagnostics.</returns>
    public static EmitResult Emit(
        ErrorCodesSpec spec,
        CatalogConfig config,
        ImmutableHashSet<string> categoryWireSet)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var validEntries = Validate(spec, config, categoryWireSet, diagnostics);
        var source = EmitSource(validEntries, config);
        return new EmitResult(source, diagnostics.ToImmutable());
    }

    /// <summary>
    /// Runs the catalog's validation rules, collecting diagnostics and
    /// returning the entries that survive every check (in spec order).
    /// Shared with <see cref="FailuresEmitter"/> so the constants + failures
    /// files agree on which entries are valid.
    /// </summary>
    /// <param name="spec">Parsed error-codes spec.</param>
    /// <param name="config">The catalog configuration.</param>
    /// <param name="categoryWireSet">
    /// The spec-derived closed set of valid <c>category</c> wire strings (from
    /// <c>error-category.spec.json</c>). When non-empty it is the membership
    /// authority for the category check; when empty (spec absent / malformed)
    /// the check is skipped — mirroring the en-US.json TK cross-check's
    /// empty-degrades-to-no-op behavior so a missing AdditionalFile never
    /// fires a false unknown-category diagnostic.
    /// </param>
    /// <param name="diagnostics">Accumulates diagnostics for invalid entries.</param>
    /// <returns>The valid entries in spec order.</returns>
    public static List<ErrorCodeEntry> Validate(
        ErrorCodesSpec spec,
        CatalogConfig config,
        ImmutableHashSet<string> categoryWireSet,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        var supportedStatuses = string.Join(
            ", ",
            SR_SupportedHttpStatuses.Select(
                s => s.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        var validEntries = new List<ErrorCodeEntry>();
        var seenCodes = new HashSet<string>(StringComparer.Ordinal);
        var seenFactories = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in spec.ErrorCodes)
        {
            // Generic-only: the SCREAMING_SNAKE code-shape regex (auth relies
            // on its schema's ^AUTH_… regex at edit time + the engine's
            // domain-prefix diagnostic at build time).
            if (config.InvalidCodeId is { } invalidCodeId &&
                (entry.Code.Falsey() || !sr_codePattern.IsMatch(entry.Code)))
            {
                diagnostics.Add(new EmitDiagnostic(invalidCodeId, [entry.Code]));
                continue;
            }

            // Engine-level: per-domain codes must start with the catalog's
            // enforced domain prefix (generic catalog is exempt, prefix null).
            if (config.DomainPrefix is { } prefix &&
                !entry.Code.StartsWith(prefix, StringComparison.Ordinal))
            {
                diagnostics.Add(EngineDiagnostics.DomainPrefixViolation(
                    entry.Code, config.TargetAssemblyName, prefix));
                continue;
            }

            if (!seenCodes.Add(entry.Code))
            {
                diagnostics.Add(new EmitDiagnostic(config.DuplicateCodeId, [entry.Code]));
                continue;
            }

            // Auth-only: factoryName uniqueness.
            if (config.DuplicateFactoryNameId is { } dupFactoryId &&
                entry.FactoryName is { } factoryName &&
                !seenFactories.Add(factoryName))
            {
                diagnostics.Add(new EmitDiagnostic(dupFactoryId, [factoryName]));
                continue;
            }

            // Auth-only: category enum membership against the spec-derived
            // closed set. An empty set means error-category.spec.json was not
            // surfaced / was unparseable — skip the check (the same degradation
            // the en-US.json TK cross-check uses) rather than fire false
            // positives.
            if (config.ValidateCategory && config.UnknownCategoryId is { } unknownCatId &&
                !categoryWireSet.IsEmpty &&
                !categoryWireSet.Contains(entry.Category ?? string.Empty))
            {
                diagnostics.Add(new EmitDiagnostic(
                    unknownCatId,
                    [
                        entry.Code,
                        entry.Category ?? string.Empty,
                        string.Join(
                            ", ",
                            categoryWireSet.OrderBy(c => c, StringComparer.Ordinal)),
                    ]));
                continue;
            }

            if (!SR_SupportedHttpStatuses.Contains(entry.HttpStatus))
            {
                diagnostics.Add(new EmitDiagnostic(
                    config.InvalidHttpStatusId,
                    [entry.Code, entry.HttpStatus, supportedStatuses]));
                continue;
            }

            // Engine-level: userMessageKey must inverse-resolve to a real key.
            // Cross-checked against en-US.json in the engine (which has the key
            // set); the membership assertion happens there, not here.

            // Generic-only: non-empty doc.
            if (config.MissingDocId is { } missingDocId && entry.Doc.Falsey())
            {
                diagnostics.Add(new EmitDiagnostic(missingDocId, [entry.Code]));
                continue;
            }

            validEntries.Add(entry);
        }

        return validEntries;
    }

    /// <summary>
    /// Composes the verbatim-string-literal argument for the <c>[Obsolete(...)]</c>
    /// attribute on a deprecated entry. The message is the entry's
    /// <c>deprecatedReason</c>, with <c>" Use {replacedBy} instead."</c> appended
    /// when a successor code is declared. The result is wrapped in a C# string
    /// literal (the same escaping the emitter applies to other string literals).
    /// Shared with <see cref="FailuresEmitter"/> so the constant + factory carry
    /// an identical attribute message.
    /// </summary>
    /// <param name="entry">A deprecated entry (<c>entry.Deprecated == true</c>).</param>
    /// <returns>The quoted, escaped <c>[Obsolete]</c> message literal.</returns>
    internal static string ObsoleteMessageLiteral(ErrorCodeEntry entry)
    {
        var reason = entry.DeprecatedReason ?? string.Empty;

        var message = entry.ReplacedBy is { } replacedBy && !replacedBy.Falsey()
            ? $"{reason} Use {replacedBy} instead."
            : reason;

        return $"\"{EscapeStringLiteral(message)}\"";
    }

    private static string EmitSource(List<ErrorCodeEntry> entries, CatalogConfig config)
    {
        var sb = new StringBuilder();
        EmitBlock(sb, config.ConstantsBanner);
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Generic;");

        if (config.EmitKebabCase)
            sb.AppendLine("using System.Text;");

        sb.AppendLine();
        sb.AppendLine($"namespace {config.RootNamespace};");
        sb.AppendLine();
        EmitBlock(sb, config.ConstantsSummary);
        sb.AppendLine($"public static class {config.ConstantsClassName}");
        sb.AppendLine("{");

        foreach (var entry in entries)
        {
            sb.AppendLine($"    /// <summary>{EscapeXmlDoc(entry.Doc)}</summary>");

            if (entry.Deprecated)
                sb.AppendLine($"    [System.Obsolete({ObsoleteMessageLiteral(entry)})]");

            sb.AppendLine(
                $"    public const string {entry.Code} = \"{EscapeStringLiteral(entry.Code)}\";");

            sb.AppendLine();
        }

        EmitAllCodes(sb, entries, config);
        sb.AppendLine();
        EmitGetHttpStatus(sb, entries, config);

        if (config.EmitKebabCase)
        {
            sb.AppendLine();
            EmitKebabCase(sb);
        }

        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    /// <summary>
    /// Appends a newline-delimited config block one line at a time via
    /// <see cref="StringBuilder.AppendLine()"/>. Every emitted line is later
    /// collapsed to a bare LF at the source funnel (<see cref="EmitSource"/>'s
    /// <c>LfNormalized()</c> return), so the generated source is LF-only
    /// regardless of the build host's <c>Environment.NewLine</c>. The block's
    /// trailing newline is consumed by the per-line split, so callers do NOT
    /// add a separate blank-line append.
    /// </summary>
    private static void EmitBlock(StringBuilder sb, string block)
    {
        var lines = block.Split('\n');

        // A trailing newline produces a final empty segment — drop it so the
        // block ends exactly at its last real line.
        var count = lines.Length;

        if (count > 0 && lines[count - 1].Length == 0)
            count--;

        for (var i = 0; i < count; i++)
            sb.AppendLine(lines[i]);
    }

    private static void EmitAllCodes(
        StringBuilder sb, List<ErrorCodeEntry> entries, CatalogConfig config)
    {
        EmitBlock(sb, config.AllCodesDoc);
        sb.AppendLine(
            "    public static IReadOnlyList<string> AllCodes => sr_allCodes;");
        sb.AppendLine();
        sb.AppendLine(
            "    private static readonly IReadOnlyList<string> sr_allCodes = new string[]");
        sb.AppendLine("    {");

        foreach (var entry in entries)
            sb.AppendLine($"        \"{EscapeStringLiteral(entry.Code)}\",");

        sb.AppendLine("    };");
    }

    private static void EmitGetHttpStatus(
        StringBuilder sb, List<ErrorCodeEntry> entries, CatalogConfig config)
    {
        EmitBlock(sb, config.GetHttpStatusDoc);
        sb.AppendLine("    public static int GetHttpStatus(string errorCode) => errorCode switch");
        sb.AppendLine("    {");

        foreach (var entry in entries)
        {
            sb.AppendLine(
                $"        \"{EscapeStringLiteral(entry.Code)}\" => {entry.HttpStatus},");
        }

        sb.AppendLine("        _ => 500,");
        sb.AppendLine("    };");
    }

    private static void EmitKebabCase(StringBuilder sb)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Converts a SCREAMING_SNAKE error code (e.g. <c>AUTH_BEARER_MISSING</c>)");
        sb.AppendLine(
            "    /// to its kebab-case form (<c>auth-bearer-missing</c>) for use in the");
        sb.AppendLine(
            "    /// RFC 7807 ProblemDetails <c>type</c> URI.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    /// <param name=\"upperUnderscore\">The SCREAMING_SNAKE error code.</param>");
        sb.AppendLine("    /// <returns>The kebab-case form.</returns>");
        sb.AppendLine("    public static string KebabCase(string upperUnderscore)");
        sb.AppendLine("    {");
        sb.AppendLine("        var sb = new StringBuilder(upperUnderscore.Length);");
        sb.AppendLine("        for (var i = 0; i < upperUnderscore.Length; i++)");
        sb.AppendLine("        {");
        sb.AppendLine("            var c = upperUnderscore[i];");
        sb.AppendLine("            sb.Append(c == '_' ? '-' : char.ToLowerInvariant(c));");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return sb.ToString();");
        sb.AppendLine("    }");
    }

    private static string EscapeStringLiteral(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"");

    private static string EscapeXmlDoc(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
