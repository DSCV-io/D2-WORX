// -----------------------------------------------------------------------
// <copyright file="HeadersSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Headers.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for parsing <c>headers.spec.json</c> into a
/// <see cref="HeadersSpec"/> record. Responsible only for JSON-shape
/// validation; semantic validation (transport closed-enum, applicability
/// non-empty, constName pattern, per-catalog uniqueness) lives in
/// <see cref="HeadersEmitter"/>.
/// </summary>
internal static class HeadersSpecLoader
{
    private const string _HEADERS_KEY = "headers";
    private const string _NAME_KEY = "name";
    private const string _CONST_NAME_KEY = "constName";
    private const string _APPLICABILITY_KEY = "applicability";
    private const string _CONVENTION_KEY = "convention";
    private const string _DESCRIPTION_KEY = "description";

    /// <summary>
    /// Parses raw JSON spec content into a <see cref="HeadersSpec"/>.
    /// Returns either a populated spec or a single diagnostic explaining the
    /// parse failure.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/> wrapping <see cref="HeadersSpec"/>.</returns>
    public static LoadResult<HeadersSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<HeadersSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_HEADERS_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<HeadersSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'headers' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<HeaderEntry>();
            var index = 0;
            foreach (var element in arr.EnumerateArray())
            {
                var (entry, diag) = ParseEntry(element, fileName, index);
                if (diag is not null)
                    return new LoadResult<HeadersSpec>(Spec: null, Diagnostic: diag);

                entries.Add(entry!);
                index++;
            }

            return new LoadResult<HeadersSpec>(
                Spec: new HeadersSpec(entries.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<HeadersSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (HeaderEntry? Entry, EmitDiagnostic? Diagnostic) ParseEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"headers[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_NAME_KEY, out var nameEl) ||
            nameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"headers[{index}] missing required string 'name'"));
        }

        var name = nameEl.GetString()!;

        if (!element.TryGetProperty(_CONST_NAME_KEY, out var constNameEl) ||
            constNameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"headers[{index}] '{name}' missing required string 'constName'"));
        }

        var constName = constNameEl.GetString()!;

        if (!element.TryGetProperty(_APPLICABILITY_KEY, out var applEl) ||
            applEl.ValueKind != JsonValueKind.Array)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"headers[{index}] '{name}' missing required array 'applicability'"));
        }

        var applBuilder = ImmutableArray.CreateBuilder<string>();
        foreach (var item in applEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return (null, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"headers[{index}] '{name}' applicability item must be a string"));
            }

            applBuilder.Add(item.GetString()!);
        }

        if (!element.TryGetProperty(_CONVENTION_KEY, out var convEl) ||
            convEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"headers[{index}] '{name}' missing required string 'convention'"));
        }

        var convention = convEl.GetString()!;

        if (!element.TryGetProperty(_DESCRIPTION_KEY, out var descEl) ||
            descEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"headers[{index}] '{name}' missing required string 'description'"));
        }

        var description = descEl.GetString()!;

        var entry = new HeaderEntry(
            Name: name,
            ConstName: constName,
            Applicability: applBuilder.ToImmutable(),
            Convention: convention,
            Description: description);

        return (entry, null);
    }
}
