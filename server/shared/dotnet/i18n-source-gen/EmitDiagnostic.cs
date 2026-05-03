// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostic.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.I18n.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// A diagnostic produced by <see cref="TKEmitter"/>. Decoupled from
/// <c>Microsoft.CodeAnalysis.Diagnostic</c> so the emitter is unit-testable
/// without instantiating a Roslyn host. The generator's
/// <see cref="TKGenerator.Initialize"/> translates these to real Roslyn
/// <c>Diagnostic</c> instances.
/// </summary>
/// <param name="DescriptorId">
/// Matches a <see cref="DiagnosticDescriptors"/> identifier (e.g. <c>"D2I18N001"</c>).
/// </param>
/// <param name="Args">
/// Arguments to format into the descriptor's <c>messageFormat</c> template.
/// </param>
internal sealed record EmitDiagnostic(string DescriptorId, ImmutableArray<object> Args)
{
    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidTranslationKey"/> diagnostic.
    /// </summary>
    /// <param name="key">The offending JSON key.</param>
    /// <param name="reason">The reason the key was rejected.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidKey(string key, string reason)
    {
        return new EmitDiagnostic(
            DiagnosticIds.InvalidTranslationKey,
            [key, reason]);
    }

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MissingKeyInLocale"/> diagnostic.
    /// </summary>
    /// <param name="key">The translation key present in en-US.</param>
    /// <param name="locale">The locale missing the key.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingInLocale(string key, string locale)
    {
        return new EmitDiagnostic(
            DiagnosticIds.MissingKeyInLocale,
            [key, locale]);
    }

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.TranslationKeyCollision"/> diagnostic.
    /// </summary>
    /// <param name="firstKey">The winning JSON key (kept).</param>
    /// <param name="secondKey">The losing JSON key (skipped).</param>
    /// <param name="fullPath">The shared TK path the two keys both decompose to.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic Collision(string firstKey, string secondKey, string fullPath)
    {
        return new EmitDiagnostic(
            DiagnosticIds.TranslationKeyCollision,
            [firstKey, secondKey, fullPath]);
    }

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.OrphanKeyInLocale"/> diagnostic.
    /// </summary>
    /// <param name="key">The orphan key present only in the non-en-US locale.</param>
    /// <param name="locale">The locale containing the orphan key.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic OrphanInLocale(string key, string locale)
    {
        return new EmitDiagnostic(
            DiagnosticIds.OrphanKeyInLocale,
            [key, locale]);
    }

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MalformedJsonCatalog"/> diagnostic.
    /// </summary>
    /// <param name="fileName">The catalog filename (e.g. <c>"en-US.json"</c>).</param>
    /// <param name="reason">The parse failure reason.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MalformedJson(string fileName, string reason)
    {
        return new EmitDiagnostic(
            DiagnosticIds.MalformedJsonCatalog,
            [fileName, reason]);
    }
}
