// -----------------------------------------------------------------------
// <copyright file="EncryptionFrameSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionFrame.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>Parses <c>encryption-frame.spec.json</c>.</summary>
internal static class EncryptionFrameSpecLoader
{
    private const string _VERSION_KEY = "version";
    private const string _FIELDS_KEY = "fields";
    private const string _CONSTRAINTS_KEY = "constraints";
    private const string _CONST_NAME_KEY = "constName";
    private const string _OFFSET_KEY = "offset";
    private const string _LENGTH_KEY = "length";
    private const string _KIND_KEY = "kind";
    private const string _DOC_KEY = "doc";

    /// <summary>Parses raw JSON spec content into an <see cref="EncryptionFrameSpec"/>.</summary>
    /// <param name="path">Spec file path.</param>
    /// <param name="json">Raw JSON content.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/>.</returns>
    public static LoadResult<EncryptionFrameSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<EncryptionFrameSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_VERSION_KEY, out var versionEl) ||
                versionEl.ValueKind != JsonValueKind.Number ||
                !versionEl.TryGetInt32(out var version))
            {
                return new LoadResult<EncryptionFrameSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required integer 'version' property at root"));
            }

            if (!root.TryGetProperty(_FIELDS_KEY, out var fieldsArr) ||
                fieldsArr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<EncryptionFrameSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'fields' array property at root"));
            }

            if (!root.TryGetProperty(_CONSTRAINTS_KEY, out var constraintsEl) ||
                constraintsEl.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<EncryptionFrameSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'constraints' object property at root"));
            }

            var fields = ImmutableArray.CreateBuilder<EncryptionFrameField>();
            var index = 0;
            foreach (var element in fieldsArr.EnumerateArray())
            {
                var (entry, diag) = ParseField(element, fileName, index);
                if (diag is not null)
                {
                    return new LoadResult<EncryptionFrameSpec>(
                        Spec: null, Diagnostic: diag);
                }

                fields.Add(entry!);
                index++;
            }

            var (constraints, constraintsDiag) =
                ParseConstraints(constraintsEl, fileName);
            if (constraintsDiag is not null)
            {
                return new LoadResult<EncryptionFrameSpec>(
                    Spec: null, Diagnostic: constraintsDiag);
            }

            return new LoadResult<EncryptionFrameSpec>(
                Spec: new EncryptionFrameSpec(version, fields.ToImmutable(), constraints!),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<EncryptionFrameSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (EncryptionFrameField? Entry, EmitDiagnostic? Diagnostic) ParseField(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"fields[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_CONST_NAME_KEY, out var nameEl) ||
            nameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"fields[{index}] missing required string 'constName'"));
        }

        var constName = nameEl.GetString()!;

        if (!element.TryGetProperty(_OFFSET_KEY, out var offsetEl) ||
            offsetEl.ValueKind != JsonValueKind.Number ||
            !offsetEl.TryGetInt32(out var offset))
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"fields[{index}] '{constName}' missing required integer 'offset'"));
        }

        if (!element.TryGetProperty(_LENGTH_KEY, out var lengthEl) ||
            lengthEl.ValueKind != JsonValueKind.Number ||
            !lengthEl.TryGetInt32(out var length))
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"fields[{index}] '{constName}' missing required integer 'length'"));
        }

        if (!element.TryGetProperty(_KIND_KEY, out var kindEl) ||
            kindEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"fields[{index}] '{constName}' missing required string 'kind'"));
        }

        var kind = kindEl.GetString()!;

        if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
            docEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"fields[{index}] '{constName}' missing required string 'doc'"));
        }

        var doc = docEl.GetString()!;

        return (new EncryptionFrameField(constName, offset, length, kind, doc), null);
    }

    private static (EncryptionFrameConstraints? Constraints, EmitDiagnostic? Diagnostic)
        ParseConstraints(JsonElement el, string fileName)
    {
        if (!TryReadInt(el, "minKidLength", out var minKid))
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName, "constraints missing required integer 'minKidLength'"));
        }

        if (!TryReadInt(el, "maxKidLength", out var maxKid))
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName, "constraints missing required integer 'maxKidLength'"));
        }

        if (!TryReadInt(el, "nonceLength", out var nonce))
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName, "constraints missing required integer 'nonceLength'"));
        }

        if (!TryReadInt(el, "tagLength", out var tag))
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName, "constraints missing required integer 'tagLength'"));
        }

        if (!TryReadInt(el, "minFrameSize", out var minFrame))
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName, "constraints missing required integer 'minFrameSize'"));
        }

        return (new EncryptionFrameConstraints(minKid, maxKid, nonce, tag, minFrame), null);
    }

    private static bool TryReadInt(JsonElement obj, string key, out int value)
    {
        value = 0;
        if (!obj.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number)
            return false;
        return el.TryGetInt32(out value);
    }
}
