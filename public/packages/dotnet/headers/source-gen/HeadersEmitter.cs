// -----------------------------------------------------------------------
// <copyright file="HeadersEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Headers.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Pure logic for emitting one per-transport headers catalog source from a
/// parsed <see cref="HeadersSpec"/>. Stateless and unit-testable in
/// isolation. The Roslyn-host integration (<see cref="HeadersGenerator"/>)
/// calls this once per consuming assembly with the matching catalog filter.
/// </summary>
internal static class HeadersEmitter
{
    /// <summary>Closed enum of supported transports.</summary>
    public static readonly ImmutableHashSet<string> SR_ValidTransports =
        ImmutableHashSet.Create(StringComparer.Ordinal, "http", "grpc", "amqp");

    /// <summary>Closed enum of recognized conventions (warning-only enforcement).</summary>
    public static readonly ImmutableHashSet<string> SR_RecognizedConventions =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "d2",
            "rfc",
            "w3c",
            "stripe",
            "amqp",
            "amqp-x",
            "oauth");

    private static readonly Regex sr_constNameRegex =
        new("^[A-Z][A-Z0-9_]*$", RegexOptions.Compiled);

    /// <summary>
    /// Catalog filter — selects which spec entries belong in a given catalog.
    /// </summary>
    public enum CatalogFilter
    {
        /// <summary>Cross-transport entries (applicability count >= 2).</summary>
        Common,

        /// <summary>Entries whose applicability includes <c>http</c>.</summary>
        Http,

        /// <summary>Entries whose applicability includes <c>amqp</c>.</summary>
        Amqp,

        /// <summary>Entries whose applicability includes <c>grpc</c>.</summary>
        Grpc,
    }

    /// <summary>
    /// Emits the per-catalog headers class source plus diagnostics.
    /// </summary>
    /// <param name="spec">Parsed headers spec.</param>
    /// <param name="filter">Catalog filter selecting which entries to emit.</param>
    /// <param name="targetNamespace">Namespace the emitted class lives in.</param>
    /// <param name="className">Emitted static class name (e.g. <c>HttpHeaders</c>).</param>
    /// <returns>Generated source + diagnostics.</returns>
    public static EmitResult Emit(
        HeadersSpec spec,
        CatalogFilter filter,
        string targetNamespace,
        string className)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var validEntries = new List<HeaderEntry>();
        var seenConstsByCatalog = new Dictionary<CatalogFilter, HashSet<string>>
        {
            { CatalogFilter.Common, new HashSet<string>(StringComparer.Ordinal) },
            { CatalogFilter.Http, new HashSet<string>(StringComparer.Ordinal) },
            { CatalogFilter.Amqp, new HashSet<string>(StringComparer.Ordinal) },
            { CatalogFilter.Grpc, new HashSet<string>(StringComparer.Ordinal) },
        };

        foreach (var entry in spec.Headers)
        {
            if (!sr_constNameRegex.IsMatch(entry.ConstName))
            {
                diagnostics.Add(EmitDiagnostics.InvalidConstName(entry.Name, entry.ConstName));
                continue;
            }

            if (entry.Applicability.IsDefaultOrEmpty || entry.Applicability.Length == 0)
            {
                diagnostics.Add(EmitDiagnostics.EmptyApplicability(entry.ConstName));
                continue;
            }

            var badTransport = false;
            foreach (var t in entry.Applicability)
            {
                if (!SR_ValidTransports.Contains(t))
                {
                    diagnostics.Add(EmitDiagnostics.UnknownTransport(
                        entry.ConstName,
                        t,
                        string.Join(
                            ", ",
                            SR_ValidTransports.OrderBy(s => s, StringComparer.Ordinal))));
                    badTransport = true;
                    break;
                }
            }

            if (badTransport) continue;

            if (!entry.Convention.Falsey() &&
                !SR_RecognizedConventions.Contains(entry.Convention))
            {
                diagnostics.Add(EmitDiagnostics.UnknownConvention(
                    entry.ConstName,
                    entry.Convention,
                    string.Join(
                        ", ",
                        SR_RecognizedConventions.OrderBy(s => s, StringComparer.Ordinal))));
            }

            // Per-catalog duplicate check (across all catalogs the entry would belong to).
            var duplicate = false;
            foreach (var cat in CatalogsForEntry(entry))
            {
                if (!seenConstsByCatalog[cat].Add(entry.ConstName))
                {
                    diagnostics.Add(EmitDiagnostics.DuplicateConstName(
                        entry.ConstName, CatalogName(cat)));
                    duplicate = true;
                    break;
                }
            }

            if (duplicate) continue;

            validEntries.Add(entry);
        }

        // Filter to the catalog this emit call is for.
        var filtered = validEntries.Where(e => BelongsTo(e, filter)).ToList();
        var source = EmitSource(filtered, filter, targetNamespace, className);
        return new EmitResult(source, diagnostics.ToImmutable());
    }

    /// <summary>Returns every catalog an entry belongs to.</summary>
    /// <param name="entry">The spec entry to inspect.</param>
    /// <returns>The catalogs the entry will appear in.</returns>
    public static IEnumerable<CatalogFilter> CatalogsForEntry(HeaderEntry entry)
    {
        if (entry.Applicability.Length >= 2) yield return CatalogFilter.Common;
        foreach (var t in entry.Applicability)
        {
            switch (t)
            {
                case "http": yield return CatalogFilter.Http; break;
                case "amqp": yield return CatalogFilter.Amqp; break;
                case "grpc": yield return CatalogFilter.Grpc; break;
            }
        }
    }

    /// <summary>True when an entry belongs to the supplied catalog.</summary>
    /// <param name="entry">The spec entry to inspect.</param>
    /// <param name="filter">The catalog filter to test against.</param>
    /// <returns>
    /// <c>true</c> when the entry belongs to the catalog; otherwise <c>false</c>.
    /// </returns>
    public static bool BelongsTo(HeaderEntry entry, CatalogFilter filter)
    {
        return filter switch
        {
            CatalogFilter.Common => entry.Applicability.Length >= 2,
            CatalogFilter.Http => entry.Applicability.Contains("http"),
            CatalogFilter.Amqp => entry.Applicability.Contains("amqp"),
            CatalogFilter.Grpc => entry.Applicability.Contains("grpc"),
            _ => false,
        };
    }

    /// <summary>Maps a catalog filter to its lowercase name (used in diagnostics).</summary>
    /// <param name="filter">The catalog filter to map.</param>
    /// <returns>The lowercase name of the catalog.</returns>
    public static string CatalogName(CatalogFilter filter) => filter switch
    {
        CatalogFilter.Common => "common",
        CatalogFilter.Http => "http",
        CatalogFilter.Amqp => "amqp",
        CatalogFilter.Grpc => "grpc",
        _ => "unknown",
    };

    private static string EmitSource(
        List<HeaderEntry> entries,
        CatalogFilter filter,
        string targetNamespace,
        string className)
    {
        var sortedEntries = entries
            .OrderBy(e => e.ConstName, StringComparer.Ordinal)
            .ToList();
        var sb = new StringBuilder();
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//   Generated by DcsvIo.D2.Headers.SourceGen.HeadersGenerator");
        sb.AppendLine("//   from contracts/headers/headers.spec.json (the source of truth).");
        sb.AppendLine("//   Manual edits will be lost on rebuild.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {targetNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            $"/// D2 wire-protocol headers applicable to the " +
            $"<c>{CatalogName(filter)}</c> catalog.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine(
            "/// Generated from contracts/headers/headers.spec.json. Cross-transport entries");
        sb.AppendLine(
            "/// (those applicable to more than one transport) appear in this catalog AND in");
        sb.AppendLine(
            "/// every other applicable per-transport catalog at identical wire values,");
        sb.AppendLine("/// codegen-guaranteed.");
        sb.AppendLine("/// </remarks>");
        sb.AppendLine($"public static class {className}");
        sb.AppendLine("{");

        foreach (var entry in sortedEntries)
        {
            EmitConstantXmlDoc(sb, entry);
            var literal = EscapeStringLiteral(entry.Name);
            sb.AppendLine(
                $"    public const string {entry.ConstName} = \"{literal}\";");
            sb.AppendLine();
        }

        EmitAllHeaders(sb, sortedEntries, className);

        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static void EmitConstantXmlDoc(StringBuilder sb, HeaderEntry entry)
    {
        sb.AppendLine("    /// <summary>");
        foreach (var line in entry.Description.Split('\n'))
            sb.AppendLine($"    /// {EscapeXmlDoc(line)}");
        sb.AppendLine("    /// </summary>");
        var convention = EscapeXmlDoc(entry.Convention);
        var applicability = EscapeXmlDoc(
            string.Join(", ", entry.Applicability.OrderBy(s => s, StringComparer.Ordinal)));
        sb.AppendLine(
            $"    /// <remarks>Convention: <c>{convention}</c>. " +
            $"Applicability: <c>{applicability}</c>.</remarks>");
    }

    private static void EmitAllHeaders(
        StringBuilder sb,
        List<HeaderEntry> entries,
        string className)
    {
        var listName = $"All{className}";
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// All headers in this catalog in <c>constName</c> order.");
        sb.AppendLine(
            "    /// Useful for cross-spec consistency tests.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static IReadOnlyList<string> {listName} => sr_allHeaders;");
        sb.AppendLine();
        sb.AppendLine(
            "    private static readonly IReadOnlyList<string> sr_allHeaders = new string[]");
        sb.AppendLine("    {");
        foreach (var entry in entries)
            sb.AppendLine($"        \"{EscapeStringLiteral(entry.Name)}\",");
        sb.AppendLine("    };");
    }

    private static string EscapeStringLiteral(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"");

    private static string EscapeXmlDoc(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
