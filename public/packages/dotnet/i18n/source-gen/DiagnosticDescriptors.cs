// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared in
/// <see cref="DiagnosticIds"/>. Only loaded inside the Roslyn host (the
/// generator's <see cref="TKGenerator.Initialize"/> call site); pure-logic
/// callers should use <see cref="DiagnosticIds"/> string constants directly to
/// avoid pulling <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.InvalidTranslationKey"/>
    public static readonly DiagnosticDescriptor InvalidTranslationKey = new(
        id: DiagnosticIds.InvalidTranslationKey,
        title: "Translation key cannot be decomposed into TK constant",
        messageFormat: "Translation key '{0}' is invalid: {1}; the key was skipped",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingKeyInLocale"/>
    public static readonly DiagnosticDescriptor MissingKeyInLocale = new(
        id: DiagnosticIds.MissingKeyInLocale,
        title: "Translation key missing in locale",
        messageFormat: "Translation key '{0}' is present in en-US but missing in locale '{1}'",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.TranslationKeyCollision"/>
    public static readonly DiagnosticDescriptor TranslationKeyCollision = new(
        id: DiagnosticIds.TranslationKeyCollision,
        title: "Translation key collision after decomposition",
        messageFormat:
            "Translation keys '{0}' and '{1}' both decompose to '{2}'; the latter was skipped",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.OrphanKeyInLocale"/>
    public static readonly DiagnosticDescriptor OrphanKeyInLocale = new(
        id: DiagnosticIds.OrphanKeyInLocale,
        title: "Orphan translation key in locale",
        messageFormat:
            "Translation key '{0}' exists in locale '{1}' but is missing from en-US " +
            "(the source of truth); the key is not represented in TK",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingEnUsJson"/>
    public static readonly DiagnosticDescriptor MissingEnUsJson = new(
        id: DiagnosticIds.MissingEnUsJson,
        title: "Required en-US.json not found in AdditionalFiles",
        messageFormat:
            "The TK source generator could not locate 'en-US.json' among AdditionalFiles; " +
            "verify the consuming csproj declares the contracts/messages/*.json glob",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MalformedJsonCatalog"/>
    public static readonly DiagnosticDescriptor MalformedJsonCatalog = new(
        id: DiagnosticIds.MalformedJsonCatalog,
        title: "Translation catalog JSON is malformed",
        messageFormat: "Translation catalog '{0}' could not be parsed: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.I18n.SourceGen";
}
