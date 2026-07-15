// -----------------------------------------------------------------------
// <copyright file="ErrorCategorySpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Category.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>Parses <c>error-category.spec.json</c>.</summary>
internal static class ErrorCategorySpecLoader
{
    private const string _CATEGORIES_KEY = "categories";
    private const string _WIRE_KEY = "wire";
    private const string _DOC_KEY = "doc";

    /// <summary>Parses raw JSON spec content into an <see cref="ErrorCategorySpec"/>.</summary>
    /// <param name="path">Spec file path.</param>
    /// <param name="json">Raw JSON content.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/>.</returns>
    public static LoadResult<ErrorCategorySpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<ErrorCategorySpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_CATEGORIES_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<ErrorCategorySpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'categories' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<ErrorCategoryEntry>();
            var index = 0;
            foreach (var element in arr.EnumerateArray())
            {
                var (entry, diag) = ParseEntry(element, fileName, index);
                if (diag is not null)
                {
                    return new LoadResult<ErrorCategorySpec>(
                        Spec: null, Diagnostic: diag);
                }

                entries.Add(entry!);
                index++;
            }

            return new LoadResult<ErrorCategorySpec>(
                Spec: new ErrorCategorySpec(entries.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<ErrorCategorySpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (ErrorCategoryEntry? Entry, EmitDiagnostic? Diagnostic) ParseEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"categories[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_WIRE_KEY, out var wireEl) ||
            wireEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"categories[{index}] missing required string 'wire'"));
        }

        var wire = wireEl.GetString()!;

        if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
            docEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"categories[{index}] '{wire}' missing required string 'doc'"));
        }

        var docText = docEl.GetString()!;

        return (new ErrorCategoryEntry(wire, docText), null);
    }
}
