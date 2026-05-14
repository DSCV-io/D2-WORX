// -----------------------------------------------------------------------
// <copyright file="ErrorCodeSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.ErrorCodes.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;

/// <summary>
/// Pure logic for parsing <c>auth-error-codes.spec.json</c> into an
/// <see cref="ErrorCodesSpec"/> record. Responsible only for JSON-shape
/// validation; semantic validation (category enum membership, uniqueness,
/// HTTP status mapping coverage, etc.) lives in
/// <see cref="ErrorCodesEmitter"/>.
/// </summary>
internal static class ErrorCodeSpecLoader
{
    private const string _ERROR_CODES_KEY = "errorCodes";
    private const string _CODE_KEY = "code";
    private const string _HTTP_STATUS_KEY = "httpStatus";
    private const string _CATEGORY_KEY = "category";
    private const string _USER_MESSAGE_KEY_KEY = "userMessageKey";
    private const string _FACTORY_NAME_KEY = "factoryName";
    private const string _DOC_KEY = "doc";

    /// <summary>
    /// Parses raw JSON spec content into an <see cref="ErrorCodesSpec"/>.
    /// Returns either a populated spec or a single diagnostic explaining the
    /// parse failure.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>A <see cref="LoadResult"/>.</returns>
    public static LoadResult Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult(
                    Spec: null,
                    Diagnostic: EmitDiagnostic.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_ERROR_CODES_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult(
                    Spec: null,
                    Diagnostic: EmitDiagnostic.MalformedSpec(
                        fileName,
                        "missing required 'errorCodes' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<ErrorCodeEntry>();
            var index = 0;
            foreach (var element in arr.EnumerateArray())
            {
                var (entry, diag) = ParseEntry(element, fileName, index);
                if (diag is not null)
                    return new LoadResult(Spec: null, Diagnostic: diag);

                entries.Add(entry!);
                index++;
            }

            return new LoadResult(
                Spec: new ErrorCodesSpec(entries.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult(
                Spec: null,
                Diagnostic: EmitDiagnostic.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (ErrorCodeEntry? Entry, EmitDiagnostic? Diagnostic) ParseEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostic.MalformedSpec(
                fileName,
                $"errorCodes[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CODE_KEY, out var codeEl) ||
            codeEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostic.MalformedSpec(
                fileName,
                $"errorCodes[{index}] missing required string 'code'"));
        }

        var code = codeEl.GetString()!;

        if (!element.TryGetProperty(_HTTP_STATUS_KEY, out var statusEl) ||
            statusEl.ValueKind != JsonValueKind.Number ||
            !statusEl.TryGetInt32(out var httpStatus))
        {
            return (null, EmitDiagnostic.MalformedSpec(
                fileName,
                $"errorCodes[{index}] '{code}' missing required integer 'httpStatus'"));
        }

        if (!element.TryGetProperty(_CATEGORY_KEY, out var categoryEl) ||
            categoryEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostic.MalformedSpec(
                fileName,
                $"errorCodes[{index}] '{code}' missing required string 'category'"));
        }

        var category = categoryEl.GetString()!;

        if (!element.TryGetProperty(_USER_MESSAGE_KEY_KEY, out var msgKeyEl) ||
            msgKeyEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostic.MalformedSpec(
                fileName,
                $"errorCodes[{index}] '{code}' missing required string 'userMessageKey'"));
        }

        var userMessageKey = msgKeyEl.GetString()!;

        if (!element.TryGetProperty(_FACTORY_NAME_KEY, out var factoryEl) ||
            factoryEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostic.MalformedSpec(
                fileName,
                $"errorCodes[{index}] '{code}' missing required string 'factoryName'"));
        }

        var factoryName = factoryEl.GetString()!;

        if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
            docEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostic.MalformedSpec(
                fileName,
                $"errorCodes[{index}] '{code}' missing required string 'doc'"));
        }

        var docText = docEl.GetString()!;

        var entry = new ErrorCodeEntry(
            Code: code,
            HttpStatus: httpStatus,
            Category: category,
            UserMessageKey: userMessageKey,
            FactoryName: factoryName,
            Doc: docText);

        return (entry, null);
    }
}
