// -----------------------------------------------------------------------
// <copyright file="JsonConverterEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Emitters;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using DcsvIo.D2.Geo.SourceGen.Spec;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Emits the three <c>JsonConverter&lt;T&gt;</c> classes paired with the
/// wrapper structs from <see cref="WrapperStructEmitter"/>. Each emitted
/// converter carries an internal <c>FrozenSet&lt;string&gt;</c> closed-set
/// validation table sourced directly from the matching spec catalog;
/// deserialization of an unknown wire value throws
/// <see cref="System.Text.Json.JsonException"/> (strict deserialization
/// policy). The validation table is exposed via the internal
/// <c>IsKnown(string)</c> entrypoint so the wrapper struct's
/// <c>TryParse</c> path can consult the same source of truth.
/// </summary>
internal static class JsonConverterEmitter
{
    private const string _NAMESPACE = EmitterHelpers.AbstractionsNamespace;

    /// <summary>
    /// Emits the three JsonConverter classes. Returns one result per
    /// converter so the dispatcher can write each into its own hint name.
    /// </summary>
    /// <param name="context">The aggregate spec context.</param>
    /// <returns>The per-converter emit results.</returns>
    public static ImmutableArray<EmitResult> EmitAll(GeoSpecContext context)
    {
        var results = ImmutableArray.CreateBuilder<EmitResult>();

        var subdivisionCodes = new List<string>();
        if (context.Subdivisions is { } subs)
        {
            foreach (var entry in subs.Entries)
            {
                if (entry.Iso31662Code.Truthy())
                    subdivisionCodes.Add(entry.Iso31662Code);
            }
        }

        var localeCodes = new List<string>();
        if (context.Locales is { } locs)
        {
            foreach (var entry in locs.Entries)
            {
                if (entry.IetfBcp47Tag.Truthy())
                    localeCodes.Add(entry.IetfBcp47Tag);
            }
        }

        var timezoneCodes = new List<string>();
        if (context.Timezones is { } tzs)
        {
            foreach (var entry in tzs.Entries)
            {
                if (entry.IanaIdentifier.Truthy())
                    timezoneCodes.Add(entry.IanaIdentifier);
            }
        }

        results.Add(EmitConverter(
            wrapperName: "SubdivisionCode",
            converterClassName: "SubdivisionCodeJsonConverter",
            validSetFieldName: "sr_validSubdivisionCodes",
            displayLabel: "ISO 3166-2 subdivision code",
            codes: subdivisionCodes));

        results.Add(EmitConverter(
            wrapperName: "LocaleCode",
            converterClassName: "LocaleCodeJsonConverter",
            validSetFieldName: "sr_validLocaleCodes",
            displayLabel: "IETF BCP 47 locale tag",
            codes: localeCodes));

        results.Add(EmitConverter(
            wrapperName: "TimezoneCode",
            converterClassName: "TimezoneCodeJsonConverter",
            validSetFieldName: "sr_validTimezoneCodes",
            displayLabel: "IANA timezone identifier",
            codes: timezoneCodes));

        return results.ToImmutable();
    }

    private static EmitResult EmitConverter(
        string wrapperName,
        string converterClassName,
        string validSetFieldName,
        string displayLabel,
        IReadOnlyList<string> codes)
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Frozen;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine("using DcsvIo.D2.Utilities.Extensions;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            $"/// <see cref=\"JsonConverter{{T}}\"/> for <see cref=\"{wrapperName}\"/>.");
        sb.AppendLine(
            "/// Serializes the wrapped string verbatim; deserialization validates against");
        sb.AppendLine(
            $"/// the embedded closed-set table ({validSetFieldName}) and throws");
        sb.AppendLine(
            "/// <see cref=\"JsonException\"/> for unknown values (strict");
        sb.AppendLine("/// deserialization policy).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine(
            $"public sealed class {converterClassName} : JsonConverter<{wrapperName}>");
        sb.AppendLine("{");
        sb.AppendLine($"    private static readonly FrozenSet<string> {validSetFieldName} =");
        sb.AppendLine("        new HashSet<string>(StringComparer.Ordinal)");
        sb.AppendLine("        {");

        var sorted = new List<string>(codes);
        sorted.Sort(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var code in sorted)
        {
            if (!seen.Add(code))
                continue;

            sb.AppendLine($"            \"{EscapeStringLiteral(code)}\",");
        }

        sb.AppendLine("        }.ToFrozenSet(StringComparer.Ordinal);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            $"    /// Returns <c>true</c> when <paramref name=\"value\"/> is in the emitted");
        sb.AppendLine(
            "    /// closed-set validation table. Exposed for the wrapper struct's");
        sb.AppendLine(
            "    /// <c>TryParse</c> path so both routes share a single source of "
            + "truth.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"value\">The candidate value.</param>");
        sb.AppendLine("    /// <returns>True when the catalog contains this value.</returns>");
        sb.AppendLine(
            $"    public static bool IsKnown(string value) => "
            + $"{validSetFieldName}.Contains(value);");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine(
            $"    public override {wrapperName} Read(");
        sb.AppendLine("        ref Utf8JsonReader reader,");
        sb.AppendLine("        Type typeToConvert,");
        sb.AppendLine("        JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (reader.TokenType != JsonTokenType.String)");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new JsonException(");
        sb.AppendLine(
            $"                $\"Expected a string for {displayLabel}; got "
            + $"{{reader.TokenType}}.\");");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        var value = reader.GetString();");
        sb.AppendLine("        if (value.Falsey())");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new JsonException(");
        sb.AppendLine(
            $"                \"Expected a non-empty {displayLabel}; got null / "
            + "empty / whitespace.\");");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        if (!{validSetFieldName}.Contains(value!))");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new JsonException(");
        sb.AppendLine(
            $"                $\"Unknown {displayLabel} '{{value}}' — not present in "
            + "the closed catalog set.\");");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        return {wrapperName}.FromString(value!);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine("    public override void Write(");
        sb.AppendLine("        Utf8JsonWriter writer,");
        sb.AppendLine($"        {wrapperName} value,");
        sb.AppendLine("        JsonSerializerOptions options)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (writer is null)");
        sb.AppendLine("            throw new ArgumentNullException(nameof(writer));");
        sb.AppendLine();
        sb.AppendLine("        writer.WriteStringValue(value.Value);");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new EmitResult(
            HintName: $"{converterClassName}.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static void AppendFileHeader(StringBuilder sb) =>
        EmitterHelpers.AppendFileHeader(sb);

    private static string EscapeStringLiteral(string value) =>
        EmitterHelpers.EscapeStringLiteral(value);
}
