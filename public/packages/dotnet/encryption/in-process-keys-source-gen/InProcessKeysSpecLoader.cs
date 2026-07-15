// -----------------------------------------------------------------------
// <copyright file="InProcessKeysSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.InProcessKeys.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for parsing <c>keys.spec.json</c> into an
/// <see cref="InProcessKeysSpec"/> record. Responsible only for JSON-shape
/// validation; semantic validation lives in
/// <see cref="InProcessKeysEmitter"/>.
/// </summary>
internal static class InProcessKeysSpecLoader
{
    private const string _KEYS_KEY = "keys";
    private const string _CONST_NAME_KEY = "constName";
    private const string _VALUE_KEY = "value";
    private const string _PURPOSE_KEY = "purpose";
    private const string _BINDINGS_KEY = "bindings";

    /// <summary>
    /// Parses raw JSON spec content into an <see cref="InProcessKeysSpec"/>.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>
    /// A <see cref="LoadResult{TSpec}"/> wrapping <see cref="InProcessKeysSpec"/>.
    /// </returns>
    public static LoadResult<InProcessKeysSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<InProcessKeysSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_KEYS_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<InProcessKeysSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'keys' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<KeyEntry>();
            var index = 0;
            foreach (var element in arr.EnumerateArray())
            {
                var (entry, diag) = ParseEntry(element, fileName, index);
                if (diag is not null)
                    return new LoadResult<InProcessKeysSpec>(Spec: null, Diagnostic: diag);

                entries.Add(entry!);
                index++;
            }

            return new LoadResult<InProcessKeysSpec>(
                Spec: new InProcessKeysSpec(entries.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<InProcessKeysSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (KeyEntry? Entry, EmitDiagnostic? Diagnostic) ParseEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"keys[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CONST_NAME_KEY, out var constNameEl) ||
            constNameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"keys[{index}] missing required string 'constName'"));
        }

        var constName = constNameEl.GetString()!;

        if (!element.TryGetProperty(_VALUE_KEY, out var valueEl) ||
            valueEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"keys[{index}] '{constName}' missing required string 'value'"));
        }

        var value = valueEl.GetString()!;

        if (!element.TryGetProperty(_PURPOSE_KEY, out var purposeEl) ||
            purposeEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"keys[{index}] '{constName}' missing required string 'purpose'"));
        }

        var purpose = purposeEl.GetString()!;

        if (!element.TryGetProperty(_BINDINGS_KEY, out var bindingsEl) ||
            bindingsEl.ValueKind != JsonValueKind.Array)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"keys[{index}] '{constName}' missing required array 'bindings'"));
        }

        var bindingsBuilder = ImmutableArray.CreateBuilder<string>();
        foreach (var item in bindingsEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return (null, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"keys[{index}] '{constName}' bindings item must be a string"));
            }

            bindingsBuilder.Add(item.GetString()!);
        }

        var entry = new KeyEntry(
            ConstName: constName,
            Value: value,
            Purpose: purpose,
            Bindings: bindingsBuilder.ToImmutable());

        return (entry, null);
    }
}
