// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionDomains.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>Parses <c>encryption-domains.spec.json</c>.</summary>
internal static class EncryptionDomainsSpecLoader
{
    private const string _DOMAINS_KEY = "domains";
    private const string _CONST_NAME_KEY = "constName";
    private const string _VALUE_KEY = "value";
    private const string _DOC_KEY = "doc";
    private const string _MODE_KEY = "mode";
    private const string _CONSUMER_SERVICE_KEY = "consumerService";

    /// <summary>Parses raw JSON spec content into an <see cref="EncryptionDomainsSpec"/>.</summary>
    /// <param name="path">Spec file path.</param>
    /// <param name="json">Raw JSON content.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/>.</returns>
    public static LoadResult<EncryptionDomainsSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<EncryptionDomainsSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_DOMAINS_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<EncryptionDomainsSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'domains' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<EncryptionDomainEntry>();
            var index = 0;
            foreach (var element in arr.EnumerateArray())
            {
                var (entry, diag) = ParseEntry(element, fileName, index);
                if (diag is not null)
                {
                    return new LoadResult<EncryptionDomainsSpec>(
                        Spec: null, Diagnostic: diag);
                }

                entries.Add(entry!);
                index++;
            }

            return new LoadResult<EncryptionDomainsSpec>(
                Spec: new EncryptionDomainsSpec(entries.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<EncryptionDomainsSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (EncryptionDomainEntry? Entry, EmitDiagnostic? Diagnostic) ParseEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"domains[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CONST_NAME_KEY, out var nameEl) ||
            nameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"domains[{index}] missing required string 'constName'"));
        }

        var constName = nameEl.GetString()!;

        if (!element.TryGetProperty(_VALUE_KEY, out var valueEl) ||
            valueEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"domains[{index}] '{constName}' missing required string 'value'"));
        }

        var value = valueEl.GetString()!;

        if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
            docEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"domains[{index}] '{constName}' missing required string 'doc'"));
        }

        var doc = docEl.GetString()!;

        if (element.TryGetProperty(_MODE_KEY, out var modeEl) &&
            modeEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"domains[{index}] '{constName}' 'mode' must be a string when present"));
        }

        var mode = modeEl.ValueKind == JsonValueKind.String ? modeEl.GetString() : null;

        if (element.TryGetProperty(_CONSUMER_SERVICE_KEY, out var consumerEl) &&
            consumerEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"domains[{index}] '{constName}' 'consumerService' must be a string when present"));
        }

        var consumerService =
            consumerEl.ValueKind == JsonValueKind.String ? consumerEl.GetString() : null;

        return (new EncryptionDomainEntry(constName, value, doc, mode, consumerService), null);
    }
}
