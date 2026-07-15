// -----------------------------------------------------------------------
// <copyright file="AudienceSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Audiences.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for parsing <c>audiences.spec.json</c> into an
/// <see cref="AudiencesSpec"/> record. Responsible only for JSON-shape
/// validation; semantic validation (name shape, URL parsing, duplicates)
/// lives in <see cref="AudiencesEmitter"/>.
/// </summary>
internal static class AudienceSpecLoader
{
    private const string _AUDIENCES_KEY = "audiences";
    private const string _NAME_KEY = "name";
    private const string _URL_KEY = "url";
    private const string _DESCRIPTION_KEY = "description";

    /// <summary>
    /// Parses raw JSON spec content into an <see cref="AudiencesSpec"/>. Returns
    /// either a populated spec or a single diagnostic explaining the parse failure.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/> wrapping <see cref="AudiencesSpec"/>.</returns>
    public static LoadResult<AudiencesSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<AudiencesSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_AUDIENCES_KEY, out var audiencesElement) ||
                audiencesElement.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<AudiencesSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'audiences' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<AudienceEntry>();
            var index = 0;
            foreach (var audienceElement in audiencesElement.EnumerateArray())
            {
                var (entry, diag) = ParseAudienceEntry(audienceElement, fileName, index);
                if (diag is not null)
                    return new LoadResult<AudiencesSpec>(Spec: null, Diagnostic: diag);

                entries.Add(entry!);
                index++;
            }

            return new LoadResult<AudiencesSpec>(
                Spec: new AudiencesSpec(entries.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<AudiencesSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (AudienceEntry? Entry, EmitDiagnostic? Diagnostic) ParseAudienceEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"audiences[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_NAME_KEY, out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"audiences[{index}] missing required string 'name'"));
        }

        var name = nameElement.GetString()!;

        if (!element.TryGetProperty(_URL_KEY, out var urlElement) ||
            urlElement.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"audiences[{index}] '{name}' missing required string 'url'"));
        }

        var url = urlElement.GetString()!;

        string? description = null;
        if (element.TryGetProperty(_DESCRIPTION_KEY, out var descElement) &&
            descElement.ValueKind == JsonValueKind.String)
        {
            description = descElement.GetString();
        }

        var entry = new AudienceEntry(
            Name: name,
            Url: url,
            Description: description);

        return (entry, null);
    }
}
