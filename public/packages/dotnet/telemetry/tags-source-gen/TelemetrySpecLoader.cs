// -----------------------------------------------------------------------
// <copyright file="TelemetrySpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Tags.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for parsing <c>telemetry.spec.json</c> into a
/// <see cref="TelemetrySpec"/> record. Responsible only for JSON-shape
/// validation; semantic validation (uniqueness / kind enum membership /
/// duplicate values / cross-spec resolution) lives in
/// <see cref="TelemetryTagsEmitter"/> + <see cref="CrossSpecResolver"/>.
/// </summary>
internal static class TelemetrySpecLoader
{
    private const string _METERS_KEY = "meters";
    private const string _METER_KEY = "meter";
    private const string _CONSUMING_ASSEMBLY_KEY = "consumingAssembly";
    private const string _TAGS_NAMESPACE_KEY = "tagsNamespace";
    private const string _TAGS_CLASS_NAME_KEY = "tagsClassName";
    private const string _INSTRUMENTS_KEY = "instruments";
    private const string _NAME_KEY = "name";
    private const string _CONST_NAME_KEY = "constName";
    private const string _KIND_KEY = "kind";
    private const string _DESCRIPTION_KEY = "description";
    private const string _UNIT_KEY = "unit";
    private const string _TAGS_KEY = "tags";
    private const string _VALUES_KEY = "values";
    private const string _VALUES_FROM_SPEC_KEY = "valuesFromSpec";

    /// <summary>
    /// Parses raw JSON spec content into a <see cref="TelemetrySpec"/>.
    /// Returns either a populated spec or a single diagnostic explaining the
    /// parse failure.
    /// </summary>
    /// <param name="path">Spec file path (used for diagnostic message context).</param>
    /// <param name="json">Raw JSON content of the spec file.</param>
    /// <returns>A <see cref="LoadResult{TSpec}"/> wrapping <see cref="TelemetrySpec"/>.</returns>
    public static LoadResult<TelemetrySpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new LoadResult<TelemetrySpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_METERS_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return new LoadResult<TelemetrySpec>(
                    Spec: null,
                    Diagnostic: EmitDiagnostics.MalformedSpec(
                        fileName,
                        "missing required 'meters' array property at root"));
            }

            var meters = ImmutableArray.CreateBuilder<MeterEntry>();
            var index = 0;
            foreach (var element in arr.EnumerateArray())
            {
                var (entry, diag) = ParseMeter(element, fileName, index);
                if (diag is not null)
                    return new LoadResult<TelemetrySpec>(Spec: null, Diagnostic: diag);

                meters.Add(entry!);
                index++;
            }

            return new LoadResult<TelemetrySpec>(
                Spec: new TelemetrySpec(meters.ToImmutable()),
                Diagnostic: null);
        }
        catch (JsonException ex)
        {
            return new LoadResult<TelemetrySpec>(
                Spec: null,
                Diagnostic: EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (MeterEntry? Entry, EmitDiagnostic? Diagnostic) ParseMeter(
        JsonElement element, string fileName, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"meters[{index}] must be a JSON object, got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_METER_KEY, out var meterEl) ||
            meterEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"meters[{index}] missing required string 'meter'"));
        }

        var meter = meterEl.GetString()!;

        if (!element.TryGetProperty(_CONSUMING_ASSEMBLY_KEY, out var asmEl) ||
            asmEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"meters[{index}] '{meter}' missing required string 'consumingAssembly'"));
        }

        var consumingAssembly = asmEl.GetString()!;

        string? tagsNamespace = null;
        if (element.TryGetProperty(_TAGS_NAMESPACE_KEY, out var nsEl) &&
            nsEl.ValueKind == JsonValueKind.String)
        {
            tagsNamespace = nsEl.GetString();
        }

        string? tagsClassName = null;
        if (element.TryGetProperty(_TAGS_CLASS_NAME_KEY, out var clsEl) &&
            clsEl.ValueKind == JsonValueKind.String)
        {
            tagsClassName = clsEl.GetString();
        }

        if (!element.TryGetProperty(_INSTRUMENTS_KEY, out var instArr) ||
            instArr.ValueKind != JsonValueKind.Array)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"meters[{index}] '{meter}' missing required 'instruments' array"));
        }

        var instruments = ImmutableArray.CreateBuilder<InstrumentEntry>();
        var instIndex = 0;
        foreach (var instEl in instArr.EnumerateArray())
        {
            var (inst, diag) = ParseInstrument(instEl, fileName, meter, instIndex);
            if (diag is not null)
                return (null, diag);

            instruments.Add(inst!);
            instIndex++;
        }

        var entry = new MeterEntry(
            Meter: meter,
            ConsumingAssembly: consumingAssembly,
            TagsNamespace: tagsNamespace,
            TagsClassName: tagsClassName,
            Instruments: instruments.ToImmutable());

        return (entry, null);
    }

    private static (InstrumentEntry? Entry, EmitDiagnostic? Diagnostic) ParseInstrument(
        JsonElement element, string fileName, string meter, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"meters[{meter}].instruments[{index}] must be a JSON object, "
                + $"got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_NAME_KEY, out var nameEl) ||
            nameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"meters[{meter}].instruments[{index}] missing required string 'name'"));
        }

        var name = nameEl.GetString()!;

        string? constName = null;
        if (element.TryGetProperty(_CONST_NAME_KEY, out var constEl) &&
            constEl.ValueKind == JsonValueKind.String)
        {
            constName = constEl.GetString();
        }

        if (!element.TryGetProperty(_KIND_KEY, out var kindEl) ||
            kindEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"meters[{meter}].instruments[{name}] missing required string 'kind'"));
        }

        var kind = kindEl.GetString()!;

        if (!element.TryGetProperty(_DESCRIPTION_KEY, out var descEl) ||
            descEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"meters[{meter}].instruments[{name}] missing required string 'description'"));
        }

        var description = descEl.GetString()!;

        string? unit = null;
        if (element.TryGetProperty(_UNIT_KEY, out var unitEl) &&
            unitEl.ValueKind == JsonValueKind.String)
        {
            unit = unitEl.GetString();
        }

        var tags = ImmutableArray<TagEntry>.Empty;
        if (element.TryGetProperty(_TAGS_KEY, out var tagsEl))
        {
            if (tagsEl.ValueKind != JsonValueKind.Array)
            {
                return (null, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"meters[{meter}].instruments[{name}].tags must be an array, "
                    + $"got {tagsEl.ValueKind}"));
            }

            var tagBuilder = ImmutableArray.CreateBuilder<TagEntry>();
            var tagIndex = 0;
            foreach (var tagEl in tagsEl.EnumerateArray())
            {
                var (tag, diag) = ParseTag(tagEl, fileName, meter, name, tagIndex);
                if (diag is not null)
                    return (null, diag);

                tagBuilder.Add(tag!);
                tagIndex++;
            }

            tags = tagBuilder.ToImmutable();
        }

        var entry = new InstrumentEntry(
            Name: name,
            ConstName: constName,
            Kind: kind,
            Description: description,
            Unit: unit,
            Tags: tags);

        return (entry, null);
    }

    private static (TagEntry? Entry, EmitDiagnostic? Diagnostic) ParseTag(
        JsonElement element, string fileName, string meter, string instrument, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"meters[{meter}].instruments[{instrument}].tags[{index}] must be an object, "
                + $"got {element.ValueKind}"));
        }

        if (!element.TryGetProperty(_NAME_KEY, out var nameEl) ||
            nameEl.ValueKind != JsonValueKind.String)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"meters[{meter}].instruments[{instrument}].tags[{index}] missing required "
                + "string 'name'"));
        }

        var name = nameEl.GetString()!;

        var values = ImmutableArray<string>.Empty;
        string? valuesFromSpec = null;

        if (element.TryGetProperty(_VALUES_KEY, out var valuesEl))
        {
            if (valuesEl.ValueKind != JsonValueKind.Array)
            {
                return (null, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"meters[{meter}].instruments[{instrument}].tags[{name}].values must be an "
                    + $"array, got {valuesEl.ValueKind}"));
            }

            var valBuilder = ImmutableArray.CreateBuilder<string>();
            foreach (var v in valuesEl.EnumerateArray())
            {
                if (v.ValueKind != JsonValueKind.String)
                {
                    return (null, EmitDiagnostics.MalformedSpec(
                        fileName,
                        $"meters[{meter}].instruments[{instrument}].tags[{name}].values entries "
                        + $"must be strings, got {v.ValueKind}"));
                }

                valBuilder.Add(v.GetString()!);
            }

            values = valBuilder.ToImmutable();
        }

        if (element.TryGetProperty(_VALUES_FROM_SPEC_KEY, out var fromSpecEl) &&
            fromSpecEl.ValueKind == JsonValueKind.String)
        {
            valuesFromSpec = fromSpecEl.GetString();
        }

        if (values.IsEmpty && valuesFromSpec is null)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"meters[{meter}].instruments[{instrument}].tags[{name}] must declare either "
                + "'values' or 'valuesFromSpec'"));
        }

        if (!values.IsEmpty && valuesFromSpec is not null)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"meters[{meter}].instruments[{instrument}].tags[{name}] declares both 'values' "
                + "and 'valuesFromSpec' - exactly one is allowed"));
        }

        var entry = new TagEntry(
            Name: name,
            Values: values,
            ValuesFromSpec: valuesFromSpec);

        return (entry, null);
    }
}
