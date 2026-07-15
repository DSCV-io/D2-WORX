// -----------------------------------------------------------------------
// <copyright file="WireShapeSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.WireShapes.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for parsing a wire-shape spec JSON file into a
/// <see cref="WireShapeSpec"/> record. Responsible only for JSON-shape
/// validation; semantic validation (constName uniqueness, value
/// uniqueness, constName pattern) lives in <see cref="WireShapeEmitter"/>.
/// </summary>
internal static class WireShapeSpecLoader
{
    private const string _PROPERTIES_KEY = "properties";
    private const string _CONST_NAME_KEY = "constName";
    private const string _VALUE_KEY = "value";
    private const string _DOC_KEY = "doc";

    /// <summary>
    /// Parses raw JSON spec content into a <see cref="WireShapeSpec"/>.
    /// Returns either a populated spec or a single diagnostic explaining
    /// the parse failure.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/> wrapping <see cref="WireShapeSpec"/>.</returns>
    public static LoadResult<WireShapeSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<WireShapeSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_PROPERTIES_KEY, out var propsArr) ||
                propsArr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<WireShapeSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'properties' array property at root"));
            }

            var properties = ImmutableArray.CreateBuilder<WireShapeProperty>();
            var index = 0;
            foreach (var element in propsArr.EnumerateArray())
            {
                var (entry, diag) = ParseProperty(element, fileName, index);
                if (diag is not null)
                    return new LoadResult<WireShapeSpec>(Spec: null, Diagnostic: diag);

                properties.Add(entry!);
                index++;
            }

            return new LoadResult<WireShapeSpec>(
                Spec: new WireShapeSpec(properties.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<WireShapeSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (WireShapeProperty? Entry, EmitDiagnostic? Diagnostic) ParseProperty(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"properties[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CONST_NAME_KEY, out var nameEl) ||
            nameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"properties[{index}] missing required string 'constName'"));
        }

        var constName = nameEl.GetString()!;

        if (!element.TryGetProperty(_VALUE_KEY, out var valueEl) ||
            valueEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"properties[{index}] '{constName}' missing required string 'value'"));
        }

        var value = valueEl.GetString()!;

        if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
            docEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"properties[{index}] '{constName}' missing required string 'doc'"));
        }

        var doc = docEl.GetString()!;

        return (new WireShapeProperty(constName, value, doc), null);
    }
}
