// -----------------------------------------------------------------------
// <copyright file="FieldConstraintsSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for parsing <c>field-constraints.spec.json</c> into a
/// <see cref="FieldConstraintsSpec"/> record. Responsible only for JSON-shape
/// validation; semantic validation (name uniqueness, SCREAMING_SNAKE shape,
/// positive value, enum-member identifier shape, etc.) lives in
/// <see cref="FieldConstraintsEmitter"/>.
/// </summary>
internal static class FieldConstraintsSpecLoader
{
    private const string _CONSTRAINTS_KEY = "constraints";
    private const string _ENUMS_KEY = "enums";
    private const string _MEMBERS_KEY = "members";
    private const string _NAME_KEY = "name";
    private const string _VALUE_KEY = "value";
    private const string _DOC_KEY = "doc";
    private const string _BACKING_KEY = "backing";

    // The only backing type the emitter supports (closed lists are small) —
    // mirrors the schema's `backing` enum and the emitter's hardcoded `: byte`.
    private const string _SUPPORTED_BACKING = "byte";

    /// <summary>
    /// Parses raw JSON spec content into a <see cref="FieldConstraintsSpec"/>.
    /// Returns either a populated spec or a single diagnostic explaining the
    /// parse failure.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>
    /// A <see cref="LoadResult{TSpec}"/> wrapping <see cref="FieldConstraintsSpec"/>.
    /// </returns>
    public static LoadResult<FieldConstraintsSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<FieldConstraintsSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_CONSTRAINTS_KEY, out var constraintsArr) ||
                constraintsArr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<FieldConstraintsSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'constraints' array property at root"));
            }

            if (!root.TryGetProperty(_ENUMS_KEY, out var enumsArr) ||
                enumsArr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<FieldConstraintsSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'enums' array property at root"));
            }

            var (constraints, constraintsDiag) = ParseConstraints(constraintsArr, fileName);
            if (constraintsDiag is not null)
            {
                return new LoadResult<FieldConstraintsSpec>(
                    Spec: null, Diagnostic: constraintsDiag);
            }

            var (enums, enumsDiag) = ParseEnums(enumsArr, fileName);
            if (enumsDiag is not null)
                return new LoadResult<FieldConstraintsSpec>(Spec: null, Diagnostic: enumsDiag);

            return new LoadResult<FieldConstraintsSpec>(
                Spec: new FieldConstraintsSpec(constraints, enums),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<FieldConstraintsSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (ImmutableArray<ConstraintEntry> Entries, EmitDiagnostic? Diagnostic)
        ParseConstraints(JsonElement arr, string fileName)
    {
        var entries = ImmutableArray.CreateBuilder<ConstraintEntry>();
        var index = 0;
        foreach (var element in arr.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return (default, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"constraints[{index}] must be a JSON object, got {element.ValueKind}"));
            }

            if (!element.TryGetProperty(_NAME_KEY, out var nameEl) ||
                nameEl.ValueKind != JsonValueKind.String)
            {
                return (default, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"constraints[{index}] missing required string 'name'"));
            }

            var name = nameEl.GetString()!;

            if (!element.TryGetProperty(_VALUE_KEY, out var valueEl) ||
                valueEl.ValueKind != JsonValueKind.Number ||
                !valueEl.TryGetInt32(out var value))
            {
                return (default, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"constraints[{index}] '{name}' missing required integer 'value'"));
            }

            if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
                docEl.ValueKind != JsonValueKind.String)
            {
                return (default, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"constraints[{index}] '{name}' missing required string 'doc'"));
            }

            entries.Add(new ConstraintEntry(name, value, docEl.GetString()!));
            index++;
        }

        return (entries.ToImmutable(), null);
    }

    private static (ImmutableArray<EnumEntry> Entries, EmitDiagnostic? Diagnostic)
        ParseEnums(JsonElement arr, string fileName)
    {
        var entries = ImmutableArray.CreateBuilder<EnumEntry>();
        var index = 0;
        foreach (var element in arr.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return (default, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"enums[{index}] must be a JSON object, got {element.ValueKind}"));
            }

            if (!element.TryGetProperty(_NAME_KEY, out var nameEl) ||
                nameEl.ValueKind != JsonValueKind.String)
            {
                return (default, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"enums[{index}] missing required string 'name'"));
            }

            var name = nameEl.GetString()!;

            if (!element.TryGetProperty(_BACKING_KEY, out var backingEl) ||
                backingEl.ValueKind != JsonValueKind.String)
            {
                return (default, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"enums[{index}] '{name}' missing required string 'backing'"));
            }

            var backing = backingEl.GetString()!;
            if (!string.Equals(backing, _SUPPORTED_BACKING, System.StringComparison.Ordinal))
            {
                return (default, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"enums[{index}] '{name}' has unsupported backing '{backing}' "
                    + $"(only '{_SUPPORTED_BACKING}' is supported)"));
            }

            if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
                docEl.ValueKind != JsonValueKind.String)
            {
                return (default, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"enums[{index}] '{name}' missing required string 'doc'"));
            }

            if (!element.TryGetProperty(_MEMBERS_KEY, out var membersArr) ||
                membersArr.ValueKind != JsonValueKind.Array)
            {
                return (default, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"enums[{index}] '{name}' missing required 'members' array"));
            }

            var (members, membersDiag) = ParseMembers(membersArr, fileName, name);
            if (membersDiag is not null)
                return (default, membersDiag);

            entries.Add(new EnumEntry(name, docEl.GetString()!, members));
            index++;
        }

        return (entries.ToImmutable(), null);
    }

    private static (ImmutableArray<EnumMemberEntry> Members, EmitDiagnostic? Diagnostic)
        ParseMembers(JsonElement arr, string fileName, string enumName)
    {
        var members = ImmutableArray.CreateBuilder<EnumMemberEntry>();
        var index = 0;
        foreach (var element in arr.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return (default, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"enums '{enumName}' members[{index}] must be a JSON object, "
                    + $"got {element.ValueKind}"));
            }

            if (!element.TryGetProperty(_NAME_KEY, out var nameEl) ||
                nameEl.ValueKind != JsonValueKind.String)
            {
                return (default, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"enums '{enumName}' members[{index}] missing required string 'name'"));
            }

            var name = nameEl.GetString()!;

            if (!element.TryGetProperty(_DOC_KEY, out var docEl) ||
                docEl.ValueKind != JsonValueKind.String)
            {
                return (default, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"enums '{enumName}' members[{index}] '{name}' missing required string 'doc'"));
            }

            members.Add(new EnumMemberEntry(name, docEl.GetString()!));
            index++;
        }

        return (members.ToImmutable(), null);
    }
}
