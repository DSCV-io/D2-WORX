// -----------------------------------------------------------------------
// <copyright file="TelemetryTagsEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Tags.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for emitting a meter's <c>*TelemetryTags.g.cs</c> typed-constants
/// class from a parsed <see cref="MeterEntry"/>. Stateless and unit-testable
/// in isolation. The Roslyn-host integration
/// (<see cref="TelemetryTagsGenerator"/>) calls this once per meter and
/// translates emit-side <see cref="EmitDiagnostic"/> records into Roslyn
/// <c>Diagnostic</c> instances.
/// </summary>
internal static class TelemetryTagsEmitter
{
    private static readonly ImmutableHashSet<string> sr_validKinds =
        ImmutableHashSet.Create(StringComparer.Ordinal, "counter", "histogram", "gauge");

    /// <summary>
    /// Emits the typed-constants class source for the supplied meter, plus
    /// diagnostics. Returns an <see cref="EmitResult"/> with empty
    /// <see cref="EmitResult.GeneratedSource"/> + empty
    /// <see cref="EmitResult.HintName"/> when the meter has no closed-enum
    /// tags (documentation-only spec entry).
    /// </summary>
    /// <param name="meter">Parsed meter entry.</param>
    /// <param name="siblingSpecs">
    /// Sibling spec files (e.g. AuthErrorCodes spec) for cross-spec resolution.
    /// </param>
    /// <returns>Generated source + diagnostics.</returns>
    public static EmitResult Emit(MeterEntry meter, ImmutableArray<SpecFile> siblingSpecs)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();

        var seenInstruments = new HashSet<string>(StringComparer.Ordinal);
        var validInstrumentsWithTags = new List<(InstrumentEntry Inst, List<TagWithValues> Tags)>();

        foreach (var inst in meter.Instruments)
        {
            if (!seenInstruments.Add(inst.Name))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateInstrument(inst.Name, meter.Meter));
                continue;
            }

            if (!sr_validKinds.Contains(inst.Kind))
            {
                diagnostics.Add(EmitDiagnostics.UnknownInstrumentKind(
                    inst.Name,
                    meter.Meter,
                    inst.Kind,
                    string.Join(", ", sr_validKinds.OrderBy(k => k, StringComparer.Ordinal))));
                continue;
            }

            // Untagged instruments are skipped for codegen — they're documentation-only.
            if (inst.Tags.IsEmpty)
                continue;

            var resolvedTags = new List<TagWithValues>();
            var tagFailed = false;
            foreach (var tag in inst.Tags)
            {
                ImmutableArray<string> values;
                if (tag.ValuesFromSpec is not null)
                {
                    var (resolvedValues, diag) = CrossSpecResolver.Resolve(
                        tag.ValuesFromSpec, siblingSpecs, inst.Name, tag.Name, meter.Meter);
                    if (diag is not null)
                    {
                        diagnostics.Add(diag);
                        tagFailed = true;
                        break;
                    }

                    values = resolvedValues;
                }
                else
                {
                    var seenValues = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var v in tag.Values)
                    {
                        if (!seenValues.Add(v))
                        {
                            diagnostics.Add(EmitDiagnostics.DuplicateTagValue(
                                inst.Name, tag.Name, meter.Meter, v));
                            tagFailed = true;
                            break;
                        }
                    }

                    if (tagFailed)
                        break;

                    values = tag.Values;
                }

                resolvedTags.Add(
                    new TagWithValues(tag.Name, values, tag.ValuesFromSpec is not null));
            }

            if (tagFailed)
                continue;

            validInstrumentsWithTags.Add((inst, resolvedTags));
        }

        // Nothing to emit — meter has no closed-enum tagged instruments.
        if (validInstrumentsWithTags.Count == 0)
        {
            return new EmitResult(
                GeneratedSource: string.Empty,
                HintName: string.Empty,
                Diagnostics: diagnostics.ToImmutable());
        }

        var className = ResolveClassName(meter);
        var ns = ResolveNamespace(meter);
        var hintName = $"{className}.g.cs";

        var source = EmitSource(meter, ns, className, validInstrumentsWithTags);
        return new EmitResult(source, hintName, diagnostics.ToImmutable());
    }

    /// <summary>
    /// Returns the typed-constants class name for the given meter — either the
    /// spec-supplied <c>tagsClassName</c> override or a derived default.
    /// </summary>
    /// <param name="meter">The meter entry.</param>
    /// <returns>The PascalCase class name.</returns>
    public static string ResolveClassName(MeterEntry meter)
    {
        if (meter.TagsClassName is { Length: > 0 })
            return meter.TagsClassName;

        var lastSegment = LastSegment(meter.Meter);
        return lastSegment + "TelemetryTags";
    }

    /// <summary>
    /// Returns the typed-constants class namespace — either the spec-supplied
    /// <c>tagsNamespace</c> override or <c>{consumingAssembly}.Telemetry</c>.
    /// </summary>
    /// <param name="meter">The meter entry.</param>
    /// <returns>The namespace.</returns>
    public static string ResolveNamespace(MeterEntry meter)
    {
        if (meter.TagsNamespace is { Length: > 0 })
            return meter.TagsNamespace;
        return meter.ConsumingAssembly + ".Telemetry";
    }

    private static string LastSegment(string dotPath)
    {
        var i = dotPath.LastIndexOf('.');
        return i < 0 ? dotPath : dotPath.Substring(i + 1);
    }

    private static string EmitSource(
        MeterEntry meter,
        string ns,
        string className,
        List<(InstrumentEntry Inst, List<TagWithValues> Tags)> instruments)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine(
            "//   Generated by DcsvIo.D2.Telemetry.Tags.SourceGen.TelemetryTagsGenerator");
        sb.AppendLine(
            "//   from contracts/telemetry/telemetry.spec.json (the source of truth).");
        sb.AppendLine("//   Manual edits will be lost on rebuild.");
        sb.AppendLine($"//   Meter: {meter.Meter}");
        sb.AppendLine($"//   Consuming assembly: {meter.ConsumingAssembly}");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            $"/// Typed tag-key + tag-value constants for the <c>{EscapeXmlDoc(meter.Meter)}</c>");
        sb.AppendLine("/// meter, derived from the telemetry spec.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine(
            "/// Use these constants instead of bare string literals at counter / histogram");
        sb.AppendLine(
            "/// tag-write sites - drift between the spec and the runtime tag values is");
        sb.AppendLine("/// impossible (single spec source).");
        sb.AppendLine("/// </remarks>");
        sb.AppendLine($"public static class {className}");
        sb.AppendLine("{");

        for (var i = 0; i < instruments.Count; i++)
        {
            var (inst, tags) = instruments[i];
            EmitInstrumentClass(sb, inst, tags);
            if (i < instruments.Count - 1)
                sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static void EmitInstrumentClass(
        StringBuilder sb, InstrumentEntry inst, List<TagWithValues> tags)
    {
        var instClassName = ResolveInstrumentClassName(inst);

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            $"    /// Tag constants for the <c>{EscapeXmlDoc(inst.Name)}</c> instrument.");
        sb.AppendLine($"    /// {EscapeXmlDoc(inst.Description)}");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    public static class {instClassName}");
        sb.AppendLine("    {");

        for (var i = 0; i < tags.Count; i++)
        {
            var tag = tags[i];
            var tagConstName = "TAG_" + tag.Name.ToUpperInvariant();

            sb.AppendLine("        /// <summary>");
            sb.AppendLine(
                $"        /// The wire-format tag key (<c>{EscapeXmlDoc(tag.Name)}</c>).");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine(
                $"        public const string {tagConstName} = "
                + $"\"{EscapeStringLiteral(tag.Name)}\";");

            // Cross-spec tags don't get nested-class enumeration — consumers
            // reference the foreign spec's constants directly.
            if (!tag.IsCrossSpec && tag.Values.Length > 0)
            {
                sb.AppendLine();
                EmitTagValuesClass(sb, tag);
            }

            if (i < tags.Count - 1)
                sb.AppendLine();
        }

        sb.AppendLine("    }");
    }

    private static void EmitTagValuesClass(StringBuilder sb, TagWithValues tag)
    {
        var pascal = ToPascalCase(tag.Name);
        sb.AppendLine("        /// <summary>");
        sb.AppendLine(
            $"        /// Closed-enum tag values for the <c>{EscapeXmlDoc(tag.Name)}</c> tag.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine($"        public static class {pascal}");
        sb.AppendLine("        {");

        for (var i = 0; i < tag.Values.Length; i++)
        {
            var v = tag.Values[i];
            var constName = v.ToUpperInvariant();
            sb.AppendLine(
                $"            /// <summary>The <c>{EscapeXmlDoc(v)}</c> value.</summary>");
            sb.AppendLine(
                $"            public const string {constName} = \"{EscapeStringLiteral(v)}\";");
            if (i < tag.Values.Length - 1)
                sb.AppendLine();
        }

        sb.AppendLine("        }");
    }

    private static string ResolveInstrumentClassName(InstrumentEntry inst)
    {
        if (inst.ConstName is { Length: > 0 })
            return inst.ConstName;

        var lastSegment = LastSegment(inst.Name);
        return ToPascalCase(lastSegment);
    }

    private static string ToPascalCase(string snake)
    {
        if (snake.Length == 0)
            return snake;
        var sb = new StringBuilder(snake.Length);
        var upperNext = true;
        for (var i = 0; i < snake.Length; i++)
        {
            var c = snake[i];
            if (c == '_' || c == '-')
            {
                upperNext = true;
                continue;
            }

            sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
            upperNext = false;
        }

        return sb.ToString();
    }

    private static string EscapeStringLiteral(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"");

    private static string EscapeXmlDoc(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");

    private sealed record TagWithValues(
        string Name,
        ImmutableArray<string> Values,
        bool IsCrossSpec);
}
