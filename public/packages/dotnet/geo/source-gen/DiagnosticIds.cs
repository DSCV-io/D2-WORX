// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by <see cref="GeoGenerator"/>.
/// Kept in a separate class from <see cref="DiagnosticDescriptors"/> so
/// non-Roslyn-host consumers (unit tests of the pure-logic loader / emitters)
/// can reference the IDs without dragging in <c>Microsoft.CodeAnalysis</c>.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Geo spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2GEO001";

    /// <summary>
    /// FK code refers to an entity not present in the target catalog
    /// (e.g. a country primary-currency referencing an ISO 4217 alpha code
    /// absent from <c>currencies.spec.json</c>).
    /// </summary>
    public const string UnknownFk = "D2GEO002";

    /// <summary>
    /// Field name doesn't match the FK / M:M naming convention AND no
    /// <c>fkTo</c> annotation supplied to disambiguate.
    /// </summary>
    public const string FkAmbiguity = "D2GEO003";

    /// <summary>
    /// Subdivision / Locale / Timezone code cannot form a valid C# identifier
    /// even after sanitization. (Reserved for nested-class shell emission.)
    /// </summary>
    public const string InvalidIdentifier = "D2GEO004";

    /// <summary>
    /// Vocabulary discipline violation — field name uses
    /// <c>region</c> / <c>state</c> / <c>province</c> at identifier position
    /// for the ISO 3166-2 concept; use <c>subdivision</c>.
    /// </summary>
    public const string VocabularyViolation = "D2GEO005";

    /// <summary>
    /// Missing or invalid <c>catalogVersion</c> / <c>generatedAt</c> in a
    /// spec file (needed for <c>GeoCatalog</c> constant emission).
    /// </summary>
    public const string MissingCatalogMetadata = "D2GEO006";

    /// <summary>
    /// A required spec file is missing from <c>AdditionalFiles</c>. Nine
    /// spec files are required at the Abstractions / Default emission
    /// boundaries (seven pipeline-derived plus two scaffold-authored).
    /// </summary>
    public const string MissingSpec = "D2GEO007";

    /// <summary>
    /// Build-time consistency mismatch — <c>selectable-locales.spec.json</c>
    /// lists an IETF tag with no corresponding
    /// <c>contracts/messages/{tag}.json</c> file (or vice versa).
    /// </summary>
    public const string LocaleMessageMismatch = "D2GEO008";

    /// <summary>
    /// Structural-parity mismatch — spec field exists but the emitter did
    /// not produce a matching record property.
    /// </summary>
    public const string StructuralParityMismatch = "D2GEO009";

    /// <summary>
    /// Catalog-uniqueness violation — two entities in the same catalog share
    /// the same normalized name across any matchable name field (DisplayName,
    /// OfficialName, EndonymDisplayName, EndonymOfficialName, ShortCode,
    /// alias, ISO code). Eliminates the "first match wins" determinism risk
    /// in <c>IGeoNameResolver</c> at the source — fail-closed by design.
    /// </summary>
    public const string DuplicateNormalizedName = "D2GEO010";

    /// <summary>
    /// A country references a locale tag (via
    /// <c>primaryLocaleIETFBCP47Tag</c> or <c>localeIETFBCP47Tags[]</c>) that
    /// has no corresponding entry in <c>locales.spec.json</c>. Surfaced
    /// during spec validation so the data emitters can emit direct indexer
    /// access (fail-loud) rather than defensive <c>TryGetValue + skip</c>
    /// patterns that mask drift between catalogs.
    /// </summary>
    /// <remarks>
    /// D2GEO011 is reserved on the TS-side for the geo-data-pipeline's
    /// <c>GEO_CLDR_ZOMBIE_DROPPED</c> (no .NET equivalent — the pipeline is
    /// TS-only). D2GEO012 is the first slot shared across both languages
    /// after that carve-out.
    /// </remarks>
    public const string MissingLocaleReference = "D2GEO012";
}
