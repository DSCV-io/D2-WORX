// -----------------------------------------------------------------------
// <copyright file="WrapperStructEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Emitters;

using System.Collections.Immutable;
using System.Text;
using DcsvIo.D2.Geo.SourceGen.Spec;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Emits the three wrapper <c>readonly struct</c> types used for
/// open-coded catalogs where the identifier carries punctuation (hyphen,
/// slash) that C# enum members cannot:
/// <list type="bullet">
///   <item><c>SubdivisionCode</c> — ISO 3166-2 code (<c>"US-CA"</c>).</item>
///   <item><c>LocaleCode</c> — IETF BCP 47 tag (<c>"en-US"</c>).</item>
///   <item><c>TimezoneCode</c> — IANA identifier (<c>"America/New_York"</c>).</item>
/// </list>
/// Each struct carries a <c>Value</c> property + <c>FromString</c> /
/// <c>TryParse</c> factories + structural equality + <c>ToString</c>.
/// Validation against the closed catalog set lives on the matching
/// <c>JsonConverter</c> + the <c>TryParse</c> path; <c>FromString</c> trusts
/// the caller (used at codegen-time + for known-good inputs).
/// </summary>
internal static class WrapperStructEmitter
{
    private const string _NAMESPACE = EmitterHelpers.AbstractionsNamespace;

    /// <summary>
    /// Emits all three wrapper structs. The parent-country lookup for
    /// <c>SubdivisionCode.ParentCountry</c> ships in a separate emitter
    /// (<see cref="ParentCountryTableEmitter"/>) because it is data-shaped
    /// (lookup table) rather than type-shaped.
    /// </summary>
    /// <param name="context">The aggregate spec context (unused — wrapper
    /// shapes do not depend on entry data).</param>
    /// <returns>The per-struct emit results.</returns>
    public static ImmutableArray<EmitResult> EmitAll(GeoSpecContext context)
    {
        _ = context;

        var results = ImmutableArray.CreateBuilder<EmitResult>();
        results.Add(EmitSubdivisionCode());
        results.Add(EmitLocaleCode());
        results.Add(EmitTimezoneCode());
        return results.ToImmutable();
    }

    private static EmitResult EmitSubdivisionCode()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine("using DcsvIo.D2.Utilities.Extensions;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// ISO 3166-2 subdivision identifier (e.g. <c>\"US-CA\"</c>,");
        sb.AppendLine(
            "/// <c>\"CA-AB\"</c>, <c>\"GB-ENG\"</c>). Wrapped in a readonly struct because");
        sb.AppendLine(
            "/// C# enums cannot carry hyphens. Wire form is the string;");
        sb.AppendLine(
            "/// <see cref=\"SubdivisionCodeJsonConverter\"/> validates against the");
        sb.AppendLine(
            "/// closed catalog set and throws <see cref=\"System.Text.Json.JsonException\"/>");
        sb.AppendLine(
            "/// for unknown values (strict deserialization policy). The");
        sb.AppendLine(
            "/// <see cref=\"ParentCountry\"/> derived property resolves");
        sb.AppendLine(
            "/// the owning <see cref=\"CountryCode\"/> in O(1) via an emitted lookup table.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonConverter(typeof(SubdivisionCodeJsonConverter))]");
        sb.AppendLine(
            "public readonly partial struct SubdivisionCode : IEquatable<SubdivisionCode>");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>The raw ISO 3166-2 code.</summary>");
        sb.AppendLine("    public string Value { get; }");
        sb.AppendLine();
        sb.AppendLine("    private SubdivisionCode(string value)");
        sb.AppendLine("    {");
        sb.AppendLine("        Value = value;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Constructs a <see cref=\"SubdivisionCode\"/> without catalog validation.");
        sb.AppendLine(
            "    /// Use at codegen time or with caller-known-good inputs only — boundary");
        sb.AppendLine(
            "    /// code SHOULD prefer <see cref=\"TryParse(string?, out SubdivisionCode)\"/>");
        sb.AppendLine("    /// (which consults the emitted closed-set validation table).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    /// <param name=\"value\">Raw ISO 3166-2 code (e.g. <c>\"US-CA\"</c>).</param>");
        sb.AppendLine("    public static SubdivisionCode FromString(string value)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("            throw new ArgumentNullException(nameof(value));");
        sb.AppendLine();
        sb.AppendLine("        return new SubdivisionCode(value);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Tries to parse a string into a <see cref=\"SubdivisionCode\"/>; the");
        sb.AppendLine(
            "    /// emitted closed-set validation table is consulted via the matching");
        sb.AppendLine(
            "    /// <see cref=\"SubdivisionCodeJsonConverter\"/>. Returns <c>false</c> for");
        sb.AppendLine(
            "    /// null, empty, whitespace-only, or unknown codes.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    public static bool TryParse(string? value, out SubdivisionCode result)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value.Falsey())");
        sb.AppendLine("        {");
        sb.AppendLine("            result = default;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine(
            "        if (!SubdivisionCodeJsonConverter.IsKnown(value!))");
        sb.AppendLine("        {");
        sb.AppendLine("            result = default;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        result = new SubdivisionCode(value!);");
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// The owning <see cref=\"CountryCode\"/> resolved in O(1) via the emitted");
        sb.AppendLine(
            "    /// parent-country lookup table.");
        sb.AppendLine("    /// Throws <see cref=\"InvalidOperationException\"/> when the");
        sb.AppendLine("    /// underlying code is not present in the catalog.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public CountryCode ParentCountry =>");
        sb.AppendLine(
            "        SubdivisionParentCountryLookup.GetParent(Value);");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine(
            "    public bool Equals(SubdivisionCode other) =>");
        sb.AppendLine("        string.Equals(Value, other.Value, StringComparison.Ordinal);");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine(
            "    public override bool Equals(object? obj) =>");
        sb.AppendLine("        obj is SubdivisionCode other && Equals(other);");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine(
            "    public override int GetHashCode() => Value?.GetHashCode() ?? 0;");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine("    public override string ToString() => Value ?? string.Empty;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Equality operator over the wrapped string value.</summary>");
        sb.AppendLine(
            "    public static bool operator ==(SubdivisionCode left, "
            + "SubdivisionCode right) =>");
        sb.AppendLine("        left.Equals(right);");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Inequality operator over the wrapped string value.</summary>");
        sb.AppendLine(
            "    public static bool operator !=(SubdivisionCode left, "
            + "SubdivisionCode right) =>");
        sb.AppendLine("        !left.Equals(right);");
        sb.AppendLine("}");

        return new EmitResult(
            HintName: "SubdivisionCode.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static EmitResult EmitLocaleCode()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine("using DcsvIo.D2.Utilities.Extensions;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// IETF BCP 47 locale tag (e.g. <c>\"en-US\"</c>, <c>\"pt-BR\"</c>,");
        sb.AppendLine(
            "/// <c>\"zh-Hans-CN\"</c>). Wrapped in a readonly struct because C# enums");
        sb.AppendLine(
            "/// cannot carry hyphens. Wire form is the string;");
        sb.AppendLine(
            "/// <see cref=\"LocaleCodeJsonConverter\"/> validates against the closed");
        sb.AppendLine(
            "/// catalog set and throws <see cref=\"System.Text.Json.JsonException\"/>");
        sb.AppendLine("/// for unknown values (strict deserialization policy).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonConverter(typeof(LocaleCodeJsonConverter))]");
        sb.AppendLine(
            "public readonly partial struct LocaleCode : IEquatable<LocaleCode>");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>The raw IETF BCP 47 tag.</summary>");
        sb.AppendLine("    public string Value { get; }");
        sb.AppendLine();
        sb.AppendLine("    private LocaleCode(string value)");
        sb.AppendLine("    {");
        sb.AppendLine("        Value = value;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Constructs a <see cref=\"LocaleCode\"/> without catalog validation.");
        sb.AppendLine(
            "    /// Use at codegen time or with caller-known-good inputs only — boundary");
        sb.AppendLine(
            "    /// code SHOULD prefer <see cref=\"TryParse(string?, out LocaleCode)\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"value\">Raw IETF BCP 47 tag.</param>");
        sb.AppendLine("    public static LocaleCode FromString(string value)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("            throw new ArgumentNullException(nameof(value));");
        sb.AppendLine();
        sb.AppendLine("        return new LocaleCode(value);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Tries to parse a string into a <see cref=\"LocaleCode\"/> against the");
        sb.AppendLine(
            "    /// emitted closed-set validation table on");
        sb.AppendLine(
            "    /// <see cref=\"LocaleCodeJsonConverter\"/>. Returns <c>false</c> for");
        sb.AppendLine(
            "    /// null / empty / whitespace / unknown.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static bool TryParse(string? value, out LocaleCode result)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value.Falsey())");
        sb.AppendLine("        {");
        sb.AppendLine("            result = default;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (!LocaleCodeJsonConverter.IsKnown(value!))");
        sb.AppendLine("        {");
        sb.AppendLine("            result = default;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        result = new LocaleCode(value!);");
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine("    public bool Equals(LocaleCode other) =>");
        sb.AppendLine("        string.Equals(Value, other.Value, StringComparison.Ordinal);");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine("    public override bool Equals(object? obj) =>");
        sb.AppendLine("        obj is LocaleCode other && Equals(other);");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine(
            "    public override int GetHashCode() => Value?.GetHashCode() ?? 0;");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine("    public override string ToString() => Value ?? string.Empty;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Equality operator over the wrapped string value.</summary>");
        sb.AppendLine("    public static bool operator ==(LocaleCode left, LocaleCode right) =>");
        sb.AppendLine("        left.Equals(right);");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Inequality operator over the wrapped string value.</summary>");
        sb.AppendLine("    public static bool operator !=(LocaleCode left, LocaleCode right) =>");
        sb.AppendLine("        !left.Equals(right);");
        sb.AppendLine("}");

        return new EmitResult(
            HintName: "LocaleCode.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static EmitResult EmitTimezoneCode()
    {
        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine("using DcsvIo.D2.Utilities.Extensions;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// IANA timezone identifier (e.g. <c>\"America/New_York\"</c>,");
        sb.AppendLine(
            "/// <c>\"Europe/Berlin\"</c>, <c>\"Asia/Tokyo\"</c>). Wrapped in a readonly");
        sb.AppendLine(
            "/// struct because C# enums cannot carry slashes or underscores. Wire form");
        sb.AppendLine(
            "/// is the string; <see cref=\"TimezoneCodeJsonConverter\"/> validates against");
        sb.AppendLine(
            "/// the closed catalog set and throws");
        sb.AppendLine(
            "/// <see cref=\"System.Text.Json.JsonException\"/> for unknown values (strict");
        sb.AppendLine("/// deserialization policy).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[JsonConverter(typeof(TimezoneCodeJsonConverter))]");
        sb.AppendLine(
            "public readonly partial struct TimezoneCode : IEquatable<TimezoneCode>");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>The raw IANA timezone identifier.</summary>");
        sb.AppendLine("    public string Value { get; }");
        sb.AppendLine();
        sb.AppendLine("    private TimezoneCode(string value)");
        sb.AppendLine("    {");
        sb.AppendLine("        Value = value;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Constructs a <see cref=\"TimezoneCode\"/> without catalog validation.");
        sb.AppendLine(
            "    /// Use at codegen time or with caller-known-good inputs only.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"value\">Raw IANA timezone identifier.</param>");
        sb.AppendLine("    public static TimezoneCode FromString(string value)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("            throw new ArgumentNullException(nameof(value));");
        sb.AppendLine();
        sb.AppendLine("        return new TimezoneCode(value);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Tries to parse a string into a <see cref=\"TimezoneCode\"/> against the");
        sb.AppendLine(
            "    /// emitted closed-set validation table on");
        sb.AppendLine(
            "    /// <see cref=\"TimezoneCodeJsonConverter\"/>. Returns <c>false</c> for");
        sb.AppendLine(
            "    /// null / empty / whitespace / unknown.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static bool TryParse(string? value, out TimezoneCode result)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value.Falsey())");
        sb.AppendLine("        {");
        sb.AppendLine("            result = default;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        if (!TimezoneCodeJsonConverter.IsKnown(value!))");
        sb.AppendLine("        {");
        sb.AppendLine("            result = default;");
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        result = new TimezoneCode(value!);");
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine("    public bool Equals(TimezoneCode other) =>");
        sb.AppendLine("        string.Equals(Value, other.Value, StringComparison.Ordinal);");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine("    public override bool Equals(object? obj) =>");
        sb.AppendLine("        obj is TimezoneCode other && Equals(other);");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine(
            "    public override int GetHashCode() => Value?.GetHashCode() ?? 0;");
        sb.AppendLine();
        sb.AppendLine("    /// <inheritdoc />");
        sb.AppendLine("    public override string ToString() => Value ?? string.Empty;");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Equality operator over the wrapped string value.</summary>");
        sb.AppendLine(
            "    public static bool operator ==(TimezoneCode left, TimezoneCode "
            + "right) =>");
        sb.AppendLine("        left.Equals(right);");
        sb.AppendLine();
        sb.AppendLine(
            "    /// <summary>Inequality operator over the wrapped string value.</summary>");
        sb.AppendLine(
            "    public static bool operator !=(TimezoneCode left, TimezoneCode "
            + "right) =>");
        sb.AppendLine("        !left.Equals(right);");
        sb.AppendLine("}");

        return new EmitResult(
            HintName: "TimezoneCode.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static void AppendFileHeader(StringBuilder sb) =>
        EmitterHelpers.AppendFileHeader(sb);
}
