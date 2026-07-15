// -----------------------------------------------------------------------
// <copyright file="ErrorCodeSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Shared logic for parsing a <c>*-error-codes.spec.json</c> catalog into an
/// <see cref="ErrorCodesSpec"/>. Responsible only for JSON-shape validation
/// of the three always-present fields (<c>code</c> / <c>httpStatus</c> /
/// <c>doc</c>); the four factory fields (<c>category</c> /
/// <c>userMessageKey</c> / <c>factoryName</c> / <c>factoryShape</c>) are
/// optional — present on factory-bearing catalogs (auth), absent on the
/// generic constants-only catalog. Semantic validation (uniqueness, code
/// regex, category enum, status coverage, etc.) lives in
/// <see cref="ConstantsEmitter"/>.
/// </summary>
/// <remarks>
/// The loader takes the catalog's <see cref="CatalogConfig.MalformedSpecId"/>
/// so the malformed-spec diagnostic carries the catalog's own id
/// (<c>D2EC001</c> / <c>D2AEC001</c>) — preserving the pre-existing
/// per-catalog diagnostic-id families.
/// </remarks>
internal static class ErrorCodeSpecLoader
{
    private const string _ERROR_CODES_KEY = "errorCodes";
    private const string _CODE_KEY = "code";
    private const string _HTTP_STATUS_KEY = "httpStatus";
    private const string _CATEGORY_KEY = "category";
    private const string _USER_MESSAGE_KEY_KEY = "userMessageKey";
    private const string _FACTORY_NAME_KEY = "factoryName";
    private const string _FACTORY_SHAPE_KEY = "factoryShape";
    private const string _DOC_KEY = "doc";
    private const string _DEPRECATED_KEY = "deprecated";
    private const string _DEPRECATED_REASON_KEY = "deprecatedReason";
    private const string _REPLACED_BY_KEY = "replacedBy";
    private const string _SUNSET_KEY = "sunset";

    /// <summary>
    /// Parses raw JSON spec content into an <see cref="ErrorCodesSpec"/>.
    /// Returns either a populated spec or a single diagnostic explaining the
    /// parse failure.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <param name="malformedSpecId">
    /// The catalog's malformed-spec diagnostic id (<c>D2EC001</c> /
    /// <c>D2AEC001</c>).
    /// </param>
    /// <returns>A <see cref="LoadResult{TSpec}"/> wrapping <see cref="ErrorCodesSpec"/>.</returns>
    public static LoadResult<ErrorCodesSpec> Load(string path, string json, string malformedSpecId)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<ErrorCodesSpec>(
                    Spec: null,
                    Diagnostic: Malformed(
                        malformedSpecId,
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_ERROR_CODES_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<ErrorCodesSpec>(
                    Spec: null,
                    Diagnostic: Malformed(
                        malformedSpecId,
                        fileName,
                        "missing required 'errorCodes' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<ErrorCodeEntry>();
            var index = 0;
            foreach (var element in arr.EnumerateArray())
            {
                var (entry, diag) = ParseEntry(element, fileName, index, malformedSpecId);
                if (diag is not null)
                    return new LoadResult<ErrorCodesSpec>(Spec: null, Diagnostic: diag);

                entries.Add(entry!);
                index++;
            }

            return new LoadResult<ErrorCodesSpec>(
                Spec: new ErrorCodesSpec(entries.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<ErrorCodesSpec>(
                Spec: null,
                Diagnostic: Malformed(malformedSpecId, fileName, ex.Message));
        }
    }

    private static EmitDiagnostic Malformed(string id, string fileName, string reason) =>
        new(id, [fileName, reason]);

    private static (ErrorCodeEntry? Entry, EmitDiagnostic? Diagnostic) ParseEntry(
        JsonElement element, string fileName, int index, string malformedSpecId)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, Malformed(
                malformedSpecId,
                fileName,
                $"errorCodes[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CODE_KEY, out var codeEl) ||
            codeEl.ValueKind != JsonValueKind.String)
        {
            return (null, Malformed(
                malformedSpecId,
                fileName,
                $"errorCodes[{index}] missing required string 'code'"));
        }

        var code = codeEl.GetString()!;

        if (!element.TryGetProperty(_HTTP_STATUS_KEY, out var statusEl) ||
            statusEl.ValueKind != JsonValueKind.Number ||
            !statusEl.TryGetInt32(out var httpStatus))
        {
            return (null, Malformed(
                malformedSpecId,
                fileName,
                $"errorCodes[{index}] '{code}' missing required integer 'httpStatus'"));
        }

        if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
            docEl.ValueKind != JsonValueKind.String)
        {
            return (null, Malformed(
                malformedSpecId,
                fileName,
                $"errorCodes[{index}] '{code}' missing required string 'doc'"));
        }

        var docText = docEl.GetString()!;

        // Factory fields are optional — present on factory-bearing catalogs
        // (auth), absent on the generic constants-only catalog.
        var category = OptionalString(element, _CATEGORY_KEY);
        var userMessageKey = OptionalString(element, _USER_MESSAGE_KEY_KEY);
        var factoryName = OptionalString(element, _FACTORY_NAME_KEY);
        var factoryShape = OptionalString(element, _FACTORY_SHAPE_KEY);

        // Deprecation marker — optional + additive. The schema allows but never
        // requires these; absence means the entry is active. Only ever true.
        var deprecated = OptionalBool(element, _DEPRECATED_KEY);
        var deprecatedReason = OptionalString(element, _DEPRECATED_REASON_KEY);
        var replacedBy = OptionalString(element, _REPLACED_BY_KEY);
        var sunset = OptionalString(element, _SUNSET_KEY);

        var entry = new ErrorCodeEntry(
            Code: code,
            HttpStatus: httpStatus,
            Doc: docText,
            Category: category,
            UserMessageKey: userMessageKey,
            FactoryName: factoryName,
            FactoryShape: factoryShape,
            Deprecated: deprecated,
            DeprecatedReason: deprecatedReason,
            ReplacedBy: replacedBy,
            Sunset: sunset);

        return (entry, null);
    }

    private static string? OptionalString(JsonElement element, string key) =>
        element.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;

    private static bool OptionalBool(JsonElement element, string key) =>
        element.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.True;
}
