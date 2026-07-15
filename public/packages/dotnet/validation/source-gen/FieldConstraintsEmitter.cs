// -----------------------------------------------------------------------
// <copyright file="FieldConstraintsEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Pure logic for emitting the <c>FieldConstraints</c> const-int class
/// (<c>FieldConstraints.g.cs</c>) and the closed-list taxonomy enums
/// (<c>Taxonomy.g.cs</c>) from a parsed <see cref="FieldConstraintsSpec"/>.
/// Stateless and unit-testable in isolation. The Roslyn-host integration
/// (<see cref="FieldConstraintsGenerator"/>) calls this and translates
/// emit-side <see cref="EmitDiagnostic"/> records into Roslyn
/// <c>Diagnostic</c> instances.
/// </summary>
internal static class FieldConstraintsEmitter
{
    /// <summary>The namespace the emitted types live in.</summary>
    public const string ROOT_NAMESPACE = "DcsvIo.D2.Validation.Abstractions";

    /// <summary>The emitted field-constraints class name.</summary>
    public const string FIELD_CONSTRAINTS_CLASS_NAME = "FieldConstraints";

    /// <summary>The Roslyn hint name for the emitted field-constraints source.</summary>
    public const string FIELD_CONSTRAINTS_HINT_NAME = "FieldConstraints.g.cs";

    /// <summary>The Roslyn hint name for the emitted taxonomy source.</summary>
    public const string TAXONOMY_HINT_NAME = "Taxonomy.g.cs";

    private static readonly Regex sr_constNamePattern =
        new("^[A-Z][A-Z0-9_]*$", RegexOptions.CultureInvariant);

    private static readonly Regex sr_enumNamePattern =
        new("^[A-Z][A-Za-z0-9]*$", RegexOptions.CultureInvariant);

    private static readonly Regex sr_identifierPattern =
        new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    /// <summary>
    /// Emits the <c>FieldConstraints.g.cs</c> + <c>Taxonomy.g.cs</c> sources plus
    /// diagnostics for the supplied spec. Returns one <see cref="EmitResult"/>
    /// per hint name so the dispatcher can write each into its own
    /// <c>AddSource</c> call.
    /// </summary>
    /// <param name="spec">Parsed field-constraints spec.</param>
    /// <returns>The per-source emit results.</returns>
    public static ImmutableArray<EmitResult> Emit(FieldConstraintsSpec spec)
    {
        var results = ImmutableArray.CreateBuilder<EmitResult>(2);
        results.Add(EmitConstraints(spec.Constraints));
        results.Add(EmitTaxonomy(spec.Enums));
        return results.ToImmutable();
    }

    private static EmitResult EmitConstraints(ImmutableArray<ConstraintEntry> entries)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var valid = new List<ConstraintEntry>();
        var seen = new HashSet<string>(System.StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (entry.Name.Falsey() || !sr_constNamePattern.IsMatch(entry.Name))
            {
                diagnostics.Add(EmitDiagnostics.InvalidConstName(entry.Name));
                continue;
            }

            if (!seen.Add(entry.Name))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateConstName(entry.Name));
                continue;
            }

            if (entry.Value <= 0)
            {
                diagnostics.Add(EmitDiagnostics.NonPositiveValue(entry.Name, entry.Value));
                continue;
            }

            valid.Add(entry);
        }

        var source = BuildConstraintsSource(valid);
        return new EmitResult(FIELD_CONSTRAINTS_HINT_NAME, source, diagnostics.ToImmutable());
    }

    private static EmitResult EmitTaxonomy(ImmutableArray<EnumEntry> enums)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var valid = new List<EnumEntry>();
        var seenEnums = new HashSet<string>(System.StringComparer.Ordinal);

        foreach (var entry in enums)
        {
            if (entry.Name.Falsey() || !sr_enumNamePattern.IsMatch(entry.Name))
            {
                diagnostics.Add(EmitDiagnostics.InvalidEnumName(entry.Name));
                continue;
            }

            if (!seenEnums.Add(entry.Name))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateEnumName(entry.Name));
                continue;
            }

            if (entry.Members.IsDefaultOrEmpty)
            {
                diagnostics.Add(EmitDiagnostics.EmptyEnumMemberList(entry.Name));
                continue;
            }

            if (!ValidateMembers(entry, diagnostics))
                continue;

            valid.Add(entry);
        }

        var source = BuildTaxonomySource(valid);
        return new EmitResult(TAXONOMY_HINT_NAME, source, diagnostics.ToImmutable());
    }

    private static bool ValidateMembers(
        EnumEntry entry, ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        var seenMembers = new HashSet<string>(System.StringComparer.Ordinal);
        var clean = true;
        foreach (var member in entry.Members)
        {
            if (member.Name.Falsey() || !sr_identifierPattern.IsMatch(member.Name))
            {
                diagnostics.Add(
                    EmitDiagnostics.InvalidEnumMemberName(entry.Name, member.Name));

                clean = false;
                continue;
            }

            if (!seenMembers.Add(member.Name))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateEnumMember(entry.Name, member.Name));
                clean = false;
            }
        }

        return clean;
    }

    private static string BuildConstraintsSource(List<ConstraintEntry> entries)
    {
        var sb = new StringBuilder();
        AppendHeader(sb);
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {ROOT_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Shared field-length / digit-count bounds enforced by the domain");
        sb.AppendLine(
            "/// value objects (contacts + Location), the FE/BFF Zod schemas, and");
        sb.AppendLine("/// arbitrary backend modules.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine(
            "/// Generated from the spec file - cross-language drift between the .NET");
        sb.AppendLine(
            "/// constants and the TS-side @dcsv-io/d2-validation-abstractions catalog is");
        sb.AppendLine(
            "/// structurally impossible (the TS emitter consumes the same spec via");
        sb.AppendLine("/// tools/ts-codegen).");
        sb.AppendLine("/// </remarks>");
        sb.AppendLine($"public static class {FIELD_CONSTRAINTS_CLASS_NAME}");
        sb.AppendLine("{");

        var first = true;
        foreach (var entry in entries)
        {
            if (!first)
                sb.AppendLine();

            first = false;
            sb.AppendLine($"    /// <summary>{EscapeXmlDoc(entry.Doc)}</summary>");
            sb.AppendLine(
                $"    public const int {entry.Name} = "
                + $"{entry.Value.ToString(CultureInfo.InvariantCulture)};");
        }

        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static string BuildTaxonomySource(List<EnumEntry> enums)
    {
        var sb = new StringBuilder();
        AppendHeader(sb);
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ROOT_NAMESPACE};");

        foreach (var entry in enums)
        {
            sb.AppendLine();
            sb.AppendLine("/// <summary>");
            foreach (var line in WrapDocLines(entry.Doc))
                sb.AppendLine($"/// {line}");

            sb.AppendLine("/// </summary>");
            sb.AppendLine("[JsonConverter(typeof(JsonStringEnumConverter))]");
            sb.AppendLine($"public enum {entry.Name} : byte");
            sb.AppendLine("{");

            byte value = 0;
            var firstMember = true;
            foreach (var member in entry.Members)
            {
                if (!firstMember)
                    sb.AppendLine();

                firstMember = false;
                var ordinal = value.ToString(CultureInfo.InvariantCulture);
                sb.AppendLine($"    /// <summary>{EscapeXmlDoc(member.Doc)}</summary>");
                sb.AppendLine($"    {member.Name} = {ordinal},");
                value++;
            }

            sb.AppendLine("}");
        }

        return sb.ToString().LfNormalized();
    }

    private static IEnumerable<string> WrapDocLines(string doc)
    {
        // The spec `doc` may be a single long sentence; emit it as one xmldoc
        // line. The generated file is exempt from the hand-authored 100-col
        // line-length rule (carve-out for .g.cs under Generated/).
        yield return EscapeXmlDoc(doc);
    }

    private static void AppendHeader(StringBuilder sb)
    {
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//   Generated by DcsvIo.D2.Validation.SourceGen");
        sb.AppendLine(
            "//   (FieldConstraintsGenerator) from "
            + "contracts/validation/field-constraints.spec.json");
        sb.AppendLine("//   (the source of truth). Manual edits will be lost on rebuild.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// -----------------------------------------------------------------------");
    }

    private static string EscapeXmlDoc(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
