// -----------------------------------------------------------------------
// <copyright file="TKGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n.SourceGen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits translation-key catalogs from
/// <c>contracts/messages/*.json</c> via <c>AdditionalFiles</c>.
/// Dual-target: public assembly <c>DcsvIo.D2.I18n.Keys</c> → <c>TK</c>
/// (public messages only); private Extensions assembly
/// <c>DcsvIo.D2.Private.I18n.Keys.Extensions</c> → <c>ProductTK</c>
/// (public∪private messages, distinct FQN under <c>DcsvIo.D2.Private.I18n</c>).
/// </summary>
/// <remarks>
/// The generator filters AdditionalFiles to <c>.json</c> files whose
/// containing directory is named <c>messages</c>. That directory convention
/// matches the consuming csproj's
/// <c>&lt;AdditionalFiles Include="...contracts/messages/*.json" /&gt;</c>
/// glob and avoids accidentally treating unrelated AdditionalFiles as catalogs.
/// Private host PackageId = <c>DcsvIo.D2.Private.I18n.Keys.Extensions</c> (1:1 with the
/// public twin). Emitted dual-types keep the <c>DcsvIo.D2.Private.I18n.ProductTK</c>
/// FQN; <c>TKMessage</c> remains the shared public primitive (IVT grant).
/// </remarks>
[Generator]
public sealed class TKGenerator : IIncrementalGenerator
{
    private const string _GENERATED_SOURCE_NAME = "TK.g.cs";
    private const string _MESSAGES_DIRECTORY_NAME = "messages";
    private const string _EN_US_LOCALE = "en-US";

    private const string _PRIVATE_GENERATED_SOURCE_NAME = "ProductTK.g.cs";
    private const string _PUBLIC_TARGET_ASSEMBLY = "DcsvIo.D2.I18n.Keys";
    private const string _PRIVATE_TARGET_ASSEMBLY = "DcsvIo.D2.Private.I18n.Keys.Extensions";
    private const string _PUBLIC_ROOT_NAMESPACE = "DcsvIo.D2.I18n";
    private const string _PRIVATE_ROOT_NAMESPACE = "DcsvIo.D2.Private.I18n";
    private const string _PUBLIC_CLASS_NAME = "TK";
    private const string _PRIVATE_CLASS_NAME = "ProductTK";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter AdditionalFiles to JSON catalogs.
        var catalogs = context.AdditionalTextsProvider
            .Where(static file => IsCatalogPath(file.Path))
            .Select(static (file, ct) => new LocaleFile(
                Locale: Path.GetFileNameWithoutExtension(file.Path),
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        // 2. Combine with compilation for dual-target assembly gate.
        var combined = catalogs.Combine(context.CompilationProvider);

        // 3. For each pipeline run, drive the emitter.
        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (files, compilation) = tuple;

            var isPublic = string.Equals(
                compilation.AssemblyName,
                _PUBLIC_TARGET_ASSEMBLY,
                StringComparison.Ordinal);
            var isPrivate = string.Equals(
                compilation.AssemblyName,
                _PRIVATE_TARGET_ASSEMBLY,
                StringComparison.Ordinal);

            // Assembly-agnostic public Keys still works when assembly name matches.
            // If neither public nor private host, no emit (prevents CS0433 dual TK).
            if (!isPublic && !isPrivate)
            {
                // Backward-compat: when hosted only by public Keys historically the
                // generator was assembly-agnostic. Public host IS DcsvIo.D2.I18n.Keys.
                // Any other host (tests double-hosting) does not emit.
                return;
            }

            var rootNamespace = isPrivate ? _PRIVATE_ROOT_NAMESPACE : _PUBLIC_ROOT_NAMESPACE;
            var className = isPrivate ? _PRIVATE_CLASS_NAME : _PUBLIC_CLASS_NAME;
            var sourceName = isPrivate ? _PRIVATE_GENERATED_SOURCE_NAME : _GENERATED_SOURCE_NAME;

            // Multi-dir AdditionalFiles: merge all en-US catalogs by key union.
            var enUsFiles = files
                .Where(f => string.Equals(f.Locale, _EN_US_LOCALE, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (enUsFiles.Count == 0 || enUsFiles.All(f => f.Content.Falsey()))
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MissingEnUsJson,
                    Location.None));
                spc.AddSource(
                    sourceName,
                    SourceText.From(
                        EmptyTKSource(rootNamespace, className),
                        System.Text.Encoding.UTF8));
                return;
            }

            // Merge en-US key sets (public∪private on private host; single dir on public).
            var mergedEnUs = MergeLocaleJson(enUsFiles.Select(f => f.Content));

            var others = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var localeGroup in files
                .Where(f => !string.Equals(f.Locale, _EN_US_LOCALE, StringComparison.OrdinalIgnoreCase))
                .GroupBy(f => f.Locale, StringComparer.OrdinalIgnoreCase))
            {
                others[localeGroup.Key] = MergeLocaleJson(localeGroup.Select(f => f.Content));
            }

            var result = TKEmitter.Emit(mergedEnUs, others, rootNamespace, className);

            foreach (var d in result.Diagnostics)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(d));
            }

            spc.AddSource(
                sourceName,
                SourceText.From(result.GeneratedSource, System.Text.Encoding.UTF8));
        });
    }

    private static string MergeLocaleJson(IEnumerable<string> jsonContents)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        string? schema = null;
        foreach (var json in jsonContents)
        {
            if (json.Falsey())
            {
                continue;
            }

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, "$schema", StringComparison.Ordinal))
                {
                    schema ??= prop.Value.GetString();
                    continue;
                }

                if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    // First write wins for stable merge (public then private if ordered).
                    if (!merged.ContainsKey(prop.Name))
                    {
                        merged[prop.Name] = prop.Value.GetString()!;
                    }
                }
            }
        }

        // Rebuild via JsonSerializer so newlines/quotes escape correctly.
        var ordered = new System.Collections.Generic.SortedDictionary<string, string>(
            StringComparer.Ordinal);
        if (schema is not null)
        {
            ordered["$schema"] = schema;
        }

        foreach (var kvp in merged.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            ordered[kvp.Key] = kvp.Value;
        }

        return System.Text.Json.JsonSerializer.Serialize(ordered);
    }

    private static bool IsCatalogPath(string path)
    {
        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var dir = Path.GetDirectoryName(path);
        if (dir is null)
        {
            return false;
        }

        var dirName = Path.GetFileName(dir);
        return string.Equals(dirName, _MESSAGES_DIRECTORY_NAME, StringComparison.OrdinalIgnoreCase);
    }

    private static Diagnostic ToRoslynDiagnostic(EmitDiagnostic emitDiag)
    {
        var descriptor = ResolveDescriptor(emitDiag.DescriptorId);
        return Diagnostic.Create(descriptor, Location.None, emitDiag.Args.ToArray());
    }

    private static DiagnosticDescriptor ResolveDescriptor(string id) => id switch
    {
        DiagnosticIds.InvalidTranslationKey => DiagnosticDescriptors.InvalidTranslationKey,
        DiagnosticIds.MissingKeyInLocale => DiagnosticDescriptors.MissingKeyInLocale,
        DiagnosticIds.TranslationKeyCollision => DiagnosticDescriptors.TranslationKeyCollision,
        DiagnosticIds.OrphanKeyInLocale => DiagnosticDescriptors.OrphanKeyInLocale,
        DiagnosticIds.MissingEnUsJson => DiagnosticDescriptors.MissingEnUsJson,
        DiagnosticIds.MalformedJsonCatalog => DiagnosticDescriptors.MalformedJsonCatalog,
        _ => throw new InvalidOperationException($"Unknown EmitDiagnostic descriptor id '{id}'."),
    };

    private static string EmptyTKSource(string rootNamespace, string className) =>
        "// <auto-generated>\n#nullable enable\n" +
        $"namespace {rootNamespace};\npublic static partial class {className} {{ }}\n";
}
