// -----------------------------------------------------------------------
// <copyright file="AdvisoryLocksSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AdvisoryLocks.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>Parses <c>advisory-locks.spec.json</c> into an <see cref="AdvisoryLocksSpec"/>.</summary>
internal static class AdvisoryLocksSpecLoader
{
    private const string _LOCKS_KEY = "locks";
    private const string _CONST_NAME_KEY = "constName";
    private const string _DATABASE_KEY = "database";
    private const string _KEY_KEY = "key";
    private const string _DOC_KEY = "doc";

    /// <summary>
    /// Parses raw JSON spec content into an <see cref="AdvisoryLocksSpec"/>.
    /// </summary>
    /// <param name="path">Spec file path (used in diagnostic messages).</param>
    /// <param name="json">Raw JSON content.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/>.</returns>
    public static LoadResult<AdvisoryLocksSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<AdvisoryLocksSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_LOCKS_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<AdvisoryLocksSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'locks' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<AdvisoryLockEntry>();
            var index = 0;
            foreach (var element in arr.EnumerateArray())
            {
                var (entry, diag) = ParseEntry(element, fileName, index);
                if (diag is not null)
                    return new LoadResult<AdvisoryLocksSpec>(Spec: null, Diagnostic: diag);

                entries.Add(entry!);
                index++;
            }

            return new LoadResult<AdvisoryLocksSpec>(
                Spec: new AdvisoryLocksSpec(entries.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<AdvisoryLocksSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (AdvisoryLockEntry? Entry, EmitDiagnostic? Diagnostic) ParseEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"locks[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CONST_NAME_KEY, out var nameEl) ||
            nameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"locks[{index}] missing required string 'constName'"));
        }

        var constName = nameEl.GetString()!;

        if (!element.TryGetProperty(_DATABASE_KEY, out var dbEl) ||
            dbEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"locks[{index}] '{constName}' missing required string 'database'"));
        }

        var database = dbEl.GetString()!;

        if (!element.TryGetProperty(_KEY_KEY, out var keyEl) ||
            keyEl.ValueKind != JsonValueKind.Number)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"locks[{index}] '{constName}' missing required integer 'key'"));
        }

        if (!keyEl.TryGetInt64(out var keyValue))
        {
            return (null, new EmitDiagnostic(
                DiagnosticIds.KeyOutOfRange,
                [constName, keyEl.GetRawText(), long.MinValue.ToString(), long.MaxValue.ToString()]));
        }

        if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
            docEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"locks[{index}] '{constName}' missing required string 'doc'"));
        }

        var doc = docEl.GetString()!;

        return (new AdvisoryLockEntry(constName, database, keyValue, doc), null);
    }
}
