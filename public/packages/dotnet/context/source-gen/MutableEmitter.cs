// -----------------------------------------------------------------------
// <copyright file="MutableEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Pure logic for emitting <c>MutableRequestContext</c> from the COMBINED
/// non-derived properties of all parsed context specs in a generator run.
/// Derived properties (e.g. impersonation flavor / impersonator org) are
/// emitted as computed getters that walk the actor chain.
/// </summary>
/// <remarks>
/// Cross-transport propagation of the small operational subset
/// (<c>RequestId</c>, <c>RequestPath</c>, fingerprints, <c>WhoIsHashId</c>)
/// happens via the hand-written <c>PropagatedContext</c> record + a single
/// transport-portable header (<c>x-d2-context</c>). Identity (UserId, OrgId,
/// Scopes, etc.) rebuilds at every hop from the JWT — the messaging /
/// gRPC layers do not propagate it.
/// </remarks>
internal static class MutableEmitter
{
    private const string _TARGET_NAMESPACE = "DcsvIo.D2.Context.Abstractions";
    private const string _CLASS_NAME = "MutableRequestContext";
    private const string _REQUEST_INTERFACE =
        "DcsvIo.D2.Context.Abstractions.IRequestContext";

    /// <summary>
    /// Emits the mutable concrete from the combined property set of
    /// <paramref name="auth"/> + <paramref name="request"/>.
    /// </summary>
    /// <param name="auth">The auth-context spec.</param>
    /// <param name="request">The request-context spec.</param>
    /// <returns>The mutable emit result.</returns>
    public static EmitResult Emit(ContextSpec auth, ContextSpec request)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();

        // Property-name collision check across the combined hierarchy.
        // EXCEPTION: properties that intentionally map to the same JWT claim
        // (e.g. Subject as string? AND UserId as Guid? — both reading "sub" with
        // different parse modes) are allowed when they live in the same spec.
        var allProps = new List<(ContextSpec Spec, PropertySpec Property)>();
        foreach (var section in auth.Sections)
        {
            foreach (var prop in section.Properties)
                allProps.Add((auth, prop));
        }

        foreach (var section in request.Sections)
        {
            foreach (var prop in section.Properties)
                allProps.Add((request, prop));
        }

        var seen = new Dictionary<string, ContextSpec>(StringComparer.Ordinal);
        foreach (var (spec, prop) in allProps)
        {
            if (seen.TryGetValue(prop.Name, out var firstSpec))
            {
                if (!ReferenceEquals(firstSpec, spec))
                {
                    diagnostics.Add(EmitDiagnostics.PropertyNameCollision(
                        prop.Name, firstSpec.Name, spec.Name));
                }
            }
            else
            {
                seen[prop.Name] = spec;
            }
        }

        // allProps is built for collision detection above; reading it here
        // keeps the variable from looking unused now that the envelope
        // emission has been retired.
        _ = allProps;

        var mutableSource = EmitMutable(auth, request);
        return new EmitResult($"{_CLASS_NAME}.g.cs", mutableSource, diagnostics.ToImmutable());
    }

    private static string EmitMutable(ContextSpec auth, ContextSpec request)
    {
        var sb = new StringBuilder();
        EmitFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Security.Claims;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using DcsvIo.D2.Auth.Abstractions;");
        sb.AppendLine("using DcsvIo.D2.Utilities.Attributes;");
        sb.AppendLine("using DcsvIo.D2.Utilities.Enums;");
        sb.AppendLine("using DcsvIo.D2.Utilities.Extensions;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_TARGET_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Mutable concrete implementation of");
        sb.AppendLine("/// <see cref=\"global::" + _REQUEST_INTERFACE + "\"/>.");
        sb.AppendLine(
            "/// Populated by transport-specific middleware (aspnetcore for HTTP,");
        sb.AppendLine("/// messaging-rabbitmq for RabbitMQ, etc.); domain code receives it as");
        sb.AppendLine("/// <see cref=\"global::" + _REQUEST_INTERFACE + "\"/>");
        sb.AppendLine("/// only (read-only contract).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public sealed class {_CLASS_NAME} : global::{_REQUEST_INTERFACE}");
        sb.AppendLine("{");

        EmitMutableSections(sb, auth);
        sb.AppendLine();
        EmitMutableSections(sb, request);
        sb.AppendLine();

        EmitFromJwtPayloadNoValidation(sb, auth, request);
        sb.AppendLine();
        EmitFromClaims(sb, auth, request);

        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static void EmitMutableSections(StringBuilder sb, ContextSpec spec)
    {
        var first = true;
        foreach (var section in spec.Sections)
        {
            if (!first)
                sb.AppendLine();

            first = false;
            sb.AppendLine($"    #region {section.Name}");
            sb.AppendLine();

            var firstProp = true;
            foreach (var prop in section.Properties)
            {
                if (!firstProp)
                    sb.AppendLine();

                firstProp = false;
                EmitMutableProperty(sb, prop, indent: 1);
            }

            sb.AppendLine();
            sb.AppendLine($"    #endregion");
        }
    }

    private static void EmitMutableProperty(StringBuilder sb, PropertySpec prop, int indent)
    {
        var pad = new string(' ', indent * 4);

        EmitXmlDocSummary(sb, prop.Doc ?? prop.Name, indent);

        if (prop.Redact)
            sb.AppendLine($"{pad}[RedactData(Reason = RedactReason.PersonalInformation)]");

        if (prop.Derived.Falsey())
        {
            var defaultExpr = prop.Default.Falsey()
                ? TypeVocabulary.DefaultExpression(prop.Type)
                : prop.Default!;
            sb.AppendLine($"{pad}public {prop.Type} {prop.Name} {{ get; set; }} = {defaultExpr};");
            return;
        }

        if (prop.Derived == "actorChain")
        {
            EmitActorChainDerivedGetter(sb, prop, indent);
            return;
        }

        // Unknown derived rule (D2CTX005 already fired in InterfaceEmitter — emit a placeholder).
        sb.AppendLine($"{pad}public {prop.Type} {prop.Name} => default;");
    }

    private static void EmitActorChainDerivedGetter(
        StringBuilder sb, PropertySpec prop, int indent)
    {
        var pad = new string(' ', indent * 4);
        var p4 = pad + "    ";
        var p8 = pad + "        ";
        var p12 = pad + "            ";
        var firstImp = "ActorChain.FirstOrDefault(a => a.Kind == ActorKind.Impersonation)";

        switch (prop.Name)
        {
            case "ImmediateCallerClientId":
                // Per RFC 8693 §4.1: outermost act = current actor = immediate caller.
                // ActorChain is flattened outermost-first, so FirstOrDefault is correct.
                sb.AppendLine($"{pad}public {prop.Type} {prop.Name}");
                sb.AppendLine($"{pad}{{");
                sb.AppendLine($"{p4}get");
                sb.AppendLine($"{p4}{{");
                sb.AppendLine(
                    $"{p8}var entry = ActorChain.FirstOrDefault(a "
                    + "=> a.Kind == ActorKind.Service);");
                sb.AppendLine($"{p8}return entry?.ClientId ?? entry?.Subject;");
                sb.AppendLine($"{p4}}}");
                sb.AppendLine($"{pad}}}");
                return;

            case "OriginatingClientId":
                // Per RFC 8693 §4.1: most-deeply-nested act = earliest actor = originator.
                // ActorChain is flattened outermost-first, so LastOrDefault is the deepest entry.
                // Fallback to Subject for pure service-identity tokens (no act chain).
                sb.AppendLine($"{pad}public {prop.Type} {prop.Name}");
                sb.AppendLine($"{pad}{{");
                sb.AppendLine($"{p4}get");
                sb.AppendLine($"{p4}{{");
                sb.AppendLine(
                    $"{p8}var entry = ActorChain.LastOrDefault(a "
                    + "=> a.Kind == ActorKind.Service);");
                sb.AppendLine($"{p8}if (entry is not null)");
                sb.AppendLine($"{p8}{{");
                sb.AppendLine($"{p12}return entry.ClientId ?? entry.Subject;");
                sb.AppendLine($"{p8}}}");
                sb.AppendLine();
                sb.AppendLine(
                    $"{p8}// Pure service-identity token (no act chain): sub IS the client_id.");
                sb.AppendLine($"{p8}return UserId is null ? Subject : null;");
                sb.AppendLine($"{p4}}}");
                sb.AppendLine($"{pad}}}");
                return;

            case "IsServiceIdentity":
                sb.AppendLine($"{pad}public {prop.Type} {prop.Name}");
                sb.AppendLine($"{pad}{{");
                sb.AppendLine($"{p4}get");
                sb.AppendLine($"{p4}{{");
                sb.AppendLine($"{p8}if (IsAuthenticated is null)");
                sb.AppendLine($"{p8}{{");
                sb.AppendLine($"{p12}return null;");
                sb.AppendLine($"{p8}}}");
                sb.AppendLine();
                sb.AppendLine(
                    $"{p8}// Service identity = no UserId AND no impersonation in chain.");
                sb.AppendLine(
                    $"{p8}return UserId is null && !ActorChain.Any(a "
                    + "=> a.Kind == ActorKind.Impersonation);");
                sb.AppendLine($"{p4}}}");
                sb.AppendLine($"{pad}}}");
                return;

            case "IsImpersonating":
                sb.AppendLine($"{pad}public {prop.Type} {prop.Name}");
                sb.AppendLine($"{pad}{{");
                sb.AppendLine($"{p4}get");
                sb.AppendLine($"{p4}{{");
                sb.AppendLine($"{p8}if (IsAuthenticated is null)");
                sb.AppendLine($"{p8}{{");
                sb.AppendLine($"{p12}return null;");
                sb.AppendLine($"{p8}}}");
                sb.AppendLine();
                sb.AppendLine(
                    $"{p8}return ActorChain.Any(a => a.Kind == ActorKind.Impersonation);");
                sb.AppendLine($"{p4}}}");
                sb.AppendLine($"{pad}}}");
                return;

            case "ImpersonationKind":
                sb.AppendLine($"{pad}public {prop.Type} {prop.Name} =>");
                sb.AppendLine($"{p4}{firstImp}?.ImpersonationKind;");
                return;

            case "ImpersonatedBy":
                sb.AppendLine($"{pad}public {prop.Type} {prop.Name}");
                sb.AppendLine($"{pad}{{");
                sb.AppendLine($"{p4}get");
                sb.AppendLine($"{p4}{{");
                sb.AppendLine($"{p8}var subject = {firstImp}?.Subject;");
                sb.AppendLine(
                    $"{p8}return subject.TryParseTruthyNull(out Guid? g) ? g : null;");
                sb.AppendLine($"{p4}}}");
                sb.AppendLine($"{pad}}}");
                return;

            case "ImpersonationSessionId":
                sb.AppendLine($"{pad}public {prop.Type} {prop.Name} =>");
                sb.AppendLine($"{p4}{firstImp}?.SessionId;");
                return;

            case "ImpersonatorOrgId":
                sb.AppendLine($"{pad}public {prop.Type} {prop.Name} =>");
                sb.AppendLine($"{p4}{firstImp}?.OrgId;");
                return;

            case "ImpersonatorOrgName":
                sb.AppendLine($"{pad}public {prop.Type} {prop.Name} =>");
                sb.AppendLine($"{p4}{firstImp}?.OrgName;");
                return;

            case "ImpersonatorOrgType":
                sb.AppendLine($"{pad}public {prop.Type} {prop.Name} =>");
                sb.AppendLine($"{p4}{firstImp}?.OrgType;");
                return;

            case "ImpersonatorOrgRole":
                sb.AppendLine($"{pad}public {prop.Type} {prop.Name} =>");
                sb.AppendLine($"{p4}{firstImp}?.OrgRole;");
                return;

            default:
                sb.AppendLine($"{pad}public {prop.Type} {prop.Name} => default;");
                return;
        }
    }

    private static void EmitFromJwtPayloadNoValidation(
        StringBuilder sb, ContextSpec auth, ContextSpec request)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// **DOES NOT VALIDATE THE JWT.** Reads claims from "
            + "<paramref name=\"payload\"/>");
        sb.AppendLine(
            "    /// and populates the mutable context. Sets <c>IsAuthenticated = false</c>");
        sb.AppendLine(
            "    /// deliberately — the caller MUST set <c>IsAuthenticated = true</c> after");
        sb.AppendLine(
            "    /// confirming JWT signature, expiry, audience, and issuer via the auth");
        sb.AppendLine("    /// middleware (typically DcsvIo.D2.Auth's JwtValidator).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine("    /// <para>");
        sb.AppendLine(
            "    /// This factory is intentionally named <c>NoValidation</c> to make the");
        sb.AppendLine(
            "    /// pre-condition impossible to miss. It exists for two scenarios only:");
        sb.AppendLine("    /// <list type=\"bullet\">");
        sb.AppendLine(
            "    ///   <item>Auth middleware that has ALREADY validated signature/expiry/aud and");
        sb.AppendLine(
            "    ///   only needs claim-shape conversion before flipping the IsAuthenticated "
            + "bit.</item>");
        sb.AppendLine(
            "    ///   <item>Test fixtures that need to construct contexts without round-tripping");
        sb.AppendLine("    ///   through real JWT issuance.</item>");
        sb.AppendLine("    /// </list>");
        sb.AppendLine("    /// </para>");
        sb.AppendLine("    /// <para>");
        sb.AppendLine(
            "    /// Calling this method with attacker-controlled JsonElement and shipping the");
        sb.AppendLine(
            "    /// resulting context as authenticated is a forged-token impersonation "
            + "primitive.");
        sb.AppendLine(
            "    /// Always pair with full JWT validation BEFORE setting "
            + "<c>IsAuthenticated = true</c>.");
        sb.AppendLine("    /// </para>");
        sb.AppendLine("    /// </remarks>");
        sb.AppendLine(
            "    /// <param name=\"payload\">The JWT payload as a JsonElement "
            + "(NOT VALIDATED).</param>");
        sb.AppendLine(
            "    /// <returns>A new mutable context with claim-shape fields populated and");
        sb.AppendLine("    /// <c>IsAuthenticated = false</c>.</returns>");
        sb.AppendLine(
            $"    public static {_CLASS_NAME} FromJwtPayloadNoValidation(JsonElement payload)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var ctx = new {_CLASS_NAME}();");
        sb.AppendLine(
            "        ctx.IsAuthenticated = false;  // ⚠ caller MUST set true after validation");
        sb.AppendLine();
        sb.AppendLine(
            "        // Defensive: a JWT payload that isn't a JSON object can't carry claims.");
        sb.AppendLine(
            "        // Caller still gets back a context with IsAuthenticated=false so auth");
        sb.AppendLine("        // middleware fails closed; we just don't try to read fields.");
        sb.AppendLine("        if (payload.ValueKind != JsonValueKind.Object)");
        sb.AppendLine("            return ctx;");

        foreach (var spec in (ContextSpec[])[auth, request])
        {
            foreach (var section in spec.Sections)
            {
                foreach (var prop in section.Properties)
                {
                    if (prop.Derived.Truthy() || prop.Claim.Falsey())
                        continue;

                    EmitJwtPayloadAssignment(sb, prop);
                }
            }
        }

        sb.AppendLine("        return ctx;");
        sb.AppendLine("    }");
    }

    private static void EmitJwtPayloadAssignment(StringBuilder sb, PropertySpec prop)
    {
        var claim = prop.Claim!;
        sb.AppendLine(
            $"        if (payload.TryGetProperty(\"{Escape(claim)}\", "
            + $"out var el_{prop.Name}))");
        sb.AppendLine("        {");
        EmitTypeAwareJsonAssignment(sb, prop, $"el_{prop.Name}");
        sb.AppendLine("        }");
    }

    private static void EmitTypeAwareJsonAssignment(
        StringBuilder sb, PropertySpec prop, string elVar)
    {
        switch (prop.Type)
        {
            case TypeVocabulary.StringNullable:
                sb.AppendLine($"            if ({elVar}.ValueKind == JsonValueKind.String)");
                sb.AppendLine("            {");
                sb.AppendLine(
                    $"                ctx.{prop.Name} = {elVar}.GetString().ToNullIfEmpty();");
                sb.AppendLine("            }");
                return;

            case TypeVocabulary.BoolNullable:
                sb.AppendLine(
                    $"            if ({elVar}.ValueKind == JsonValueKind.True ||");
                sb.AppendLine(
                    $"                {elVar}.ValueKind == JsonValueKind.False)");
                sb.AppendLine("            {");
                sb.AppendLine($"                ctx.{prop.Name} = {elVar}.GetBoolean();");
                sb.AppendLine("            }");
                return;

            case TypeVocabulary.IntNullable:
                sb.AppendLine(
                    $"            if ({elVar}.ValueKind == JsonValueKind.Number &&");
                sb.AppendLine(
                    $"                {elVar}.TryGetInt32(out var v_{prop.Name}))");
                sb.AppendLine("            {");
                sb.AppendLine($"                ctx.{prop.Name} = v_{prop.Name};");
                sb.AppendLine("            }");
                return;

            case TypeVocabulary.DoubleNullable:
                sb.AppendLine(
                    $"            if ({elVar}.ValueKind == JsonValueKind.Number &&");
                sb.AppendLine(
                    $"                {elVar}.TryGetDouble(out var v_{prop.Name}))");
                sb.AppendLine("            {");
                sb.AppendLine($"                ctx.{prop.Name} = v_{prop.Name};");
                sb.AppendLine("            }");
                return;

            case TypeVocabulary.GuidNullable:
                sb.AppendLine(
                    $"            if ({elVar}.ValueKind == JsonValueKind.String &&");
                sb.AppendLine(
                    $"                {elVar}.GetString()"
                    + $".TryParseTruthyNull(out Guid? v_{prop.Name}))");
                sb.AppendLine("            {");
                sb.AppendLine($"                ctx.{prop.Name} = v_{prop.Name};");
                sb.AppendLine("            }");
                return;

            case TypeVocabulary.DateTimeOffsetNullable:
                sb.AppendLine(
                    $"            if ({elVar}.ValueKind == JsonValueKind.Number &&");
                sb.AppendLine(
                    $"                {elVar}.TryGetInt64(out var v_{prop.Name}))");
                sb.AppendLine("            {");
                sb.AppendLine(
                    $"                ctx.{prop.Name} = "
                    + $"DateTimeOffset.FromUnixTimeSeconds(v_{prop.Name});");
                sb.AppendLine("            }");
                sb.AppendLine(
                    $"            else if ({elVar}.ValueKind == JsonValueKind.String &&");
                sb.AppendLine(
                    $"                DateTimeOffset.TryParse("
                    + $"{elVar}.GetString(), out var vs_{prop.Name}))");
                sb.AppendLine("            {");
                sb.AppendLine($"                ctx.{prop.Name} = vs_{prop.Name};");
                sb.AppendLine("            }");
                return;

            case TypeVocabulary.OrgTypeNullable:
                EmitJsonEnumAssignment(sb, prop, elVar, "OrgType");
                return;

            case TypeVocabulary.RoleNullable:
                EmitJsonEnumAssignment(sb, prop, elVar, "Role");
                return;

            case TypeVocabulary.ActorKindNullable:
                EmitJsonEnumAssignment(sb, prop, elVar, "ActorKind");
                return;

            case TypeVocabulary.ImpersonationKindNullable:
                EmitJsonEnumAssignment(sb, prop, elVar, "ImpersonationKind");
                return;

            case TypeVocabulary.ActorChainList:
                sb.AppendLine(
                    $"            ctx.{prop.Name} = ActorChainParser.ParseFromJson({elVar});");
                return;

            case TypeVocabulary.StringList:
                // Per RFC 7519 §4.1.3 — claim value MAY be a single string OR an
                // array of strings.
                sb.AppendLine($"            if ({elVar}.ValueKind == JsonValueKind.String)");
                sb.AppendLine("            {");
                sb.AppendLine(
                    $"                var s_{prop.Name} = "
                    + $"{elVar}.GetString().ToNullIfEmpty();");
                sb.AppendLine($"                if (s_{prop.Name} is not null)");
                sb.AppendLine(
                    $"                    ctx.{prop.Name} = new[] {{ s_{prop.Name} }};");
                sb.AppendLine("            }");
                sb.AppendLine($"            else if ({elVar}.ValueKind == JsonValueKind.Array)");
                sb.AppendLine("            {");
                sb.AppendLine($"                var list_{prop.Name} = new List<string>();");
                sb.AppendLine(
                    $"                foreach (var item_{prop.Name} in "
                    + $"{elVar}.EnumerateArray())");
                sb.AppendLine("                {");
                sb.AppendLine(
                    $"                    if (item_{prop.Name}.ValueKind == "
                    + $"JsonValueKind.String &&");
                sb.AppendLine(
                    $"                        item_{prop.Name}.GetString()"
                    + $".ToNullIfEmpty() is {{ }} si_{prop.Name})");
                sb.AppendLine("                    {");
                sb.AppendLine($"                        list_{prop.Name}.Add(si_{prop.Name});");
                sb.AppendLine("                    }");
                sb.AppendLine("                }");
                sb.AppendLine($"                if (list_{prop.Name}.Count > 0)");
                sb.AppendLine(
                    $"                    ctx.{prop.Name} = list_{prop.Name};");
                sb.AppendLine("            }");
                return;

            case TypeVocabulary.StringSet:
                sb.AppendLine($"            ctx.{prop.Name} = ScopeClaimParser.Parse({elVar});");
                return;
        }
    }

    private static void EmitJsonEnumAssignment(
        StringBuilder sb, PropertySpec prop, string elVar, string enumName)
    {
        sb.AppendLine(
            $"            if ({elVar}.ValueKind == JsonValueKind.String &&");
        sb.AppendLine(
            $"                {elVar}.GetString()"
            + $".TryParseTruthyNull<{enumName}>(out var v_{prop.Name}))");
        sb.AppendLine("            {");
        sb.AppendLine($"                ctx.{prop.Name} = v_{prop.Name};");
        sb.AppendLine("            }");
    }

    private static void EmitFromClaims(StringBuilder sb, ContextSpec auth, ContextSpec request)
    {
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Builds a <see cref=\"" + _CLASS_NAME + "\"/> from an authenticated");
        sb.AppendLine(
            "    /// <see cref=\"ClaimsPrincipal\"/> (typical AspNetCore JWT path). Sets only");
        sb.AppendLine(
            "    /// auth-derived fields; transport-level fields require their respective");
        sb.AppendLine("    /// filling extensions to populate.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <remarks>");
        sb.AppendLine(
            "    /// Trusts that the auth middleware has already validated signature, expiry,");
        sb.AppendLine(
            "    /// audience, and issuer before constructing the ClaimsPrincipal — that's the");
        sb.AppendLine("    /// AspNetCore authentication middleware's contract.");
        sb.AppendLine("    /// </remarks>");
        sb.AppendLine("    /// <param name=\"principal\">The authenticated principal.</param>");
        sb.AppendLine(
            "    /// <returns>A new mutable context with auth fields populated.</returns>");
        sb.AppendLine($"    public static {_CLASS_NAME} FromClaims(ClaimsPrincipal principal)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var ctx = new {_CLASS_NAME}();");
        sb.AppendLine(
            "        ctx.IsAuthenticated = principal.Identity?.IsAuthenticated ?? false;");

        foreach (var spec in (ContextSpec[])[auth, request])
        {
            foreach (var section in spec.Sections)
            {
                foreach (var prop in section.Properties)
                {
                    if (prop.Derived.Truthy() || prop.Claim.Falsey())
                        continue;

                    EmitClaimsAssignment(sb, prop);
                }
            }
        }

        sb.AppendLine("        return ctx;");
        sb.AppendLine("    }");
    }

    private static void EmitClaimsAssignment(StringBuilder sb, PropertySpec prop)
    {
        var claim = prop.Claim!;

        // Multi-valued claims (e.g. RFC 7519 §4.1.3 aud) get FindAll because
        // ASP.NET expands array claims into multiple Claim entries.
        if (prop.Type == TypeVocabulary.StringList)
        {
            sb.AppendLine(
                $"        var claims_{prop.Name} = "
                + $"principal.FindAll(\"{Escape(claim)}\")");
            sb.AppendLine($"            .Select(c => c.Value.ToNullIfEmpty())");
            sb.AppendLine($"            .Where(v => v is not null)");
            sb.AppendLine($"            .Cast<string>()");
            sb.AppendLine($"            .ToList();");
            sb.AppendLine($"        if (claims_{prop.Name}.Count > 0)");
            sb.AppendLine("        {");
            sb.AppendLine($"            ctx.{prop.Name} = claims_{prop.Name};");
            sb.AppendLine("        }");
            return;
        }

        var local = $"claim_{prop.Name}";
        sb.AppendLine($"        var {local} = principal.FindFirst(\"{Escape(claim)}\")?.Value;");
        sb.AppendLine($"        if ({local} is not null)");
        sb.AppendLine("        {");
        EmitTypeAwareClaimAssignment(sb, prop, local);
        sb.AppendLine("        }");
    }

    private static void EmitTypeAwareClaimAssignment(
        StringBuilder sb, PropertySpec prop, string local)
    {
        switch (prop.Type)
        {
            case TypeVocabulary.StringNullable:
                sb.AppendLine($"            ctx.{prop.Name} = {local}.ToNullIfEmpty();");
                return;

            case TypeVocabulary.BoolNullable:
                sb.AppendLine($"            if (bool.TryParse({local}, out var v_{prop.Name}))");
                sb.AppendLine("            {");
                sb.AppendLine($"                ctx.{prop.Name} = v_{prop.Name};");
                sb.AppendLine("            }");
                return;

            case TypeVocabulary.IntNullable:
                sb.AppendLine($"            if (int.TryParse({local}, out var v_{prop.Name}))");
                sb.AppendLine("            {");
                sb.AppendLine($"                ctx.{prop.Name} = v_{prop.Name};");
                sb.AppendLine("            }");
                return;

            case TypeVocabulary.DoubleNullable:
                sb.AppendLine(
                    $"            if (double.TryParse(");
                sb.AppendLine(
                    $"                {local},");
                sb.AppendLine(
                    $"                System.Globalization.NumberStyles.Float,");
                sb.AppendLine(
                    $"                System.Globalization.CultureInfo.InvariantCulture,");
                sb.AppendLine(
                    $"                out var v_{prop.Name}))");
                sb.AppendLine("            {");
                sb.AppendLine($"                ctx.{prop.Name} = v_{prop.Name};");
                sb.AppendLine("            }");
                return;

            case TypeVocabulary.GuidNullable:
                sb.AppendLine(
                    $"            if ({local}"
                    + $".TryParseTruthyNull(out Guid? v_{prop.Name}))");
                sb.AppendLine("            {");
                sb.AppendLine($"                ctx.{prop.Name} = v_{prop.Name};");
                sb.AppendLine("            }");
                return;

            case TypeVocabulary.DateTimeOffsetNullable:
                sb.AppendLine(
                    $"            if (long.TryParse({local}, out var ts_{prop.Name}))");
                sb.AppendLine("            {");
                sb.AppendLine(
                    $"                ctx.{prop.Name} = "
                    + $"DateTimeOffset.FromUnixTimeSeconds(ts_{prop.Name});");
                sb.AppendLine("            }");
                sb.AppendLine(
                    $"            else if (DateTimeOffset.TryParse("
                    + $"{local}, out var vs_{prop.Name}))");
                sb.AppendLine("            {");
                sb.AppendLine($"                ctx.{prop.Name} = vs_{prop.Name};");
                sb.AppendLine("            }");
                return;

            case TypeVocabulary.OrgTypeNullable:
                EmitClaimEnumAssignment(sb, prop, local, "OrgType");
                return;

            case TypeVocabulary.RoleNullable:
                EmitClaimEnumAssignment(sb, prop, local, "Role");
                return;

            case TypeVocabulary.ActorKindNullable:
                EmitClaimEnumAssignment(sb, prop, local, "ActorKind");
                return;

            case TypeVocabulary.ImpersonationKindNullable:
                EmitClaimEnumAssignment(sb, prop, local, "ImpersonationKind");
                return;

            case TypeVocabulary.ActorChainList:
                sb.AppendLine(
                    $"            ctx.{prop.Name} = "
                    + $"ActorChainParser.ParseFromJsonString({local});");
                return;

            case TypeVocabulary.StringSet:
                sb.AppendLine(
                    $"            ctx.{prop.Name} = ScopeClaimParser.ParseString({local});");
                return;
        }
    }

    private static void EmitClaimEnumAssignment(
        StringBuilder sb, PropertySpec prop, string local, string enumName)
    {
        sb.AppendLine(
            $"            if ({local}"
            + $".TryParseTruthyNull<{enumName}>(out var v_{prop.Name}))");
        sb.AppendLine("            {");
        sb.AppendLine($"                ctx.{prop.Name} = v_{prop.Name};");
        sb.AppendLine("            }");
    }

    private static void EmitFileHeader(StringBuilder sb)
    {
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//   Generated by DcsvIo.D2.Context.SourceGen.ContextGenerator");
        sb.AppendLine(
            "//   from contracts/{auth,request}-context/*.spec.json (the source of truth).");
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

    private static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"");
}
