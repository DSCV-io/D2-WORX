// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce
/// <see cref="EmitDiagnostic"/> instances with i18n-source-gen descriptor
/// IDs (<c>D2I18N*</c>). The diagnostic record itself lives in
/// <c>DcsvIo.D2.SourceGen</c> (shared across every source generator); only
/// the per-topic factory shape lives here.
/// </summary>
internal static class EmitDiagnostics
{
    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidTranslationKey"/> diagnostic.
    /// </summary>
    /// <param name="key">The offending JSON key.</param>
    /// <param name="reason">The reason the key was rejected.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidKey(string key, string reason) =>
        new(DiagnosticIds.InvalidTranslationKey, [key, reason]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MissingKeyInLocale"/> diagnostic.
    /// </summary>
    /// <param name="key">The translation key present in en-US.</param>
    /// <param name="locale">The locale missing the key.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingInLocale(string key, string locale) =>
        new(DiagnosticIds.MissingKeyInLocale, [key, locale]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.TranslationKeyCollision"/> diagnostic.
    /// </summary>
    /// <param name="firstKey">The winning JSON key (kept).</param>
    /// <param name="secondKey">The losing JSON key (skipped).</param>
    /// <param name="fullPath">The shared TK path the two keys both decompose to.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic Collision(string firstKey, string secondKey, string fullPath) =>
        new(DiagnosticIds.TranslationKeyCollision, [firstKey, secondKey, fullPath]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.OrphanKeyInLocale"/> diagnostic.
    /// </summary>
    /// <param name="key">The orphan key present only in the non-en-US locale.</param>
    /// <param name="locale">The locale containing the orphan key.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic OrphanInLocale(string key, string locale) =>
        new(DiagnosticIds.OrphanKeyInLocale, [key, locale]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MalformedJsonCatalog"/> diagnostic.
    /// </summary>
    /// <param name="fileName">The catalog filename (e.g. <c>"en-US.json"</c>).</param>
    /// <param name="reason">The parse failure reason.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MalformedJson(string fileName, string reason) =>
        new(DiagnosticIds.MalformedJsonCatalog, [fileName, reason]);
}
