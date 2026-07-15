// -----------------------------------------------------------------------
// <copyright file="SpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for parsing a context spec JSON file into a <see cref="ContextSpec"/>
/// record. Responsible only for JSON-shape validation; semantic validation
/// (closed type vocab, name collisions, etc.) lives in the emitters.
/// </summary>
internal static class SpecLoader
{
    private const string _NAME_KEY = "name";
    private const string _NAMESPACE_KEY = "namespace";
    private const string _DESCRIPTION_KEY = "description";
    private const string _EXTENDS_KEY = "extends";
    private const string _SECTIONS_KEY = "sections";
    private const string _PROPERTIES_KEY = "properties";
    private const string _TYPE_KEY = "type";
    private const string _CLAIM_KEY = "claim";
    private const string _TRINARY_AUTH_KEY = "trinaryAuth";
    private const string _DERIVED_KEY = "derived";
    private const string _DEFAULT_KEY = "default";
    private const string _DOC_KEY = "doc";
    private const string _PROPAGATE_KEY = "propagate";
    private const string _MAX_LENGTH_KEY = "maxLength";
    private const string _ENTRY_ID_MAX_LENGTH_KEY = "entryIdMaxLength";
    private const string _REDACT_KEY = "redact";

    /// <summary>
    /// Parses raw JSON spec content into a <see cref="ContextSpec"/>. Returns
    /// either a populated spec or a single diagnostic explaining the parse failure.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/> wrapping <see cref="ContextSpec"/>.</returns>
    public static LoadResult<ContextSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<ContextSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName, $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_NAME_KEY, out var nameElement) ||
                nameElement.ValueKind != JsonValueKind.String)
            {
                return new LoadResult<ContextSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName, "missing required string 'name' at root"));
            }

            if (!root.TryGetProperty(_NAMESPACE_KEY, out var nsElement) ||
                nsElement.ValueKind != JsonValueKind.String)
            {
                return new LoadResult<ContextSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName, "missing required string 'namespace' at root"));
            }

            string? description = null;
            if (root.TryGetProperty(_DESCRIPTION_KEY, out var descElement) &&
                descElement.ValueKind == JsonValueKind.String)
                description = descElement.GetString();

            string? extends = null;
            if (root.TryGetProperty(_EXTENDS_KEY, out var extElement) &&
                extElement.ValueKind == JsonValueKind.String)
                extends = extElement.GetString();

            if (!root.TryGetProperty(_SECTIONS_KEY, out var sectionsElement) ||
                sectionsElement.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<ContextSpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName, "missing required 'sections' array at root"));
            }

            var sections = ImmutableArray.CreateBuilder<Section>();
            var sectionIndex = 0;
            foreach (var sectionElement in sectionsElement.EnumerateArray())
            {
                var (section, diag) = ParseSection(sectionElement, fileName, sectionIndex);
                if (diag is not null)
                    return new LoadResult<ContextSpec>(Spec: null, Diagnostic: diag);

                sections.Add(section!);
                sectionIndex++;
            }

            var spec = new ContextSpec(
                Name: nameElement.GetString()!,
                Namespace: nsElement.GetString()!,
                Description: description,
                Extends: extends,
                Sections: sections.ToImmutable());

            return new LoadResult<ContextSpec>(Spec: spec, Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<ContextSpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (Section? Section, EmitDiagnostic? Diagnostic) ParseSection(
        JsonElement element, string fileName, int sectionIndex)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName, $"sections[{sectionIndex}] must be an object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_NAME_KEY, out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName, $"sections[{sectionIndex}] missing required string 'name'"));
        }

        var sectionName = nameElement.GetString()!;

        if (!element.TryGetProperty(_PROPERTIES_KEY, out var propsElement) ||
            propsElement.ValueKind != JsonValueKind.Array)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"sections[{sectionIndex}] '{sectionName}' missing required 'properties' array"));
        }

        var props = ImmutableArray.CreateBuilder<PropertySpec>();
        var propIndex = 0;
        foreach (var propElement in propsElement.EnumerateArray())
        {
            var (prop, diag) = ParseProperty(propElement, fileName, sectionName, propIndex);
            if (diag is not null)
                return (null, diag);

            props.Add(prop!);
            propIndex++;
        }

        return (new Section(sectionName, props.ToImmutable()), null);
    }

    private static (PropertySpec? Property, EmitDiagnostic? Diagnostic) ParseProperty(
        JsonElement element, string fileName, string sectionName, int propIndex)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"sections '{sectionName}' properties[{propIndex}] must be an object, "
                + $"got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_NAME_KEY, out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"sections '{sectionName}' properties[{propIndex}] missing required "
                + "string 'name'"));
        }

        var name = nameElement.GetString()!;

        if (!element.TryGetProperty(_TYPE_KEY, out var typeElement) ||
            typeElement.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"sections '{sectionName}' property '{name}' missing required string 'type'"));
        }

        string? claim = null;
        if (element.TryGetProperty(_CLAIM_KEY, out var claimElement) &&
            claimElement.ValueKind == JsonValueKind.String)
            claim = claimElement.GetString();

        var trinaryAuth = false;
        if (element.TryGetProperty(_TRINARY_AUTH_KEY, out var trinaryElement) &&
            (trinaryElement.ValueKind == JsonValueKind.True ||
             trinaryElement.ValueKind == JsonValueKind.False))
            trinaryAuth = trinaryElement.GetBoolean();

        string? derived = null;
        if (element.TryGetProperty(_DERIVED_KEY, out var derivedElement) &&
            derivedElement.ValueKind == JsonValueKind.String)
            derived = derivedElement.GetString();

        string? defaultValue = null;
        if (element.TryGetProperty(_DEFAULT_KEY, out var defaultElement) &&
            defaultElement.ValueKind == JsonValueKind.String)
            defaultValue = defaultElement.GetString();

        string? doc = null;
        if (element.TryGetProperty(_DOC_KEY, out var docElement) &&
            docElement.ValueKind == JsonValueKind.String)
            doc = docElement.GetString();

        var propagate = false;
        if (element.TryGetProperty(_PROPAGATE_KEY, out var propagateElement) &&
            (propagateElement.ValueKind == JsonValueKind.True ||
             propagateElement.ValueKind == JsonValueKind.False))
            propagate = propagateElement.GetBoolean();

        int? maxLength = null;
        if (element.TryGetProperty(_MAX_LENGTH_KEY, out var maxLengthElement) &&
            maxLengthElement.ValueKind == JsonValueKind.Number)
            maxLength = maxLengthElement.GetInt32();

        int? entryIdMaxLength = null;
        if (element.TryGetProperty(_ENTRY_ID_MAX_LENGTH_KEY, out var entryIdMaxElement) &&
            entryIdMaxElement.ValueKind == JsonValueKind.Number)
            entryIdMaxLength = entryIdMaxElement.GetInt32();

        var redact = false;
        if (element.TryGetProperty(_REDACT_KEY, out var redactElement) &&
            (redactElement.ValueKind == JsonValueKind.True ||
             redactElement.ValueKind == JsonValueKind.False))
            redact = redactElement.GetBoolean();

        var prop = new PropertySpec(
            Name: name,
            Type: typeElement.GetString()!,
            Claim: claim,
            TrinaryAuth: trinaryAuth,
            Derived: derived,
            Default: defaultValue,
            Doc: doc,
            Propagate: propagate,
            MaxLength: maxLength,
            EntryIdMaxLength: entryIdMaxLength,
            Redact: redact);

        return (prop, null);
    }
}
