// -----------------------------------------------------------------------
// <copyright file="ScopesEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Scopes.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Pure logic for emitting the <c>Scopes</c> static partial class source from
/// a parsed <see cref="ScopesSpec"/>. Stateless and unit-testable in isolation.
/// The Roslyn-host integration (<see cref="ScopesGenerator"/>) calls this and
/// translates emit-side <see cref="EmitDiagnostic"/> records into Roslyn
/// <c>Diagnostic</c> instances.
/// </summary>
internal static class ScopesEmitter
{
    private const string _DEFAULT_ROOT_NAMESPACE = "DcsvIo.D2.Auth.Abstractions";
    private const string _DEFAULT_SCOPES_CLASS_NAME = "Scopes";
    private const string _ANON_PREFIX = "anon.";

    // Internal workload scopes (internal.*) are granted by the internal
    // transaction-token mint at the Edge boundary — NOT by the per-(OrgType, Role)
    // grant matrix — so they legitimately omit grantedTo. They are NOT anonymous
    // (no pre-auth universal grant): no user org-role can ever be granted them, which
    // is exactly the intended reachability for a service-to-service / in-process scope.
    private const string _INTERNAL_PREFIX = "internal.";

    // Mirror of DcsvIo.D2.Auth.Abstractions.ActionSensitivity members.
    // ActionSensitivity is small and stable; it's safe to keep static.
    // OrgType + Role members come from the compilation symbol at codegen time
    // (passed in via Emit's orgTypeNames / roleNames params) so adding a new
    // enum member doesn't require updating this file in lockstep.
    private static readonly HashSet<string> sr_actionSensitivityNames =
        new(StringComparer.Ordinal) { "Routine", "Sensitive", "Critical" };

    /// <summary>
    /// Emits the Scopes class source plus diagnostics for the supplied spec.
    /// </summary>
    /// <param name="spec">Parsed scope spec.</param>
    /// <param name="orgTypeNames">
    /// OrgType enum member names extracted from the compilation symbol by
    /// <see cref="ScopesGenerator"/>. Used for <c>grantedTo</c> validation +
    /// wildcard expansion. Adding a new <c>OrgType</c> member triggers
    /// re-emission with the expanded set automatically.
    /// </param>
    /// <param name="roleNames">Role enum member names — same source.</param>
    /// <param name="rootNamespace">Emit namespace (public or private product type).</param>
    /// <param name="className">Emit class name (<c>Scopes</c> or <c>ProductScopes</c>).</param>
    /// <returns>Generated source + diagnostics.</returns>
    public static EmitResult Emit(
        ScopesSpec spec,
        IReadOnlyList<string> orgTypeNames,
        IReadOnlyList<string> roleNames,
        string? rootNamespace = null,
        string? className = null)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var orgTypeSet = new HashSet<string>(orgTypeNames, StringComparer.Ordinal);
        var roleSet = new HashSet<string>(roleNames, StringComparer.Ordinal);
        var effectiveNamespace = rootNamespace ?? _DEFAULT_ROOT_NAMESPACE;
        var effectiveClassName = className ?? _DEFAULT_SCOPES_CLASS_NAME;

        // Per-scope semantic validation (name shape, enum values, etc.).
        var validScopes = new List<ScopeEntry>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var scope in spec.Scopes)
        {
            if (!seenNames.Add(scope.Name))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateScope(scope.Name));
                continue;
            }

            if (!ValidateScopeName(scope.Name, out var nameReason))
            {
                diagnostics.Add(EmitDiagnostics.InvalidScopeName(scope.Name, nameReason));
                continue;
            }

            if (!sr_actionSensitivityNames.Contains(scope.ActionSensitivity))
            {
                diagnostics.Add(EmitDiagnostics.UnknownEnumValue(
                    scope.Name,
                    "actionSensitivity",
                    "ActionSensitivity",
                    scope.ActionSensitivity,
                    string.Join(
                        ", ",
                        sr_actionSensitivityNames.OrderBy(n => n, StringComparer.Ordinal))));
                continue;
            }

            var isAnon = scope.Name.StartsWith(_ANON_PREFIX, StringComparison.Ordinal);
            var isInternal = scope.Name.StartsWith(_INTERNAL_PREFIX, StringComparison.Ordinal);

            // D2SCP005 (warning only — keep the scope): anon scope marked
            // impersonationBlocked is meaningless noise.
            if (isAnon && scope.ImpersonationBlocked)
                diagnostics.Add(EmitDiagnostics.AnonImpersonationBlockedNoise(scope.Name));

            // D2SCP008: a scope that no caller can ever be granted is unreachable. A
            // non-anon scope normally needs grantedTo. Internal workload scopes
            // (internal.*) are the exception: they are granted by the internal
            // transaction-token mint, NOT the org-role grant matrix, so they
            // legitimately omit grantedTo (no user org-role can hold them).
            if (!isAnon && !isInternal && scope.GrantedTo is null)
            {
                diagnostics.Add(EmitDiagnostics.MissingGrantedTo(scope.Name));
                continue;
            }

            // grantedTo entries: validate keys/values, reject empty arrays.
            var grantedToValid = true;
            if (scope.GrantedTo is { } granted)
            {
                foreach (var kvp in granted)
                {
                    if (kvp.Value.Length == 0)
                    {
                        diagnostics.Add(EmitDiagnostics.EmptyRoleArray(scope.Name, kvp.Key));
                        grantedToValid = false;
                        break;
                    }

                    if (kvp.Key != "*" && !orgTypeSet.Contains(kvp.Key))
                    {
                        diagnostics.Add(EmitDiagnostics.UnknownEnumValue(
                            scope.Name,
                            kvp.Key,
                            "OrgType",
                            kvp.Key,
                            "* (wildcard), " + string.Join(", ", orgTypeNames)));
                        grantedToValid = false;
                        break;
                    }

                    foreach (var role in kvp.Value)
                    {
                        if (role != "*" && !roleSet.Contains(role))
                        {
                            diagnostics.Add(EmitDiagnostics.UnknownEnumValue(
                                scope.Name,
                                kvp.Key,
                                "Role",
                                role,
                                "* (wildcard), " + string.Join(", ", roleNames)));
                            grantedToValid = false;
                            break;
                        }
                    }

                    if (!grantedToValid)
                        break;
                }
            }

            if (!grantedToValid)
                continue;

            validScopes.Add(scope);
        }

        // Tree-position collision detection (D2SCP007). For every pair of valid
        // scopes, if one's name is a strict dot-prefix of the other, the prefix
        // scope cannot be both a leaf constant and a parent class. Drop the
        // SHORTER (parent) one to keep the longer path emittable.
        var collidingNames = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < validScopes.Count; i++)
        {
            for (var j = 0; j < validScopes.Count; j++)
            {
                if (i == j)
                    continue;

                var a = validScopes[i].Name;
                var b = validScopes[j].Name;
                if (b.StartsWith(a + ".", StringComparison.Ordinal))
                {
                    if (collidingNames.Add(a))
                        diagnostics.Add(EmitDiagnostics.TreePositionCollision(a, b));
                }
            }
        }

        if (collidingNames.Count > 0)
            validScopes = validScopes.Where(s => !collidingNames.Contains(s.Name)).ToList();

        // Build the tree of scope-name segments and emit source.
        var tree = new TreeNode(string.Empty);
        foreach (var scope in validScopes.OrderBy(s => s.Name, StringComparer.Ordinal))
            tree.AddScope(scope);

        var source = EmitSource(
            tree, validScopes, orgTypeNames, roleNames, effectiveNamespace, effectiveClassName);
        return new EmitResult(source, diagnostics.ToImmutable());
    }

    private static bool ValidateScopeName(string name, out string reason)
    {
        if (name.Falsey())
        {
            reason = "name is empty";
            return false;
        }

        if (name.Contains(".."))
        {
            reason = "name contains consecutive dots";
            return false;
        }

        var segments = name.Split('.');
        if (segments.Length < 2)
        {
            reason = "name must have at least 2 dot-separated segments";
            return false;
        }

        foreach (var segment in segments)
        {
            if (segment.Length == 0)
            {
                reason = "leading or trailing dot in name";
                return false;
            }

            if (!IsAsciiLowerLetter(segment[0]))
            {
                reason = $"segment '{segment}' must start with a lowercase letter";
                return false;
            }

            for (var i = 1; i < segment.Length; i++)
            {
                var c = segment[i];
                if (!IsAsciiLowerLetter(c) && !IsAsciiDigit(c))
                {
                    reason =
                        $"segment '{segment}' contains invalid character '{c}' "
                        + "(only lowercase letters and digits allowed)";
                    return false;
                }
            }
        }

        reason = string.Empty;
        return true;
    }

    private static string EmitSource(
        TreeNode tree,
        IReadOnlyList<ScopeEntry> validScopes,
        IReadOnlyList<string> orgTypeNames,
        IReadOnlyList<string> roleNames,
        string rootNamespace,
        string className)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("//   Generated by DcsvIo.D2.Auth.Scopes.SourceGen.ScopesGenerator");
        sb.AppendLine("//   from contracts/auth-scopes/scopes.spec.json (the source of truth).");
        sb.AppendLine("//   Manual edits will be lost on rebuild.");
        sb.AppendLine("//");
        sb.AppendLine("//   Wildcards in grantedTo were expanded against the OrgType + Role enum");
        sb.AppendLine("//   members read from the compilation at codegen time:");
        sb.AppendLine("//     OrgType: " + string.Join(", ", orgTypeNames));
        sb.AppendLine("//     Role:    " + string.Join(", ", roleNames));
        sb.AppendLine("//   Adding a new enum member triggers re-emission automatically.");
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
            "/// OAuth-canonical scope string constants generated from "
            + "<c>contracts/auth-scopes/scopes.spec.json</c>.");
        sb.AppendLine(
            "/// Each scope is represented as a nested <c>const string</c> matching its "
            + "dot-separated path");
        sb.AppendLine($"/// (e.g. <c>{className}.Auth.User.Impersonate.Force</c>).");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public static partial class {className}");
        sb.AppendLine("{");

        EmitTreeNode(sb, tree, indent: 1);

        sb.AppendLine();
        EmitHelpers(sb, validScopes, orgTypeNames, roleNames);

        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static void EmitTreeNode(StringBuilder sb, TreeNode node, int indent)
    {
        var pad = new string(' ', indent * 4);

        var constantOrder = node.Constants.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        for (var i = 0; i < constantOrder.Count; i++)
        {
            var constName = constantOrder[i];
            var scope = node.Constants[constName];
            var doc = scope.Description ?? scope.Name;
            sb.AppendLine($"{pad}/// <summary>");
            sb.AppendLine($"{pad}/// {EscapeXmlDoc(doc)}");
            sb.AppendLine($"{pad}/// </summary>");
            sb.AppendLine(
                $"{pad}public const string {constName} = "
                + $"\"{EscapeStringLiteral(scope.Name)}\";");
            if (i < constantOrder.Count - 1 || node.Children.Count > 0)
                sb.AppendLine();
        }

        var childOrder = node.Children.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        for (var i = 0; i < childOrder.Count; i++)
        {
            var childName = childOrder[i];
            var childNode = node.Children[childName];
            sb.AppendLine($"{pad}/// <summary>");
            sb.AppendLine(
                $"{pad}/// Scopes nested under "
                + $"<c>{EscapeXmlDoc(childName.ToLowerInvariant())}</c>.");
            sb.AppendLine($"{pad}/// </summary>");
            sb.AppendLine($"{pad}public static class {childName}");
            sb.AppendLine($"{pad}{{");
            EmitTreeNode(sb, childNode, indent + 1);
            sb.AppendLine($"{pad}}}");
            if (i < childOrder.Count - 1)
                sb.AppendLine();
        }
    }

    private static void EmitHelpers(
        StringBuilder sb,
        IReadOnlyList<ScopeEntry> validScopes,
        IReadOnlyList<string> orgTypeNames,
        IReadOnlyList<string> roleNames)
    {
        var allScopes = validScopes
            .Select(s => s.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        var anonScopes = validScopes
            .Where(s => s.Name.StartsWith(_ANON_PREFIX, StringComparison.Ordinal))
            .Select(s => s.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        var blockedScopes = validScopes
            .Where(s =>
                s.ImpersonationBlocked
                && !s.Name.StartsWith(_ANON_PREFIX, StringComparison.Ordinal))
            .Select(s => s.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        // Granted scopes: expand wildcards into a (OrgType, Role) → SortedSet<string> map.
        var granted = new SortedDictionary<(string Org, string Role), SortedSet<string>>(
            Comparer<(string Org, string Role)>.Create((a, b) =>
            {
                var orgCompare = string.CompareOrdinal(a.Org, b.Org);
                return orgCompare != 0 ? orgCompare : string.CompareOrdinal(a.Role, b.Role);
            }));

        foreach (var scope in validScopes)
        {
            if (scope.GrantedTo is null)
                continue;

            foreach (var kvp in scope.GrantedTo)
            {
                IEnumerable<string> orgsFor = kvp.Key == "*" ? orgTypeNames : [kvp.Key];
                foreach (var org in orgsFor)
                {
                    foreach (var role in kvp.Value)
                    {
                        IEnumerable<string> rolesFor = role == "*" ? roleNames : [role];
                        foreach (var r in rolesFor)
                        {
                            var key = (org, r);
                            if (!granted.TryGetValue(key, out var set))
                            {
                                set = new SortedSet<string>(StringComparer.Ordinal);
                                granted[key] = set;
                            }

                            set.Add(scope.Name);
                        }
                    }
                }
            }
        }

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Returns the <see cref=\"ActionSensitivity\"/> classification for");
        sb.AppendLine(
            "    /// <paramref name=\"scope\"/>, or "
            + "<see cref=\"ActionSensitivity.Routine\"/>");
        sb.AppendLine("    /// for unknown scopes (defensive default).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"scope\">The scope string.</param>");
        sb.AppendLine("    /// <returns>The action sensitivity for the scope.</returns>");
        sb.AppendLine("    public static ActionSensitivity GetActionSensitivity(string scope) =>");
        sb.AppendLine(
            "        sr_actionSensitivity.TryGetValue(scope, out var s) "
            + "? s : ActionSensitivity.Routine;");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// True when the scope is stripped from impersonated tokens at Edge");
        sb.AppendLine("    /// mint time (defense-in-depth).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"scope\">The scope string.</param>");
        sb.AppendLine("    /// <returns>True if the scope is impersonation-blocked.</returns>");
        sb.AppendLine("    public static bool IsImpersonationBlocked(string scope) =>");
        sb.AppendLine("        sr_impersonationBlockedScopes.Contains(scope);");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// True when the scope is in the <c>anon.*</c> namespace "
            + "(pre-auth, no");
        sb.AppendLine("    /// caller identity required).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"scope\">The scope string.</param>");
        sb.AppendLine("    /// <returns>True if the scope is anonymous.</returns>");
        sb.AppendLine("    public static bool IsAnonymous(string scope) =>");
        sb.AppendLine("        scope.StartsWith(\"anon.\", StringComparison.Ordinal);");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// True when the scope appears in the spec (i.e. is a known scope name).");
        sb.AppendLine("    /// Useful for rejecting unknown scope strings early.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"scope\">The scope string.</param>");
        sb.AppendLine("    /// <returns>True if the scope is a known spec entry.</returns>");
        sb.AppendLine(
            "    public static bool IsKnown(string scope) => sr_allScopes.Contains(scope);");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// True when <paramref name=\"orgType\"/> + <paramref name=\"role\"/> "
            + "is granted");
        sb.AppendLine(
            "    /// <paramref name=\"scope\"/> per the spec's grantedTo matrix "
            + "(after wildcard expansion).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"scope\">The scope string.</param>");
        sb.AppendLine("    /// <param name=\"orgType\">The org type.</param>");
        sb.AppendLine("    /// <param name=\"role\">The role within the org.</param>");
        sb.AppendLine(
            "    /// <returns>True if the (orgType, role) pair is granted the scope."
            + "</returns>");
        sb.AppendLine(
            "    public static bool IsGrantedTo(string scope, OrgType orgType, Role role) =>");
        sb.AppendLine(
            "        sr_grantedScopes.TryGetValue((orgType, role), out var set) "
            + "&& set.Contains(scope);");
        sb.AppendLine();

        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Gets all scope names declared in the spec (defensive read-only set).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IReadOnlySet<string> AllScopes => sr_allScopes;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Gets the anonymous (<c>anon.*</c>) scope names — implicit universal "
            + "pre-auth grant.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    public static IReadOnlySet<string> AllAnonymousScopes => sr_anonymousScopes;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Gets the scopes that Edge strips from impersonated tokens at mint time.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    public static IReadOnlySet<string> AllImpersonationBlockedScopes =>");
        sb.AppendLine("        sr_impersonationBlockedScopes;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            "    /// Gets the (<see cref=\"OrgType\"/>, <see cref=\"Role\"/>) → granted scope "
            + "set, with wildcards expanded.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine(
            "    public static IReadOnlyDictionary<(OrgType, Role), IReadOnlySet<string>> "
            + "GrantedScopes =>");
        sb.AppendLine("        sr_grantedScopesView;");
        sb.AppendLine();

        sb.AppendLine(
            "    private static readonly HashSet<string> sr_allScopes =");
        sb.AppendLine("        new(StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var scope in allScopes)
            sb.AppendLine($"        \"{EscapeStringLiteral(scope)}\",");

        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine(
            "    private static readonly HashSet<string> sr_anonymousScopes =");
        sb.AppendLine("        new(StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var scope in anonScopes)
            sb.AppendLine($"        \"{EscapeStringLiteral(scope)}\",");

        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine(
            "    private static readonly HashSet<string> sr_impersonationBlockedScopes =");
        sb.AppendLine("        new(StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var scope in blockedScopes)
            sb.AppendLine($"        \"{EscapeStringLiteral(scope)}\",");

        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine(
            "    private static readonly Dictionary<string, ActionSensitivity> "
            + "sr_actionSensitivity =");
        sb.AppendLine("        new(StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var scope in validScopes.OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            sb.AppendLine(
                $"        [\"{EscapeStringLiteral(scope.Name)}\"] = "
                + $"ActionSensitivity.{scope.ActionSensitivity},");
        }

        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine(
            "    private static readonly Dictionary<(OrgType, Role), HashSet<string>> "
            + "sr_grantedScopesBacking =");
        sb.AppendLine("        new()");
        sb.AppendLine("    {");
        foreach (var kvp in granted)
        {
            sb.AppendLine(
                $"        [(OrgType.{kvp.Key.Org}, Role.{kvp.Key.Role})] = "
                + "new(StringComparer.Ordinal)");
            sb.AppendLine($"        {{");
            foreach (var scope in kvp.Value)
                sb.AppendLine($"            \"{EscapeStringLiteral(scope)}\",");

            sb.AppendLine($"        }},");
        }

        sb.AppendLine("    };");
        sb.AppendLine();

        sb.AppendLine(
            "    private static readonly Dictionary<(OrgType, Role), IReadOnlySet<string>> "
            + "sr_grantedScopes =");
        sb.AppendLine("        BuildGrantedScopesView();");
        sb.AppendLine();
        sb.AppendLine(
            "    private static readonly "
            + "IReadOnlyDictionary<(OrgType, Role), IReadOnlySet<string>> "
            + "sr_grantedScopesView =");
        sb.AppendLine("        sr_grantedScopes;");
        sb.AppendLine();
        sb.AppendLine(
            "    private static Dictionary<(OrgType, Role), IReadOnlySet<string>> "
            + "BuildGrantedScopesView()");
        sb.AppendLine("    {");
        sb.AppendLine(
            "        var result = "
            + "new Dictionary<(OrgType, Role), IReadOnlySet<string>>("
            + "sr_grantedScopesBacking.Count);");
        sb.AppendLine("        foreach (var kvp in sr_grantedScopesBacking)");
        sb.AppendLine("            result[kvp.Key] = kvp.Value;");
        sb.AppendLine();
        sb.AppendLine("        return result;");
        sb.AppendLine("    }");
    }

    private static string EscapeStringLiteral(string value) => value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"");

    private static string EscapeXmlDoc(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");

    private static bool IsAsciiLowerLetter(char c) => c is >= 'a' and <= 'z';

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';
}
