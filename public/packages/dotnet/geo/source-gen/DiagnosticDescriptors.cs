// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared
/// in <see cref="DiagnosticIds"/>. Only loaded inside the Roslyn host (the
/// generator's <see cref="GeoGenerator.Initialize"/> call site); pure-logic
/// callers should use <see cref="DiagnosticIds"/> string constants directly
/// to avoid pulling <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Geo spec file is malformed",
        messageFormat: "Geo spec '{0}' could not be parsed: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.UnknownFk"/>
    public static readonly DiagnosticDescriptor UnknownFk = new(
        id: DiagnosticIds.UnknownFk,
        title: "Unknown foreign-key reference in geo spec",
        messageFormat:
            "Geo spec field '{0}' on entity '{1}' references unknown code '{2}' "
            + "(no matching entry in target catalog)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.FkAmbiguity"/>
    public static readonly DiagnosticDescriptor FkAmbiguity = new(
        id: DiagnosticIds.FkAmbiguity,
        title: "FK detection ambiguity in geo spec",
        messageFormat:
            "Geo spec field '{0}' does not match the FK naming convention and has no "
            + "'fkTo' annotation; cannot classify",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidIdentifier"/>
    public static readonly DiagnosticDescriptor InvalidIdentifier = new(
        id: DiagnosticIds.InvalidIdentifier,
        title: "Geo code cannot form a valid C# identifier",
        messageFormat:
            "Geo code '{0}' cannot be sanitized into a valid C# identifier; "
            + "emission target '{1}' skipped",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.VocabularyViolation"/>
    public static readonly DiagnosticDescriptor VocabularyViolation = new(
        id: DiagnosticIds.VocabularyViolation,
        title: "Geo subdivision vocabulary discipline violation",
        messageFormat:
            "Geo spec '{0}' uses forbidden identifier '{1}' for the ISO 3166-2 concept; "
            + "use 'subdivision' (display strings on Subdivision.Type are exempt)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingCatalogMetadata"/>
    public static readonly DiagnosticDescriptor MissingCatalogMetadata = new(
        id: DiagnosticIds.MissingCatalogMetadata,
        title: "Geo catalog metadata missing or invalid",
        messageFormat:
            "Geo spec '{0}' is missing or has invalid catalog metadata field '{1}' "
            + "(needed for GeoCatalog emission)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingSpec"/>
    public static readonly DiagnosticDescriptor MissingSpec = new(
        id: DiagnosticIds.MissingSpec,
        title: "Required geo spec file not found in AdditionalFiles",
        messageFormat:
            "Required geo spec '{0}' was not found among AdditionalFiles for assembly '{1}'; "
            + "verify the consuming csproj declares the contracts/geo/*.spec.json glob",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.LocaleMessageMismatch"/>
    public static readonly DiagnosticDescriptor LocaleMessageMismatch = new(
        id: DiagnosticIds.LocaleMessageMismatch,
        title: "Selectable-locale spec does not match Paraglide messages files",
        messageFormat:
            "Selectable-locale tag '{0}' has no corresponding contracts/messages/{0}.json file "
            + "(or vice versa)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.StructuralParityMismatch"/>
    public static readonly DiagnosticDescriptor StructuralParityMismatch = new(
        id: DiagnosticIds.StructuralParityMismatch,
        title: "Geo emitter structural parity mismatch",
        messageFormat:
            "Geo spec '{0}' field '{1}' has no matching property on emitted record '{2}'",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateNormalizedName"/>
    /// <remarks>
    /// <para>
    /// Default severity is <see cref="DiagnosticSeverity.Error"/>; the
    /// diagnostic is <b>disabled by default</b> because the 2026-05-23
    /// Tier-2 catalog ships with 11 baseline subdivision-name collisions
    /// that pre-date this check (Spain ES-NA / ES-NC "Navarra" doublet,
    /// Indonesia ID-PA / ID-PP "Papua" post-split doublet, Malta MT-45 /
    /// MT-46 "Rabat" doublet, Guinea GN single-letter vs two-letter doublets,
    /// etc.). The infrastructure is in place; flip
    /// <c>isEnabledByDefault</c> to <c>true</c> once the catalog cleanup +
    /// overlay canonicalization land. Consumers MAY opt in early via
    /// <c>.editorconfig</c>:
    /// <code>dotnet_diagnostic.D2GEO010.severity = error</code>
    /// </para>
    /// <para>
    /// The name-resolver impl MUST treat any unresolved-by-construction
    /// collision as fail-closed (NotFound) regardless of this build-time
    /// gate's enabled state — silent first-wins picks are explicitly
    /// disallowed.
    /// </para>
    /// </remarks>
    public static readonly DiagnosticDescriptor DuplicateNormalizedName = new(
        id: DiagnosticIds.DuplicateNormalizedName,
        title: "Geo catalog has duplicate normalized name",
        messageFormat:
            "Geo spec '{0}' has two or more entities sharing normalized name '{1}': "
            + "{2}. Name-resolver would have to silently pick one — fail-closed "
            + "by design.",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: false);

    /// <inheritdoc cref="DiagnosticIds.MissingLocaleReference"/>
    public static readonly DiagnosticDescriptor MissingLocaleReference = new(
        id: DiagnosticIds.MissingLocaleReference,
        title: "Geo country references unknown locale",
        messageFormat:
            "Country '{0}' references locale '{1}' which is not present in "
            + "locales.spec.json",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.Geo.SourceGen";
}
