// -----------------------------------------------------------------------
// <copyright file="ScopeSpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Scopes.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for parsing <c>scopes.spec.json</c> into a <see cref="ScopesSpec"/>
/// record. Responsible only for JSON-shape validation; semantic validation
/// (naming convention, enum values, tree-position collisions, etc.) lives in
/// <see cref="ScopesEmitter"/>.
/// </summary>
internal static class ScopeSpecLoader
{
    private const string _SCOPES_KEY = "scopes";
    private const string _NAME_KEY = "name";
    private const string _DESCRIPTION_KEY = "description";
    private const string _ACTION_SENSITIVITY_KEY = "actionSensitivity";
    private const string _IMPERSONATION_BLOCKED_KEY = "impersonationBlocked";
    private const string _GRANTED_TO_KEY = "grantedTo";

    /// <summary>
    /// Parses raw JSON spec content into a <see cref="ScopesSpec"/>. Returns
    /// either a populated spec or a single diagnostic explaining the parse failure.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/> wrapping <see cref="ScopesSpec"/>.</returns>
    public static LoadResult<ScopesSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<ScopesSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_SCOPES_KEY, out var scopesElement) ||
                scopesElement.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<ScopesSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'scopes' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<ScopeEntry>();
            var index = 0;
            foreach (var scopeElement in scopesElement.EnumerateArray())
            {
                var (entry, diag) = ParseScopeEntry(scopeElement, fileName, index);
                if (diag is not null)
                    return new LoadResult<ScopesSpec>(Spec: null, Diagnostic: diag);

                entries.Add(entry!);
                index++;
            }

            return new LoadResult<ScopesSpec>(
                Spec: new ScopesSpec(entries.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<ScopesSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (ScopeEntry? Entry, EmitDiagnostic? Diagnostic) ParseScopeEntry(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"scopes[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_NAME_KEY, out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"scopes[{index}] missing required string 'name'"));
        }

        var name = nameElement.GetString()!;

        string? description = null;
        if (element.TryGetProperty(_DESCRIPTION_KEY, out var descElement) &&
            descElement.ValueKind == JsonValueKind.String)
        {
            description = descElement.GetString();
        }

        if (!element.TryGetProperty(_ACTION_SENSITIVITY_KEY, out var sensElement) ||
            sensElement.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"scopes[{index}] '{name}' missing required string 'actionSensitivity'"));
        }

        var actionSensitivity = sensElement.GetString()!;

        if (!element.TryGetProperty(_IMPERSONATION_BLOCKED_KEY, out var impElement) ||
            (impElement.ValueKind != JsonValueKind.True &&
             impElement.ValueKind != JsonValueKind.False))
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"scopes[{index}] '{name}' missing required boolean 'impersonationBlocked'"));
        }

        var impersonationBlocked = impElement.GetBoolean();

        IReadOnlyDictionary<string, ImmutableArray<string>>? grantedTo = null;
        if (element.TryGetProperty(_GRANTED_TO_KEY, out var grantElement))
        {
            if (grantElement.ValueKind != JsonValueKind.Object)
            {
                return (null, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"scopes[{index}] '{name}' grantedTo must be an object, "
                    + $"got {grantElement.ValueKind}"));
            }

            var dict = new Dictionary<string, ImmutableArray<string>>(StringComparer.Ordinal);
            foreach (var prop in grantElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Array)
                {
                    return (null, EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"scopes[{index}] '{name}' grantedTo['{prop.Name}'] must be an array, "
                        + $"got {prop.Value.ValueKind}"));
                }

                var roles = ImmutableArray.CreateBuilder<string>();
                foreach (var role in prop.Value.EnumerateArray())
                {
                    if (role.ValueKind != JsonValueKind.String)
                    {
                        return (null, EmitDiagnostics.MalformedSpec(
                            fileName,
                            $"scopes[{index}] '{name}' grantedTo['{prop.Name}'] entries must "
                            + $"be strings, got {role.ValueKind}"));
                    }

                    roles.Add(role.GetString()!);
                }

                dict[prop.Name] = roles.ToImmutable();
            }

            grantedTo = dict;
        }

        var entry = new ScopeEntry(
            Name: name,
            Description: description,
            ActionSensitivity: actionSensitivity,
            ImpersonationBlocked: impersonationBlocked,
            GrantedTo: grantedTo);

        return (entry, null);
    }
}
