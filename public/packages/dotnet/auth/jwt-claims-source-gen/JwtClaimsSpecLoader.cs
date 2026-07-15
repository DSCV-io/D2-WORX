// -----------------------------------------------------------------------
// <copyright file="JwtClaimsSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.JwtClaims.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for parsing <c>jwt-claims.spec.json</c> into a
/// <see cref="JwtClaimsSpec"/> record.
/// </summary>
internal static class JwtClaimsSpecLoader
{
    private const string _CLAIMS_KEY = "claims";
    private const string _CONST_NAME_KEY = "constName";
    private const string _VALUE_KEY = "value";
    private const string _KIND_KEY = "kind";
    private const string _DESCRIPTION_KEY = "description";

    /// <summary>
    /// Parses raw JSON spec content into a <see cref="JwtClaimsSpec"/>.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/> wrapping <see cref="JwtClaimsSpec"/>.</returns>
    public static LoadResult<JwtClaimsSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<JwtClaimsSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_CLAIMS_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<JwtClaimsSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'claims' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<JwtClaimEntry>();
            var index = 0;
            foreach (var element in arr.EnumerateArray())
            {
                var (entry, diag) = ParseEntry(element, fileName, index);
                if (diag is not null)
                    return new LoadResult<JwtClaimsSpec>(Spec: null, Diagnostic: diag);

                entries.Add(entry!);
                index++;
            }

            return new LoadResult<JwtClaimsSpec>(
                Spec: new JwtClaimsSpec(entries.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<JwtClaimsSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (JwtClaimEntry? Entry, EmitDiagnostic? Diagnostic) ParseEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"claims[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CONST_NAME_KEY, out var constNameEl) ||
            constNameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"claims[{index}] missing required string 'constName'"));
        }

        var constName = constNameEl.GetString()!;

        if (!element.TryGetProperty(_VALUE_KEY, out var valueEl) ||
            valueEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"claims[{index}] '{constName}' missing required string 'value'"));
        }

        var value = valueEl.GetString()!;

        if (!element.TryGetProperty(_KIND_KEY, out var kindEl) ||
            kindEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"claims[{index}] '{constName}' missing required string 'kind'"));
        }

        var kind = kindEl.GetString()!;

        if (!element.TryGetProperty(_DESCRIPTION_KEY, out var descEl) ||
            descEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"claims[{index}] '{constName}' missing required string 'description'"));
        }

        var description = descEl.GetString()!;

        var entry = new JwtClaimEntry(
            ConstName: constName,
            Value: value,
            Kind: kind,
            Description: description);

        return (entry, null);
    }
}
