// -----------------------------------------------------------------------
// <copyright file="OtelMessagingTagsSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.OtelMessagingTags.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for parsing <c>otel-messaging-tags.spec.json</c> into an
/// <see cref="OtelMessagingTagsSpec"/> record.
/// </summary>
internal static class OtelMessagingTagsSpecLoader
{
    private const string _TAGS_KEY = "tags";
    private const string _CONST_NAME_KEY = "constName";
    private const string _VALUE_KEY = "value";
    private const string _DOC_KEY = "doc";

    /// <summary>
    /// Parses raw JSON spec content into an <see cref="OtelMessagingTagsSpec"/>.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>
    /// A <see cref="LoadResult{TSpec}"/> wrapping <see cref="OtelMessagingTagsSpec"/>.
    /// </returns>
    public static LoadResult<OtelMessagingTagsSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<OtelMessagingTagsSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_TAGS_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<OtelMessagingTagsSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'tags' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<OtelMessagingTagEntry>();
            var index = 0;
            foreach (var element in arr.EnumerateArray())
            {
                var (entry, diag) = ParseEntry(element, fileName, index);
                if (diag is not null)
                {
                    return new LoadResult<OtelMessagingTagsSpec>(
                        Spec: null, Diagnostic: diag);
                }

                entries.Add(entry!);
                index++;
            }

            return new LoadResult<OtelMessagingTagsSpec>(
                Spec: new OtelMessagingTagsSpec(entries.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<OtelMessagingTagsSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (OtelMessagingTagEntry? Entry, EmitDiagnostic? Diagnostic) ParseEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"tags[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CONST_NAME_KEY, out var nameEl) ||
            nameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"tags[{index}] missing required string 'constName'"));
        }

        var constName = nameEl.GetString()!;

        if (!element.TryGetProperty(_VALUE_KEY, out var valueEl) ||
            valueEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"tags[{index}] '{constName}' missing required string 'value'"));
        }

        var value = valueEl.GetString()!;

        if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
            docEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"tags[{index}] '{constName}' missing required string 'doc'"));
        }

        var doc = docEl.GetString()!;

        return (new OtelMessagingTagEntry(constName, value, doc), null);
    }
}
