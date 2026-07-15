// -----------------------------------------------------------------------
// <copyright file="AudiencesEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Audiences.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using D2.Shared.SourceGen;
using D2.Shared.SourceGen.Polyfills;

/// <summary>
/// Pure logic for emitting the <c>Audiences</c> static partial class source from
/// a parsed <see cref="AudiencesSpec"/>. Stateless and unit-testable in isolation.
/// The Roslyn-host integration (<see cref="AudiencesGenerator"/>) calls this and
/// translates emit-side <see cref="EmitDiagnostic"/> records into Roslyn
/// <c>Diagnostic</c> instances.
/// </summary>
internal static class AudiencesEmitter
{
    private const string _DEFAULT_ROOT_NAMESPACE = "D2.Shared.Auth.Abstractions";
    private const string _DEFAULT_AUDIENCES_CLASS_NAME = "Audiences";

    /// <summary>
    /// Emits the Audiences class source plus diagnostics for the supplied spec.
    /// </summary>
    /// <param name="spec">Parsed audience spec.</param>
    /// <param name="rootNamespace">Emit namespace (public or private product type).</param>
    /// <param name="className">Emit class name (<c>Audiences</c> or <c>ProductAudiences</c>).</param>
    /// <returns>Generated source + diagnostics.</returns>
    public static EmitResult Emit(
        AudiencesSpec spec,
        string? rootNamespace = null,
        string? className = null)
    {
        var effectiveNamespace = rootNamespace ?? _DEFAULT_ROOT_NAMESPACE;
        var effectiveClassName = className ?? _DEFAULT_AUDIENCES_CLASS_NAME;

        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var validAudiences = new List<AudienceEntry>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        // Track URL → first-seen name so duplicate-URL diagnostics can name both
        // sides of the collision (Files=https://x.internal AND Notifications=https://x.internal).
        var urlToFirstName = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var audience in spec.Audiences)
        {
            if (!seenNames.Add(audience.Name))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateAudienceName(audience.Name));
                continue;
            }

            if (!ValidateAudienceName(audience.Name, out var nameReason))
            {
                diagnostics.Add(EmitDiagnostics.InvalidAudienceName(audience.Name, nameReason));
                continue;
            }

            // Uri.TryCreate with UriKind.Absolute has platform-dependent behaviour for
            // root-relative paths such as "/relative/path": on some .NET versions it
            // returns true (treating the leading slash as a host-relative file URI) while
            // on others it correctly returns false.  Uri.IsWellFormedUriString follows
            // RFC 3986 strictly — it requires a scheme component — and therefore rejects
            // "/relative/path" consistently across all platforms and .NET versions.
            if (!Uri.IsWellFormedUriString(audience.Url, UriKind.Absolute))
            {
                diagnostics.Add(EmitDiagnostics.InvalidAudienceUrl(audience.Name, audience.Url));
                continue;
            }

            if (urlToFirstName.TryGetValue(audience.Url, out var firstName))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateAudienceUrl(
                    firstName,
                    audience.Name,
                    audience.Url));
                continue;
            }

            urlToFirstName[audience.Url] = audience.Name;
            validAudiences.Add(audience);
        }

        var source = EmitSource(validAudiences, effectiveNamespace, effectiveClassName);
        return new EmitResult(source, diagnostics.ToImmutable());
    }

    private static bool ValidateAudienceName(string name, out string reason)
    {
        if (name.Falsey())
        {
            reason = "name is empty";
            return false;
        }

        if (!IsAsciiUpperLetter(name[0]))
        {
            reason = $"name '{name}' must start with an uppercase ASCII letter";
            return false;
        }

        for (var i = 1; i < name.Length; i++)
        {
            var c = name[i];
            if (!IsAsciiLetter(c) && !IsAsciiDigit(c))
            {
                reason =
                    $"name '{name}' contains invalid character '{c}' "
                    + "(only ASCII letters and digits allowed)";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static string EmitSource(
        IReadOnlyList<AudienceEntry> validAudiences,
        string rootNamespace,
        string className)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//   Generated by D2.Shared.Auth.Audiences.SourceGen.AudiencesGenerator");
        sb.AppendLine(
            "//   from contracts/auth-audiences/audiences.spec.json (the source of truth).");
        sb.AppendLine("//   Manual edits will be lost on rebuild.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// JWT <c>aud</c>-claim audience constants generated from "
            + "<c>contracts/auth-audiences/audiences.spec.json</c>.");
        sb.AppendLine(
            "/// Each audience surfaces as a <c>public const string {Name} = \"{url}\"</c>.");
        sb.AppendLine(
            "/// Use <see cref=\"IsKnown\"/> to validate inbound JWT <c>aud</c> claims and");
        sb.AppendLine(
            "/// <see cref=\"Resolve\"/> to look up a URL by audience name.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static partial class {className}");
        sb.AppendLine("{");

        var ordered = validAudiences
            .OrderBy(a => a.Name, StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            var audience = ordered[i];
            var doc = audience.Description ?? audience.Url;
            sb.AppendLine("    /// <summary>");
            sb.AppendLine($"    /// {EscapeXmlDoc(doc)}");
            sb.AppendLine("    /// </summary>");
            sb.AppendLine(
                $"    public const string {audience.Name} = "
                + $"\"{EscapeStringLiteral(audience.Url)}\";");
            if (i < ordered.Count - 1)
                sb.AppendLine();
        }

        sb.AppendLine();
        EmitHelpers(sb, ordered);

        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static void EmitHelpers(StringBuilder sb, IReadOnlyList<AudienceEntry> ordered)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// True when <paramref name=\"audience\"/> matches a registered audience URL.");
        sb.AppendLine(
            "    /// Use this to validate an inbound JWT's <c>aud</c> claim against the spec.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"audience\">The audience URL string.</param>");
        sb.AppendLine("    /// <returns>True if the URL is a known spec entry.</returns>");
        sb.AppendLine("    public static bool IsKnown(string audience) =>");
        sb.AppendLine("        sr_allUrls.Contains(audience);");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Resolves an audience <paramref name=\"name\"/> (e.g. <c>\"Files\"</c>) to "
            + "its URL, or returns <c>null</c> when the name is not in the spec.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"name\">The audience name.</param>");
        sb.AppendLine("    /// <returns>The audience URL or null if unknown.</returns>");
        sb.AppendLine("    public static string? Resolve(string name) =>");
        sb.AppendLine("        sr_byName.TryGetValue(name, out var url) ? url : null;");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Reverse-resolves an audience <paramref name=\"url\"/> (e.g. "
            + "<c>\"https://files.internal\"</c>) to its name, or returns <c>null</c> when "
            + "the URL is not in the spec.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"url\">The audience URL.</param>");
        sb.AppendLine("    /// <returns>The audience name or null if unknown.</returns>");
        sb.AppendLine("    public static string? ResolveByUrl(string url) =>");
        sb.AppendLine("        sr_byUrl.TryGetValue(url, out var name) ? name : null;");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Gets all registered audience URLs (defensive read-only set).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IReadOnlySet<string> AllUrls => sr_allUrls;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Gets the name → URL map for all registered audiences.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    public static IReadOnlyDictionary<string, string> ByName => sr_byName;");
        sb.AppendLine();

        sb.AppendLine("    private static readonly HashSet<string> sr_allUrls =");
        sb.AppendLine("        new(StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var audience in ordered)
            sb.AppendLine($"        \"{EscapeStringLiteral(audience.Url)}\",");

        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly Dictionary<string, string> sr_byName =");
        sb.AppendLine("        new(StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var audience in ordered)
        {
            sb.AppendLine(
                $"        [\"{EscapeStringLiteral(audience.Name)}\"] = "
                + $"\"{EscapeStringLiteral(audience.Url)}\",");
        }

        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine("    private static readonly Dictionary<string, string> sr_byUrl =");
        sb.AppendLine("        new(StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var audience in ordered)
        {
            sb.AppendLine(
                $"        [\"{EscapeStringLiteral(audience.Url)}\"] = "
                + $"\"{EscapeStringLiteral(audience.Name)}\",");
        }

        sb.AppendLine("    };");
    }

    private static string EscapeStringLiteral(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"");

    private static string EscapeXmlDoc(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");

    private static bool IsAsciiUpperLetter(char c) => c is >= 'A' and <= 'Z';

    private static bool IsAsciiLetter(char c) =>
        (c is >= 'A' and <= 'Z') || (c is >= 'a' and <= 'z');

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';
}
