// -----------------------------------------------------------------------
// <copyright file="ProblemDetailsSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ProblemDetails.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for parsing <c>problem-details.spec.json</c> into a
/// <see cref="ProblemDetailsSpec"/> record. Responsible only for JSON-shape
/// validation; semantic validation (constName uniqueness, httpStatus
/// uniqueness, trailing-slash on the URI prefix, etc.) lives in
/// <see cref="ProblemDetailsEmitter"/>.
/// </summary>
internal static class ProblemDetailsSpecLoader
{
    private const string _TYPE_URI_PREFIX_KEY = "typeUriPrefix";
    private const string _CONTENT_TYPE_KEY = "contentType";
    private const string _EXTENSION_KEYS_KEY = "extensionKeys";
    private const string _TITLES_KEY = "titles";
    private const string _CONST_NAME_KEY = "constName";
    private const string _VALUE_KEY = "value";
    private const string _HTTP_STATUS_KEY = "httpStatus";
    private const string _DOC_KEY = "doc";

    /// <summary>
    /// Parses raw JSON spec content into a <see cref="ProblemDetailsSpec"/>.
    /// Returns either a populated spec or a single diagnostic explaining the
    /// parse failure.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>
    /// A <see cref="LoadResult{TSpec}"/> wrapping <see cref="ProblemDetailsSpec"/>.
    /// </returns>
    public static LoadResult<ProblemDetailsSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<ProblemDetailsSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_TYPE_URI_PREFIX_KEY, out var prefixEl) ||
                prefixEl.ValueKind != JsonValueKind.String)
            {
                return new LoadResult<ProblemDetailsSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required string 'typeUriPrefix' property at root"));
            }

            var typeUriPrefix = prefixEl.GetString()!;

            if (!root.TryGetProperty(_CONTENT_TYPE_KEY, out var contentTypeEl) ||
                contentTypeEl.ValueKind != JsonValueKind.String)
            {
                return new LoadResult<ProblemDetailsSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required string 'contentType' property at root"));
            }

            var contentType = contentTypeEl.GetString()!;

            if (!root.TryGetProperty(_EXTENSION_KEYS_KEY, out var extArr) ||
                extArr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<ProblemDetailsSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'extensionKeys' array property at root"));
            }

            if (!root.TryGetProperty(_TITLES_KEY, out var titlesArr) ||
                titlesArr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<ProblemDetailsSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'titles' array property at root"));
            }

            var extensionKeys = ImmutableArray.CreateBuilder<ExtensionKeyEntry>();
            var extIndex = 0;
            foreach (var element in extArr.EnumerateArray())
            {
                var (entry, diag) = ParseExtensionKey(element, fileName, extIndex);
                if (diag is not null)
                    return new LoadResult<ProblemDetailsSpec>(Spec: null, Diagnostic: diag);

                extensionKeys.Add(entry!);
                extIndex++;
            }

            var titles = ImmutableArray.CreateBuilder<TitleEntry>();
            var titleIndex = 0;
            foreach (var element in titlesArr.EnumerateArray())
            {
                var (entry, diag) = ParseTitle(element, fileName, titleIndex);
                if (diag is not null)
                    return new LoadResult<ProblemDetailsSpec>(Spec: null, Diagnostic: diag);

                titles.Add(entry!);
                titleIndex++;
            }

            return new LoadResult<ProblemDetailsSpec>(
                Spec: new ProblemDetailsSpec(
                    typeUriPrefix,
                    contentType,
                    extensionKeys.ToImmutable(),
                    titles.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<ProblemDetailsSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (ExtensionKeyEntry? Entry, EmitDiagnostic? Diagnostic) ParseExtensionKey(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"extensionKeys[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CONST_NAME_KEY, out var nameEl) ||
            nameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"extensionKeys[{index}] missing required string 'constName'"));
        }

        var constName = nameEl.GetString()!;

        if (!element.TryGetProperty(_VALUE_KEY, out var valueEl) ||
            valueEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"extensionKeys[{index}] '{constName}' missing required string 'value'"));
        }

        var value = valueEl.GetString()!;

        if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
            docEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"extensionKeys[{index}] '{constName}' missing required string 'doc'"));
        }

        var doc = docEl.GetString()!;

        return (new ExtensionKeyEntry(constName, value, doc), null);
    }

    private static (TitleEntry? Entry, EmitDiagnostic? Diagnostic) ParseTitle(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"titles[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CONST_NAME_KEY, out var nameEl) ||
            nameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"titles[{index}] missing required string 'constName'"));
        }

        var constName = nameEl.GetString()!;

        if (!element.TryGetProperty(_HTTP_STATUS_KEY, out var statusEl))
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"titles[{index}] '{constName}' missing required 'httpStatus' property "
                    + "(integer or null)"));
        }

        int? httpStatus;
        if (statusEl.ValueKind == JsonValueKind.Null)
        {
            httpStatus = null;
        }
        else if (statusEl.ValueKind == JsonValueKind.Number &&
            statusEl.TryGetInt32(out var parsed))
        {
            httpStatus = parsed;
        }
        else
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"titles[{index}] '{constName}' has non-integer 'httpStatus' "
                    + $"(got {statusEl.ValueKind}); expected integer or null"));
        }

        if (!element.TryGetProperty(_VALUE_KEY, out var valueEl) ||
            valueEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"titles[{index}] '{constName}' missing required string 'value'"));
        }

        var value = valueEl.GetString()!;

        if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
            docEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"titles[{index}] '{constName}' missing required string 'doc'"));
        }

        var doc = docEl.GetString()!;

        return (new TitleEntry(constName, httpStatus, value, doc), null);
    }
}
