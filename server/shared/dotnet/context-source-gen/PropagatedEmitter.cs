// -----------------------------------------------------------------------
// <copyright file="PropagatedEmitter.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Context.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

/// <summary>
/// Emits the cross-hop <c>PropagatedContext</c> trio:
/// <list type="bullet">
///   <item><c>PropagatedContext.g.cs</c> — sealed record with the
///   <c>propagate: true</c> field subset.</item>
///   <item><c>PropagatedContextExtensions.g.cs</c> — projections
///   (<c>ToPropagatedContext</c> on <c>IRequestContext</c>,
///   <c>ApplyPropagatedContext</c> on <c>MutableRequestContext</c>).</item>
///   <item><c>PropagatedContextSerializer.g.cs</c> — base64url + JSON
///   encode/decode with per-field <c>maxLength</c> validation baked in
///   from the spec.</item>
/// </list>
/// </summary>
/// <remarks>
/// All three live in <c>D2.Shared.Context.Abstractions</c> alongside the
/// codegen-emitted interfaces + <c>MutableRequestContext</c>. The set of
/// propagatable fields is whatever the spec marks <c>propagate: true</c>;
/// per-field length caps come from the spec's <c>maxLength</c> on each
/// propagatable string field. This is the wire-format primitive a future
/// TS / Python / Go BFF would mirror from the same spec.
/// </remarks>
internal static class PropagatedEmitter
{
    private const string _TARGET_NAMESPACE = "D2.Shared.Context.Abstractions";
    private const string _RECORD_NAME = "PropagatedContext";
    private const string _EXTENSIONS_NAME = "PropagatedContextExtensions";
    private const string _SERIALIZER_NAME = "PropagatedContextSerializer";

    /// <summary>Emits all three files based on the propagate-marked
    /// properties from <paramref name="auth"/> + <paramref name="request"/>.
    /// Returns one <see cref="EmitResult"/> per file.</summary>
    /// <param name="auth">The auth-context spec.</param>
    /// <param name="request">The request-context spec.</param>
    public static IReadOnlyList<EmitResult> EmitAll(
        ContextSpec auth, ContextSpec request)
    {
        var props = CollectPropagated(auth, request);
        return
        [
            EmitRecord(props),
            EmitExtensions(props),
            EmitSerializer(props),
        ];
    }

    private static List<PropertySpec> CollectPropagated(
        ContextSpec auth, ContextSpec request)
    {
        var list = new List<PropertySpec>();
        foreach (var spec in new[] { auth, request })
        {
            foreach (var section in spec.Sections)
            {
                foreach (var prop in section.Properties)
                {
                    if (prop.Propagate)
                        list.Add(prop);
                }
            }
        }

        return list;
    }

    private static EmitResult EmitRecord(IReadOnlyList<PropertySpec> propagated)
    {
        var sb = new StringBuilder();
        EmitFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine($"namespace {_TARGET_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Cross-hop propagation subset of");
        sb.AppendLine(
            "/// <see cref=\"IRequestContext\"/>: the small set of fields a");
        sb.AppendLine(
            "/// downstream service can NOT recompute on its own and that are NOT");
        sb.AppendLine(
            "/// identity claims (identity rebuilds at every hop from the JWT).");
        sb.AppendLine(
            "/// Carried as a single header (<c>x-d2-context</c>) on AMQP / gRPC /");
        sb.AppendLine(
            "/// HTTP — same shape on every transport.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine(
            "/// Field set is spec-driven — every property marked <c>propagate: true</c>");
        sb.AppendLine(
            "/// in <c>contracts/request-context/IRequestContext.spec.json</c> appears");
        sb.AppendLine(
            "/// here. Per-field length caps from the spec's <c>maxLength</c> are");
        sb.AppendLine(
            "/// enforced by <see cref=\"" + _SERIALIZER_NAME + ".TryDecode\"/>.");
        sb.AppendLine("/// </remarks>");
        sb.AppendLine($"public sealed record {_RECORD_NAME}");
        sb.AppendLine("{");
        var first = true;
        foreach (var prop in propagated)
        {
            if (!first) sb.AppendLine();

            first = false;
            EmitXmlDocSummary(sb, prop.Doc ?? prop.Name, indent: 1);
            sb.AppendLine($"    public {prop.Type} {prop.Name} {{ get; init; }}");
        }

        sb.AppendLine();
        sb.AppendLine("    /// <summary>Gets a value indicating whether any propagated");
        sb.AppendLine("    /// field is set — lets callers skip the encode/serialize round-");
        sb.AppendLine("    /// trip when nothing meaningful is in the context.</summary>");
        sb.AppendLine("    public bool HasAnyField =>");
        for (var i = 0; i < propagated.Count; i++)
        {
            var p = propagated[i];
            var suffix = i == propagated.Count - 1 ? ";" : " ||";
            sb.AppendLine($"        {p.Name} is not null{suffix}");
        }

        sb.AppendLine("}");
        return new EmitResult(
            $"{_RECORD_NAME}.g.cs", sb.ToString(), ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static EmitResult EmitExtensions(IReadOnlyList<PropertySpec> propagated)
    {
        var sb = new StringBuilder();
        EmitFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine($"namespace {_TARGET_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Spec-driven projections between <see cref=\"IRequestContext\"/>");
        sb.AppendLine(
            "/// and <see cref=\"" + _RECORD_NAME + "\"/>. Codegen-emitted from");
        sb.AppendLine(
            "/// the <c>propagate: true</c> annotations on the request-context spec.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static class {_EXTENSIONS_NAME}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Projects the propagation subset of");
        sb.AppendLine($"    /// <paramref name=\"context\"/> into a <see cref=\"{_RECORD_NAME}\"/>.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"context\">Source context.</param>");
        sb.AppendLine($"    public static {_RECORD_NAME} ToPropagatedContext(this IRequestContext context)");
        sb.AppendLine("    {");
        sb.AppendLine("        System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine($"        return new {_RECORD_NAME}");
        sb.AppendLine("        {");
        foreach (var prop in propagated)
            sb.AppendLine($"            {prop.Name} = context.{prop.Name},");

        sb.AppendLine("        };");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Applies <paramref name=\"propagated\"/> onto a");
        sb.AppendLine($"    /// <see cref=\"MutableRequestContext\"/> — copies every");
        sb.AppendLine("    /// non-null propagation field. No-op when arg is null.</summary>");
        sb.AppendLine("    /// <param name=\"context\">Mutable context to populate.</param>");
        sb.AppendLine("    /// <param name=\"propagated\">Decoded propagation record.</param>");
        sb.AppendLine("    public static void ApplyPropagatedContext(");
        sb.AppendLine("        this MutableRequestContext context,");
        sb.AppendLine($"        {_RECORD_NAME}? propagated)");
        sb.AppendLine("    {");
        sb.AppendLine("        System.ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("        if (propagated is null) return;");
        sb.AppendLine();
        foreach (var prop in propagated)
        {
            sb.AppendLine($"        if (propagated.{prop.Name} is not null)");
            sb.AppendLine($"            context.{prop.Name} = propagated.{prop.Name};");
            sb.AppendLine();
        }

        // Trim trailing blank line for cleanliness.
        var src = sb.ToString().TrimEnd('\r', '\n', ' ');
        sb.Clear();
        sb.Append(src);
        sb.AppendLine();
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return new EmitResult(
            $"{_EXTENSIONS_NAME}.g.cs", sb.ToString(), ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static EmitResult EmitSerializer(IReadOnlyList<PropertySpec> propagated)
    {
        var sb = new StringBuilder();
        EmitFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_TARGET_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Wire encode / decode for <see cref=\"" + _RECORD_NAME + "\"/> as a single");
        sb.AppendLine(
            "/// header value. Identical encoding on every transport — AMQP header");
        sb.AppendLine(
            "/// (<c>x-d2-context</c>), gRPC metadata, HTTP header — so transports");
        sb.AppendLine(
            "/// don't need transport-specific serialization logic.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("/// <remarks>");
        sb.AppendLine(
            "/// Encoding: canonical-JSON (camelCase, omit-null, no whitespace) → UTF-8 →");
        sb.AppendLine(
            "/// base64url. Base64url is chosen over plain base64 to keep the value safe");
        sb.AppendLine(
            "/// in HTTP headers (no <c>+</c> / <c>/</c> / <c>=</c> meta-chars). Typical");
        sb.AppendLine(
            "/// payload is ~150–300 bytes; well under any practical header limit.");
        sb.AppendLine("/// </remarks>");
        sb.AppendLine($"public static class {_SERIALIZER_NAME}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Hard cap on accepted header length. Defends downstream");
        sb.AppendLine("    /// memory + parser cost from a hostile or accidentally-bloated header.");
        sb.AppendLine("    /// 2 KiB base64url decodes to ~1.5 KiB of JSON — far above any");
        sb.AppendLine($"    /// legitimate <see cref=\"{_RECORD_NAME}\"/> payload.</summary>");
        sb.AppendLine("    public const int MAX_HEADER_LENGTH = 2048;");
        sb.AppendLine();
        sb.AppendLine("    private static readonly JsonSerializerOptions sr_jsonOptions = new()");
        sb.AppendLine("    {");
        sb.AppendLine("        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,");
        sb.AppendLine("        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,");
        sb.AppendLine("        WriteIndented = false,");
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Encodes a <see cref=\"" + _RECORD_NAME + "\"/> as a");
        sb.AppendLine("    /// base64url-of-JSON string suitable for any transport's single-value");
        sb.AppendLine("    /// header slot.</summary>");
        sb.AppendLine($"    /// <param name=\"context\">The context to encode. Must not be null.</param>");
        sb.AppendLine("    /// <returns>The encoded header value.</returns>");
        sb.AppendLine($"    public static string Encode({_RECORD_NAME} context)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(context);");
        sb.AppendLine("        var json = JsonSerializer.SerializeToUtf8Bytes(context, sr_jsonOptions);");
        sb.AppendLine("        return Base64Url.Encode(json);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Decodes a header value previously produced by");
        sb.AppendLine("    /// <see cref=\"Encode\"/>. Returns null on any failure path");
        sb.AppendLine("    /// (malformed base64, bad JSON, oversize wire payload, any single");
        sb.AppendLine("    /// field exceeding its <c>maxLength</c> spec cap): the consumer");
        sb.AppendLine("    /// then proceeds with a null context — propagation is opportunistic,");
        sb.AppendLine("    /// never required.</summary>");
        sb.AppendLine("    /// <param name=\"encoded\">The encoded header value.</param>");
        sb.AppendLine($"    public static {_RECORD_NAME}? TryDecode(string? encoded)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (string.IsNullOrWhiteSpace(encoded)) return null;");
        sb.AppendLine("        if (encoded.Length > MAX_HEADER_LENGTH) return null;");
        sb.AppendLine();
        sb.AppendLine("        try");
        sb.AppendLine("        {");
        sb.AppendLine("            var json = Base64Url.Decode(encoded);");
        sb.AppendLine($"            var context = JsonSerializer.Deserialize<{_RECORD_NAME}>(json, sr_jsonOptions);");
        sb.AppendLine("            return context is null || !FieldsWithinBounds(context) ? null : context;");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (FormatException)");
        sb.AppendLine("        {");
        sb.AppendLine("            return null;");
        sb.AppendLine("        }");
        sb.AppendLine("        catch (JsonException)");
        sb.AppendLine("        {");
        sb.AppendLine("            return null;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Per-field length cap. The wire-level");
        sb.AppendLine("    /// <see cref=\"MAX_HEADER_LENGTH\"/> guard bounds total payload size;");
        sb.AppendLine("    /// this method bounds individual fields so a forged context can't");
        sb.AppendLine("    /// blow up downstream log scope keys / DB columns / etc. with a");
        sb.AppendLine("    /// 1.5 KiB single field. Returning <c>false</c> drops the entire");
        sb.AppendLine("    /// context — propagation is opportunistic, never required.</summary>");
        sb.AppendLine($"    private static bool FieldsWithinBounds({_RECORD_NAME} ctx)");
        sb.AppendLine("    {");
        var anyBoundedField = false;
        foreach (var prop in propagated)
        {
            if (prop.MaxLength is not { } max) continue;

            anyBoundedField = true;
            sb.AppendLine($"        if (ctx.{prop.Name} is {{ Length: > {max} }}) return false;");
        }

        if (!anyBoundedField)
            sb.AppendLine("        // No spec-declared maxLength bounds on any field.");

        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Tiny base64url codec. .NET's");
        sb.AppendLine("    /// <c>Convert.ToBase64String</c> uses the URL-unsafe alphabet");
        sb.AppendLine("    /// (<c>+</c>, <c>/</c>, <c>=</c>); we swap them.</summary>");
        sb.AppendLine("    private static class Base64Url");
        sb.AppendLine("    {");
        sb.AppendLine("        public static string Encode(byte[] bytes)");
        sb.AppendLine("        {");
        sb.AppendLine("            return Convert.ToBase64String(bytes)");
        sb.AppendLine("                .TrimEnd('=')");
        sb.AppendLine("                .Replace('+', '-')");
        sb.AppendLine("                .Replace('/', '_');");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public static byte[] Decode(string s)");
        sb.AppendLine("        {");
        sb.AppendLine("            var padded = s.Replace('-', '+').Replace('_', '/');");
        sb.AppendLine("            var pad = padded.Length % 4;");
        sb.AppendLine("            if (pad > 0) padded = padded.PadRight(padded.Length + (4 - pad), '=');");
        sb.AppendLine("            return Convert.FromBase64String(padded);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return new EmitResult(
            $"{_SERIALIZER_NAME}.g.cs", sb.ToString(), ImmutableArray<EmitDiagnostic>.Empty);
    }

    private static void EmitFileHeader(StringBuilder sb)
    {
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//   Generated by D2.Shared.Context.SourceGen.PropagatedEmitter");
        sb.AppendLine(
            "//   from contracts/request-context/IRequestContext.spec.json (the source of truth).");
        sb.AppendLine("//   Manual edits will be lost on rebuild.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
    }

    private static void EmitXmlDocSummary(StringBuilder sb, string text, int indent)
    {
        var pad = new string(' ', indent * 4);
        var lines = text.Split('\n');
        sb.AppendLine($"{pad}/// <summary>");
        foreach (var line in lines)
            sb.AppendLine($"{pad}/// {EscapeXmlDoc(line.TrimEnd('\r'))}");

        sb.AppendLine($"{pad}/// </summary>");
    }

    private static string EscapeXmlDoc(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
