// -----------------------------------------------------------------------
// <copyright file="RegistryCollisionChecker.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Registry.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Build-time safety gate that enforces two cross-catalog invariants across
/// all loaded error-code specs:
/// <list type="bullet">
///   <item>
///     No two catalogs declare the same <c>code</c> string — if they do,
///     <see cref="RegistryDiagnosticIds.CrossCatalogDuplicateCode"/>
///     (<c>D2ERC004</c>) fires.
///   </item>
///   <item>
///     The reserved-namespace rule is respected — unprefixed codes (no
///     <c>_</c> anywhere in the code after the first character) belong
///     exclusively to the generic catalog (<c>common</c> domain); per-domain
///     catalogs must prefix every code with their domain token in
///     SCREAMING_SNAKE (e.g. <c>AUTH_</c>). Violations surface as
///     <see cref="RegistryDiagnosticIds.ReservedNamespaceViolation"/>
///     (<c>D2ERC005</c>).
///   </item>
/// </list>
/// When any diagnostic fires the caller MUST NOT emit the registry (the
/// registry would be incomplete or ambiguous).
/// </summary>
internal static class RegistryCollisionChecker
{
    private const string _GENERIC_DOMAIN = "common";

    /// <summary>
    /// Runs both cross-catalog checks against the full set of aggregated
    /// entries. Returns one diagnostic per violation found; empty when the
    /// catalog set is clean.
    /// </summary>
    /// <param name="entries">
    /// All registry entries aggregated from every spec, in the order they
    /// were loaded.
    /// </param>
    /// <returns>
    /// Diagnostics for cross-catalog collisions + reserved-namespace
    /// violations; empty when the catalog set is clean.
    /// </returns>
    public static ImmutableArray<EmitDiagnostic> Check(
        IReadOnlyList<RegistrySpecEntry> entries)
    {
        var diagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();

        CheckCrossCollisions(entries, diagnostics);
        CheckReservedNamespace(entries, diagnostics);

        return diagnostics.ToImmutable();
    }

    private static void CheckCrossCollisions(
        IReadOnlyList<RegistrySpecEntry> entries,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        // code → first SpecFileName that declared it.
        var seenCodes = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (!seenCodes.TryGetValue(entry.Code, out var firstSpec))
            {
                seenCodes[entry.Code] = entry.SpecFileName;
                continue;
            }

            // Only emit the diagnostic once per (code, pair) — using the
            // stable "first spec" reference and the current spec name.
            diagnostics.Add(RegistryDiagnostics.CrossCatalogDuplicateCode(
                entry.Code, firstSpec, entry.SpecFileName));
        }
    }

    private static void CheckReservedNamespace(
        IReadOnlyList<RegistrySpecEntry> entries,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        // Collect the set of non-generic domain tokens present in the
        // aggregated catalog. These are the UPPERCASE prefixes that
        // per-domain codes MUST start with (e.g. "AUTH", "GEO").
        var domainPrefixes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (!string.Equals(entry.Domain, _GENERIC_DOMAIN, StringComparison.Ordinal))
                domainPrefixes.Add(entry.Domain.ToUpperInvariant());
        }

        foreach (var entry in entries)
        {
            var isGenericCatalog = string.Equals(
                entry.Domain, _GENERIC_DOMAIN, StringComparison.Ordinal);

            if (isGenericCatalog)
            {
                // Generic catalog codes must NOT start with any known domain
                // prefix (e.g. AUTH_, GEO_). The domain prefix is derived from
                // the code's first underscore-delimited segment compared against
                // the set of known non-generic domain tokens.
                var codePrefix = CodePrefixSegment(entry.Code);
                if (codePrefix is not null && domainPrefixes.Contains(codePrefix))
                {
                    diagnostics.Add(RegistryDiagnostics.ReservedNamespaceViolation(
                        entry.Code,
                        entry.SpecFileName,
                        $"the generic catalog must not contain domain-prefixed codes; "
                        + $"code '{entry.Code}' starts with the '{codePrefix}_' domain prefix "
                        + $"which belongs in the '{codePrefix.ToLowerInvariant()}' catalog"));
                }
            }
            else
            {
                // Per-domain catalog codes must start with {DOMAIN.UPPER}_ .
                // D2ERC001 already enforces this per-catalog; the registry
                // re-asserts it at the cross-catalog level so a code that
                // somehow bypasses the per-catalog check still fails the build.
                var expectedPrefix = entry.Domain.ToUpperInvariant() + "_";
                if (!entry.Code.StartsWith(expectedPrefix, StringComparison.Ordinal))
                {
                    diagnostics.Add(RegistryDiagnostics.ReservedNamespaceViolation(
                        entry.Code,
                        entry.SpecFileName,
                        $"per-domain codes must start with the domain prefix '{expectedPrefix}'; "
                        + $"code '{entry.Code}' in domain '{entry.Domain}' lacks the prefix"));
                }
            }
        }
    }

    /// <summary>
    /// Returns the all-uppercase prefix segment before the first underscore
    /// in <paramref name="code"/>, or <see langword="null"/> when the first
    /// segment is not all uppercase letters (indicating a non-domain-prefix
    /// pattern).
    /// </summary>
    /// <param name="code">SCREAMING_SNAKE error code.</param>
    /// <returns>
    /// The uppercase prefix segment (e.g. <c>"AUTH"</c> for
    /// <c>"AUTH_BEARER_MISSING"</c>), or <see langword="null"/> when the
    /// code does not start with a domain-prefix pattern.
    /// </returns>
    private static string? CodePrefixSegment(string code)
    {
        var underscoreIdx = code.IndexOf('_');
        if (underscoreIdx <= 0)
            return null;

        var prefix = code.Substring(0, underscoreIdx);
        foreach (var ch in prefix)
        {
            if (ch < 'A' || ch > 'Z')
                return null;
        }

        return prefix;
    }
}
