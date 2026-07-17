// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by <see cref="TKGenerator"/>.
/// Kept in a separate class from <see cref="DiagnosticDescriptors"/> so
/// non-Roslyn-host consumers (e.g. unit tests of the pure-logic
/// <see cref="TKEmitter"/>) can reference the IDs without dragging in
/// <c>Microsoft.CodeAnalysis</c> (which the SrcGen csproj marks
/// <c>PrivateAssets="all"</c>).
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>JSON key cannot be decomposed into a valid TK constant path.</summary>
    public const string InvalidTranslationKey = "D2I18N001";

    /// <summary>Key present in en-US is missing from another locale catalog.</summary>
    public const string MissingKeyInLocale = "D2I18N002";

    /// <summary>Two distinct JSON keys decompose to the same TK path.</summary>
    public const string TranslationKeyCollision = "D2I18N003";

    /// <summary>Key exists in a non-en-US locale but has no matching entry in en-US.</summary>
    public const string OrphanKeyInLocale = "D2I18N004";

    /// <summary>Generator cannot find en-US.json among the supplied AdditionalFiles.</summary>
    public const string MissingEnUsJson = "D2I18N005";

    /// <summary>JSON catalog file cannot be parsed.</summary>
    public const string MalformedJsonCatalog = "D2I18N006";
}
