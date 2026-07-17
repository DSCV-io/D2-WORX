// -----------------------------------------------------------------------
// <copyright file="DlqFailureMetadataSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.DlqMetadata.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>Parses <c>dlq-failure-metadata.spec.json</c>.</summary>
internal static class DlqFailureMetadataSpecLoader
{
    private const string _FIELDS_KEY = "fields";
    private const string _CAUSES_KEY = "causes";
    private const string _CONST_NAME_KEY = "constName";
    private const string _VALUE_KEY = "value";
    private const string _DOC_KEY = "doc";

    /// <summary>Parses raw JSON spec content into a <see cref="DlqFailureMetadataSpec"/>.</summary>
    /// <param name="path">Spec file path.</param>
    /// <param name="json">Raw JSON content.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/>.</returns>
    public static LoadResult<DlqFailureMetadataSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<DlqFailureMetadataSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_FIELDS_KEY, out var fieldsArr) ||
                fieldsArr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<DlqFailureMetadataSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'fields' array property at root"));
            }

            if (!root.TryGetProperty(_CAUSES_KEY, out var causesArr) ||
                causesArr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<DlqFailureMetadataSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'causes' array property at root"));
            }

            var fields = ImmutableArray.CreateBuilder<DlqFieldEntry>();
            var fieldIndex = 0;
            foreach (var element in fieldsArr.EnumerateArray())
            {
                var (entry, diag) = ParseFieldEntry(element, fileName, fieldIndex);
                if (diag is not null)
                {
                    return new LoadResult<DlqFailureMetadataSpec>(
                        Spec: null, Diagnostic: diag);
                }

                fields.Add(entry!);
                fieldIndex++;
            }

            var causes = ImmutableArray.CreateBuilder<DlqCauseEntry>();
            var causeIndex = 0;
            foreach (var element in causesArr.EnumerateArray())
            {
                var (entry, diag) = ParseCauseEntry(element, fileName, causeIndex);
                if (diag is not null)
                {
                    return new LoadResult<DlqFailureMetadataSpec>(
                        Spec: null, Diagnostic: diag);
                }

                causes.Add(entry!);
                causeIndex++;
            }

            return new LoadResult<DlqFailureMetadataSpec>(
                Spec: new DlqFailureMetadataSpec(fields.ToImmutable(), causes.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<DlqFailureMetadataSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (DlqFieldEntry? Entry, EmitDiagnostic? Diagnostic) ParseFieldEntry(
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

        return (new DlqFieldEntry(constName, value, doc), null);
    }

    private static (DlqCauseEntry? Entry, EmitDiagnostic? Diagnostic) ParseCauseEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"causes[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CONST_NAME_KEY, out var nameEl) ||
            nameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"causes[{index}] missing required string 'constName'"));
        }

        var constName = nameEl.GetString()!;

        if (!element.TryGetProperty(_VALUE_KEY, out var valueEl) ||
            valueEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"causes[{index}] '{constName}' missing required string 'value'"));
        }

        var value = valueEl.GetString()!;

        if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
            docEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"causes[{index}] '{constName}' missing required string 'doc'"));
        }

        var doc = docEl.GetString()!;

        return (new DlqCauseEntry(constName, value, doc), null);
    }
}
