// -----------------------------------------------------------------------
// <copyright file="D2ResultEnvelopeSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result.Envelope.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>Parses <c>d2result-envelope.spec.json</c>.</summary>
internal static class D2ResultEnvelopeSpecLoader
{
    private const string _FIELDS_KEY = "fields";
    private const string _CONST_NAME_KEY = "constName";
    private const string _VALUE_KEY = "value";
    private const string _DOC_KEY = "doc";

    /// <summary>Parses raw JSON spec content into a <see cref="D2ResultEnvelopeSpec"/>.</summary>
    /// <param name="path">Spec file path.</param>
    /// <param name="json">Raw JSON content.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/>.</returns>
    public static LoadResult<D2ResultEnvelopeSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<D2ResultEnvelopeSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_FIELDS_KEY, out var fieldsArr) ||
                fieldsArr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<D2ResultEnvelopeSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'fields' array property at root"));
            }

            var fields = ImmutableArray.CreateBuilder<D2ResultEnvelopeFieldEntry>();
            var fieldIndex = 0;
            foreach (var element in fieldsArr.EnumerateArray())
            {
                var (entry, diag) = ParseFieldEntry(element, fileName, fieldIndex);
                if (diag is not null)
                {
                    return new LoadResult<D2ResultEnvelopeSpec>(
                        Spec: null, Diagnostic: diag);
                }

                fields.Add(entry!);
                fieldIndex++;
            }

            return new LoadResult<D2ResultEnvelopeSpec>(
                Spec: new D2ResultEnvelopeSpec(fields.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<D2ResultEnvelopeSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (D2ResultEnvelopeFieldEntry? Entry, EmitDiagnostic? Diagnostic) ParseFieldEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"fields[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CONST_NAME_KEY, out var nameEl) ||
            nameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"fields[{index}] missing required string 'constName'"));
        }

        var constName = nameEl.GetString()!;

        if (!element.TryGetProperty(_VALUE_KEY, out var valueEl) ||
            valueEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"fields[{index}] '{constName}' missing required string 'value'"));
        }

        var value = valueEl.GetString()!;

        if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
            docEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"fields[{index}] '{constName}' missing required string 'doc'"));
        }

        var doc = docEl.GetString()!;

        return (new D2ResultEnvelopeFieldEntry(constName, value, doc), null);
    }
}
