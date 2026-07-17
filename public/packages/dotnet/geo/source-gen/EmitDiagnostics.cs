// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce <see cref="EmitDiagnostic"/>
/// instances with geo-source-gen descriptor IDs (<c>D2GEO*</c>). The
/// diagnostic record itself lives in <c>DcsvIo.D2.SourceGen</c> (shared
/// across every source generator); only the per-topic factory shape lives
/// here.
/// </summary>
internal static class EmitDiagnostics
{
    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MalformedSpec"/>
    /// diagnostic.
    /// </summary>
    /// <param name="fileName">The spec filename.</param>
    /// <param name="reason">The parse-failure reason.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MalformedSpec(string fileName, string reason) =>
        new(DiagnosticIds.MalformedSpec, [fileName, reason]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.UnknownFk"/> diagnostic.
    /// </summary>
    /// <param name="fieldName">The FK field name.</param>
    /// <param name="entityName">The owning entity name.</param>
    /// <param name="code">The unresolvable code value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic UnknownFk(string fieldName, string entityName, string code) =>
        new(DiagnosticIds.UnknownFk, [fieldName, entityName, code]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.FkAmbiguity"/>
    /// diagnostic.
    /// </summary>
    /// <param name="fieldName">The unclassifiable field name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic FkAmbiguity(string fieldName) =>
        new(DiagnosticIds.FkAmbiguity, [fieldName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidIdentifier"/>
    /// diagnostic.
    /// </summary>
    /// <param name="code">The offending code value.</param>
    /// <param name="emissionTarget">The emission target that was skipped.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidIdentifier(string code, string emissionTarget) =>
        new(DiagnosticIds.InvalidIdentifier, [code, emissionTarget]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.VocabularyViolation"/>
    /// diagnostic.
    /// </summary>
    /// <param name="specName">The spec file name.</param>
    /// <param name="forbiddenIdentifier">The forbidden identifier surfaced.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic VocabularyViolation(string specName, string forbiddenIdentifier) =>
        new(DiagnosticIds.VocabularyViolation, [specName, forbiddenIdentifier]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MissingCatalogMetadata"/>
    /// diagnostic.
    /// </summary>
    /// <param name="specName">The spec file name.</param>
    /// <param name="fieldName">The missing or invalid metadata field name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingCatalogMetadata(string specName, string fieldName) =>
        new(DiagnosticIds.MissingCatalogMetadata, [specName, fieldName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MissingSpec"/>
    /// diagnostic.
    /// </summary>
    /// <param name="specName">The spec file name that should be present.</param>
    /// <param name="assemblyName">The assembly being emitted into.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingSpec(string specName, string assemblyName) =>
        new(DiagnosticIds.MissingSpec, [specName, assemblyName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.LocaleMessageMismatch"/>
    /// diagnostic.
    /// </summary>
    /// <param name="tag">The unmatched IETF tag.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic LocaleMessageMismatch(string tag) =>
        new(DiagnosticIds.LocaleMessageMismatch, [tag]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.StructuralParityMismatch"/>
    /// diagnostic.
    /// </summary>
    /// <param name="specName">The spec name.</param>
    /// <param name="fieldName">The spec field that was not matched.</param>
    /// <param name="recordName">The emitted record that should have carried it.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic StructuralParityMismatch(
        string specName,
        string fieldName,
        string recordName) =>
        new(DiagnosticIds.StructuralParityMismatch, [specName, fieldName, recordName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateNormalizedName"/>
    /// diagnostic.
    /// </summary>
    /// <param name="specName">The catalog spec file name.</param>
    /// <param name="normalizedName">The collision string (post-NFD + casefold).</param>
    /// <param name="entityIds">Comma-joined identifiers of the colliding entities.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateNormalizedName(
        string specName,
        string normalizedName,
        string entityIds) =>
        new(DiagnosticIds.DuplicateNormalizedName, [specName, normalizedName, entityIds]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MissingLocaleReference"/>
    /// diagnostic.
    /// </summary>
    /// <param name="countryCode">The country code carrying the bad ref.</param>
    /// <param name="localeTag">The locale tag absent from locales.spec.json.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingLocaleReference(
        string countryCode,
        string localeTag) =>
        new(DiagnosticIds.MissingLocaleReference, [countryCode, localeTag]);
}
