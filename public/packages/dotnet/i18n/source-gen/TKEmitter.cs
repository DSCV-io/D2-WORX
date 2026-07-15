// -----------------------------------------------------------------------
// <copyright file="TKEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for emitting the <c>TK</c> static class source from a parsed
/// en-US JSON catalog plus optional non-en-US catalogs (used only for
/// per-locale coverage diagnostics).
/// </summary>
/// <remarks>
/// Stateless and unit-testable in isolation. The Roslyn-host integration
/// (<see cref="TKGenerator"/>) reads the JSON files via <c>AdditionalText</c>
/// and forwards them here.
/// </remarks>
internal static class TKEmitter
{
    private const string _SCHEMA_KEY = "$schema";
    private const string _DEFAULT_ROOT_NAMESPACE = "DcsvIo.D2.I18n";
    private const string _DEFAULT_TK_CLASS_NAME = "TK";

    /// <summary>
    /// Emits the TK class source plus diagnostics for the supplied catalogs.
    /// </summary>
    /// <param name="enUsJson">The en-US JSON catalog content. Source of truth for TK.</param>
    /// <param name="otherLocales">
    /// Other locale catalogs by locale code (e.g. <c>"fr-FR"</c> → JSON content).
    /// Used for per-locale coverage diagnostics only — the keys themselves are
    /// NOT included in TK if they're absent from en-US.
    /// </param>
    /// <param name="rootNamespace">Emit namespace (public TK or private ProductTK).</param>
    /// <param name="className">Emit class name (<c>TK</c> or <c>ProductTK</c>).</param>
    /// <returns>The generated source code plus an immutable array of diagnostics.</returns>
    public static EmitResult Emit(
        string enUsJson,
        IReadOnlyDictionary<string, string> otherLocales,
        string? rootNamespace = null,
        string? className = null)
    {
        var effectiveNamespace = rootNamespace ?? _DEFAULT_ROOT_NAMESPACE;
        var effectiveClassName = className ?? _DEFAULT_TK_CLASS_NAME;
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();

        // Parse en-US — if it fails, emit an empty TK class with a diagnostic.
        Dictionary<string, string> enUsKeys;
        try
        {
            enUsKeys = ParseCatalog(enUsJson);
        }
        catch (Exception ex)
        {
            diagnostics.Add(EmitDiagnostics.MalformedJson("en-US.json", ex.Message));
            return new EmitResult(
                EmptyTKSource(effectiveNamespace, effectiveClassName),
                diagnostics.ToImmutable());
        }

        // Decompose every key, collect valid ones and report invalid ones.
        // Sort by key for deterministic emission order (cache stability).
        // Type alias for the 3-level nested sorted dictionary keeps the line short.
        var byPath = NewByPath();

        foreach (var kvp in enUsKeys.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var decomposed = KeyDecomposer.Decompose(kvp.Key);
            if (!decomposed.IsValid)
            {
                diagnostics.Add(EmitDiagnostics.InvalidKey(kvp.Key, decomposed.InvalidReason!));
                continue;
            }

            if (!byPath.TryGetValue(decomposed.Domain, out var categories))
            {
                categories = NewCategoryDict();
                byPath[decomposed.Domain] = categories;
            }

            if (!categories.TryGetValue(decomposed.Category, out var constants))
            {
                constants = new SortedDictionary<string, string>(StringComparer.Ordinal);
                categories[decomposed.Category] = constants;
            }

            if (constants.TryGetValue(decomposed.ConstantName, out var existingKey))
            {
                // Collision: two source keys decompose to the same TK path.
                // First one wins (we got here via OrderBy), report the loser.
                var fullPath =
                    $"{effectiveClassName}.{decomposed.Domain}." +
                    $"{decomposed.Category}.{decomposed.ConstantName}";
                diagnostics.Add(EmitDiagnostics.Collision(existingKey, kvp.Key, fullPath));
                continue;
            }

            constants[decomposed.ConstantName] = kvp.Key;
        }

        // Per-locale coverage diagnostics.
        var enUsKeySet = enUsKeys.Keys
            .Where(k => !string.Equals(k, _SCHEMA_KEY, StringComparison.Ordinal))
            .ToImmutableHashSet(StringComparer.Ordinal);

        foreach (var localeKvp in otherLocales.OrderBy(l => l.Key, StringComparer.Ordinal))
        {
            Dictionary<string, string> localeKeys;
            try
            {
                localeKeys = ParseCatalog(localeKvp.Value);
            }
            catch (Exception ex)
            {
                diagnostics.Add(EmitDiagnostics.MalformedJson(localeKvp.Key + ".json", ex.Message));
                continue;
            }

            // en-US-present-but-missing-here → D2I18N002.
            foreach (var enUsKey in enUsKeySet.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (!localeKeys.ContainsKey(enUsKey))
                {
                    diagnostics.Add(EmitDiagnostics.MissingInLocale(enUsKey, localeKvp.Key));
                }
            }

            // here-but-missing-in-en-US → D2I18N004.
            foreach (var localeKey in localeKeys.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (string.Equals(localeKey, _SCHEMA_KEY, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!enUsKeySet.Contains(localeKey))
                {
                    diagnostics.Add(EmitDiagnostics.OrphanInLocale(localeKey, localeKvp.Key));
                }
            }
        }

        var source = EmitSource(byPath, effectiveNamespace, effectiveClassName);
        return new EmitResult(source, diagnostics.ToImmutable());
    }

    private static Dictionary<string, string> ParseCatalog(string json)
    {
        // Use JsonDocument for granular parsing — we must error on non-string
        // values without aborting the whole catalog.
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException(
                $"Catalog root must be a JSON object, got {doc.RootElement.ValueKind}.");
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (string.Equals(prop.Name, _SCHEMA_KEY, StringComparison.Ordinal))
            {
                continue;
            }

            if (prop.Value.ValueKind != JsonValueKind.String)
            {
                throw new JsonException(
                    $"Catalog entry '{prop.Name}' must be a string, got {prop.Value.ValueKind}.");
            }

            result[prop.Name] = prop.Value.GetString()!;
        }

        return result;
    }

    private static string EmitSource(
        SortedDictionary<string, SortedDictionary<string, SortedDictionary<string, string>>>
            byPath,
        string rootNamespace,
        string className)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//   Generated by DcsvIo.D2.I18n.SourceGen.TKGenerator");
        sb.AppendLine("//   from contracts/messages/en-US.json (the source of truth).");
        sb.AppendLine("//   Manual edits will be lost on rebuild.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Translation key constants generated from <c>contracts/messages/en-US.json</c>.");
        sb.AppendLine(
            "/// Each constant is a <see cref=\"global::DcsvIo.D2.I18n.TKMessage\"/> with the JSON key and no params;");
        sb.AppendLine("/// bind parameters via <see cref=\"global::DcsvIo.D2.I18n.TKMessage.With(string, string)\"/>.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static partial class {className}");
        sb.AppendLine("{");

        var domainOrder = byPath.Keys.ToList();
        for (var i = 0; i < domainOrder.Count; i++)
        {
            var domain = domainOrder[i];
            EmitDomain(sb, domain, byPath[domain], indent: 1);
            if (i < domainOrder.Count - 1)
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static void EmitDomain(
        StringBuilder sb,
        string domain,
        SortedDictionary<string, SortedDictionary<string, string>> categories,
        int indent)
    {
        var pad = new string(' ', indent * 4);
        sb.AppendLine($"{pad}/// <summary>");
        sb.AppendLine($"{pad}/// {domain} translation keys.");
        sb.AppendLine($"{pad}/// </summary>");
        sb.AppendLine($"{pad}public static partial class {domain}");
        sb.AppendLine($"{pad}{{");

        var categoryOrder = categories.Keys.ToList();
        for (var i = 0; i < categoryOrder.Count; i++)
        {
            var category = categoryOrder[i];
            EmitCategory(sb, category, categories[category], indent + 1);
            if (i < categoryOrder.Count - 1)
            {
                sb.AppendLine();
            }
        }

        sb.AppendLine($"{pad}}}");
    }

    private static void EmitCategory(
        StringBuilder sb,
        string category,
        SortedDictionary<string, string> constants,
        int indent)
    {
        var pad = new string(' ', indent * 4);
        sb.AppendLine($"{pad}/// <summary>");
        sb.AppendLine($"{pad}/// {category} translation keys.");
        sb.AppendLine($"{pad}/// </summary>");
        sb.AppendLine($"{pad}public static partial class {category}");
        sb.AppendLine($"{pad}{{");

        foreach (var constantKvp in constants)
        {
            var docKey = EscapeXmlDoc(constantKvp.Value);
            var literalKey = EscapeStringLiteral(constantKvp.Value);
            sb.AppendLine($"{pad}    /// <summary>");
            sb.AppendLine($"{pad}    /// Translation key <c>{docKey}</c>.");
            sb.AppendLine($"{pad}    /// </summary>");
            sb.AppendLine(
                $"{pad}    public static readonly global::DcsvIo.D2.I18n.TKMessage " +
                $"{constantKvp.Key} = new(\"{literalKey}\");");
        }

        sb.AppendLine($"{pad}}}");
    }

    private static SortedDictionary<
        string,
        SortedDictionary<string, SortedDictionary<string, string>>>
        NewByPath() => new(StringComparer.Ordinal);

    private static SortedDictionary<string, SortedDictionary<string, string>>
        NewCategoryDict() => new(StringComparer.Ordinal);

    private static string EmptyTKSource(string rootNamespace, string className)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//   Generated by DcsvIo.D2.I18n.SourceGen.TKGenerator");
        sb.AppendLine("//   No keys could be loaded from contracts/messages/en-US.json.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace};");
        sb.AppendLine();
        sb.AppendLine($"public static partial class {className}");
        sb.AppendLine("{");
        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static string EscapeStringLiteral(string value)
    {
        // netstandard2.0 lacks the (string, string, StringComparison) overload —
        // use the 2-arg form.
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }

    private static string EscapeXmlDoc(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
