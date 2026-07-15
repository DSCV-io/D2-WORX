// -----------------------------------------------------------------------
// <copyright file="RegistrySpecLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Registry.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using DcsvIo.D2.ErrorCodes.SourceGen;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Aggregates all error-code spec files surfaced via <c>AdditionalFiles</c>
/// into a flat list of <see cref="RegistrySpecEntry"/> records. Each spec
/// is parsed using the shared <see cref="ErrorCodeSpecLoader"/> (reusing all
/// JSON-shape validation from the per-catalog engine) and enriched with the
/// registry-level <c>domain</c> token derived from the spec filename.
/// </summary>
/// <remarks>
/// <para>
/// Domain derivation: the generic spec
/// (<c>error-codes.spec.json</c>) maps to <c>common</c>; every per-domain
/// spec (<c>{domain}-error-codes.spec.json</c>) maps to the leading segment
/// (e.g. <c>auth-error-codes.spec.json</c> → <c>auth</c>).
/// </para>
/// <para>
/// Entries whose spec is missing any of the four factory fields
/// (<c>category</c> / <c>userMessageKey</c> / <c>factoryName</c> /
/// <c>factoryShape</c>) are skipped with a malformed-spec diagnostic — the
/// registry requires all 8 fields to be present on every entry (all current
/// spec entries satisfy this; the schema enforces it).
/// </para>
/// </remarks>
internal static class RegistrySpecLoader
{
    private const string _GENERIC_SPEC_NAME = "error-codes.spec.json";
    private const string _DOMAIN_SPEC_SUFFIX = "-error-codes.spec.json";
    private const string _GENERIC_DOMAIN = "common";

    /// <summary>
    /// Parses all surfaced spec files and returns the aggregated registry
    /// entries plus any load-time diagnostics.
    /// </summary>
    /// <param name="specFiles">
    /// All spec files surfaced via <c>AdditionalFiles</c>. The loader
    /// distinguishes the generic spec from domain specs by filename.
    /// </param>
    /// <param name="diagnostics">
    /// Builder to append load-time diagnostics to. Callers check this
    /// after return and abort emission on any error-severity diagnostic.
    /// </param>
    /// <returns>Aggregated entries in the order they were loaded.</returns>
    public static IReadOnlyList<RegistrySpecEntry> LoadAll(
        ImmutableArray<SpecFile> specFiles,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        var entries = new List<RegistrySpecEntry>();

        // Sort by spec filename for deterministic output order.
        var sorted = new List<SpecFile>(specFiles);
        sorted.Sort(static (a, b) =>
            string.Compare(
                Path.GetFileName(a.Path),
                Path.GetFileName(b.Path),
                StringComparison.Ordinal));

        foreach (var spec in sorted)
        {
            var fileName = Path.GetFileName(spec.Path);

            if (!IsErrorCodeSpec(fileName))
                continue;

            var domain = DeriveDomain(fileName);

            var loadResult = ErrorCodeSpecLoader.Load(
                spec.Path,
                spec.Content,
                malformedSpecId: RegistryDiagnosticIds.MalformedRegistrySpec);

            if (loadResult.Diagnostic is { } diag)
            {
                diagnostics.Add(diag);
                continue;
            }

            foreach (var entry in loadResult.Spec!.ErrorCodes)
            {
                // All 8 fields are required in the registry. Every valid spec
                // entry will have them; guard defensively.
                if (entry.Category is null || entry.UserMessageKey is null
                    || entry.FactoryName is null || entry.FactoryShape is null)
                {
                    diagnostics.Add(RegistryDiagnostics.MalformedRegistrySpec(
                        fileName,
                        $"entry '{entry.Code}' is missing required registry fields "
                        + "(category / userMessageKey / factoryName / factoryShape)"));
                    continue;
                }

                entries.Add(new RegistrySpecEntry(
                    Code: entry.Code,
                    HttpStatus: entry.HttpStatus,
                    Category: entry.Category,
                    UserMessageKey: entry.UserMessageKey,
                    FactoryName: entry.FactoryName,
                    FactoryShape: entry.FactoryShape,
                    Doc: entry.Doc,
                    Domain: domain,
                    SpecFileName: fileName,
                    IsDeprecated: entry.Deprecated));
            }
        }

        return entries;
    }

    /// <summary>
    /// Returns <see langword="true"/> for spec files the registry generator
    /// owns: either the generic <c>error-codes.spec.json</c> or any
    /// <c>*-error-codes.spec.json</c> domain spec.
    /// </summary>
    /// <param name="fileName">The filename (without path) to test.</param>
    /// <returns><see langword="true"/> when the file is an error-code spec.</returns>
    public static bool IsErrorCodeSpec(string? fileName)
    {
        if (fileName is null)
            return false;

        return string.Equals(fileName, _GENERIC_SPEC_NAME, StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(_DOMAIN_SPEC_SUFFIX, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Derives the domain token from a spec filename. The generic spec
    /// (<c>error-codes.spec.json</c>) returns <c>common</c>; domain specs
    /// (<c>auth-error-codes.spec.json</c>) return the leading segment
    /// before the <c>-error-codes</c> suffix (<c>auth</c>).
    /// </summary>
    /// <param name="fileName">The spec filename (e.g. <c>auth-error-codes.spec.json</c>).</param>
    /// <returns>The domain token (lowercase).</returns>
    public static string DeriveDomain(string fileName)
    {
        if (string.Equals(fileName, _GENERIC_SPEC_NAME, StringComparison.OrdinalIgnoreCase))
            return _GENERIC_DOMAIN;

        // Strip the -error-codes.spec.json suffix.
        var suffixLen = _DOMAIN_SPEC_SUFFIX.Length;
        var domainPart = fileName.Substring(0, fileName.Length - suffixLen);
        return domainPart.ToLowerInvariant();
    }
}
