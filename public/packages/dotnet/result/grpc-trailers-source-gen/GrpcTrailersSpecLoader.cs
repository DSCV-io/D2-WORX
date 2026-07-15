// -----------------------------------------------------------------------
// <copyright file="GrpcTrailersSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Grpc.Trailers.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using D2.Shared.SourceGen;

/// <summary>
/// Pure logic for parsing <c>grpc-trailers.spec.json</c> into a
/// <see cref="GrpcTrailersSpec"/> record. Responsible only for JSON-shape
/// validation; semantic validation (constName uniqueness, value uniqueness,
/// constName pattern) lives in <see cref="GrpcTrailersEmitter"/>.
/// </summary>
internal static class GrpcTrailersSpecLoader
{
    private const string _TRAILERS_KEY = "trailers";
    private const string _CONST_NAME_KEY = "constName";
    private const string _VALUE_KEY = "value";
    private const string _DOC_KEY = "doc";

    /// <summary>
    /// Parses raw JSON spec content into a <see cref="GrpcTrailersSpec"/>.
    /// Returns either a populated spec or a single diagnostic explaining the
    /// parse failure.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>
    /// A <see cref="LoadResult{TSpec}"/> wrapping <see cref="GrpcTrailersSpec"/>.
    /// </returns>
    public static LoadResult<GrpcTrailersSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<GrpcTrailersSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_TRAILERS_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<GrpcTrailersSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'trailers' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<GrpcTrailerEntry>();
            var index = 0;
            foreach (var element in arr.EnumerateArray())
            {
                var (entry, diag) = ParseEntry(element, fileName, index);
                if (diag is not null)
                {
                    return new LoadResult<GrpcTrailersSpec>(Spec: null, Diagnostic: diag);
                }

                entries.Add(entry!);
                index++;
            }

            return new LoadResult<GrpcTrailersSpec>(
                Spec: new GrpcTrailersSpec(entries.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<GrpcTrailersSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (GrpcTrailerEntry? Entry, EmitDiagnostic? Diagnostic) ParseEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"trailers[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CONST_NAME_KEY, out var nameEl) ||
            nameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"trailers[{index}] missing required string 'constName'"));
        }

        var constName = nameEl.GetString()!;

        if (!element.TryGetProperty(_VALUE_KEY, out var valueEl) ||
            valueEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"trailers[{index}] '{constName}' missing required string 'value'"));
        }

        var value = valueEl.GetString()!;

        if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
            docEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"trailers[{index}] '{constName}' missing required string 'doc'"));
        }

        var doc = docEl.GetString()!;

        return (new GrpcTrailerEntry(constName, value, doc), null);
    }
}
