// -----------------------------------------------------------------------
// <copyright file="ProtocolAudienceSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.ProtocolAudiences.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for parsing <c>protocol-audiences.spec.json</c> into a
/// <see cref="ProtocolAudiencesSpec"/> record. Responsible only for JSON-shape
/// validation; semantic validation (name shape, empty/duplicate value) lives in
/// <see cref="ProtocolAudiencesEmitter"/>.
/// </summary>
internal static class ProtocolAudienceSpecLoader
{
    private const string _PROTOCOL_AUDIENCES_KEY = "protocolAudiences";
    private const string _NAME_KEY = "name";
    private const string _VALUE_KEY = "value";
    private const string _DESCRIPTION_KEY = "description";

    /// <summary>
    /// Parses raw JSON spec content into a <see cref="ProtocolAudiencesSpec"/>.
    /// Returns either a populated spec or a single diagnostic explaining the parse
    /// failure.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/> wrapping <see cref="ProtocolAudiencesSpec"/>.</returns>
    public static LoadResult<ProtocolAudiencesSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<ProtocolAudiencesSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_PROTOCOL_AUDIENCES_KEY, out var audiencesElement) ||
                audiencesElement.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<ProtocolAudiencesSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'protocolAudiences' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<ProtocolAudienceEntry>();
            var index = 0;
            foreach (var audienceElement in audiencesElement.EnumerateArray())
            {
                var (entry, diag) = ParseEntry(audienceElement, fileName, index);
                if (diag is not null)
                    return new LoadResult<ProtocolAudiencesSpec>(Spec: null, Diagnostic: diag);

                entries.Add(entry!);
                index++;
            }

            return new LoadResult<ProtocolAudiencesSpec>(
                Spec: new ProtocolAudiencesSpec(entries.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<ProtocolAudiencesSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (ProtocolAudienceEntry? Entry, EmitDiagnostic? Diagnostic) ParseEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"protocolAudiences[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_NAME_KEY, out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"protocolAudiences[{index}] missing required string 'name'"));
        }

        var name = nameElement.GetString()!;

        if (!element.TryGetProperty(_VALUE_KEY, out var valueElement) ||
            valueElement.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"protocolAudiences[{index}] '{name}' missing required string 'value'"));
        }

        var value = valueElement.GetString()!;

        string? description = null;
        if (element.TryGetProperty(_DESCRIPTION_KEY, out var descElement) &&
            descElement.ValueKind == JsonValueKind.String)
        {
            description = descElement.GetString();
        }

        var entry = new ProtocolAudienceEntry(
            Name: name,
            Value: value,
            Description: description);

        return (entry, null);
    }
}
