// -----------------------------------------------------------------------
// <copyright file="ErrorCodesSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ResultErrorCodes.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using D2.Shared.SourceGen;

/// <summary>
/// Pure logic for parsing <c>error-codes.spec.json</c> into an
/// <see cref="ErrorCodesSpec"/> record. Responsible only for JSON-shape
/// validation; semantic validation (uniqueness, HTTP status mapping
/// coverage, code regex shape, etc.) lives in
/// <see cref="ErrorCodesEmitter"/>.
/// </summary>
internal static class ErrorCodesSpecLoader
{
    private const string _ERROR_CODES_KEY = "errorCodes";
    private const string _CODE_KEY = "code";
    private const string _HTTP_STATUS_KEY = "httpStatus";
    private const string _DOC_KEY = "doc";

    /// <summary>
    /// Parses raw JSON spec content into an <see cref="ErrorCodesSpec"/>.
    /// Returns either a populated spec or a single diagnostic explaining the
    /// parse failure.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/> wrapping <see cref="ErrorCodesSpec"/>.</returns>
    public static LoadResult<ErrorCodesSpec> Load(string path, string json)
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
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_ERROR_CODES_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<ErrorCodesSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'errorCodes' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<ErrorCodeEntry>();
            var index = 0;
            foreach (var element in arr.EnumerateArray())
            {
                var (entry, diag) = ParseEntry(element, fileName, index);
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
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (ErrorCodeEntry? Entry, EmitDiagnostic? Diagnostic) ParseEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"errorCodes[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CODE_KEY, out var codeEl) ||
            codeEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"errorCodes[{index}] missing required string 'code'"));
        }

        var code = codeEl.GetString()!;

        if (!element.TryGetProperty(_HTTP_STATUS_KEY, out var statusEl) ||
            statusEl.ValueKind != JsonValueKind.Number ||
            !statusEl.TryGetInt32(out var httpStatus))
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"errorCodes[{index}] '{code}' missing required integer 'httpStatus'"));
        }

        if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
            docEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"errorCodes[{index}] '{code}' missing required string 'doc'"));
        }

        var docText = docEl.GetString()!;

        var entry = new ErrorCodeEntry(
            Code: code,
            HttpStatus: httpStatus,
            Doc: docText);

        return (entry, null);
    }
}
