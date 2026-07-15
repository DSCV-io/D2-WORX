// -----------------------------------------------------------------------
// <copyright file="RegistryDiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Registry.SourceGen;

/// <summary>
/// String identifiers for the cross-catalog registry diagnostics. These
/// extend the <c>D2ERC*</c> engine-diagnostic family established in
/// <c>DcsvIo.D2.ErrorCodes.SourceGen.EngineDiagnosticIds</c>
/// (<c>D2ERC001</c>–<c>D2ERC003</c>) with three registry-level checks:
/// <list type="bullet">
///   <item>
///     <c>D2ERC004</c> — the same error <c>code</c> string appears in two
///     different catalogs (cross-catalog collision). The registry must have a
///     unique key per code; a collision means both catalogs are wrong and the
///     build fails.
///   </item>
///   <item>
///     <c>D2ERC005</c> — reserved-namespace violation: an unprefixed code
///     appears in a per-domain spec (only the generic catalog may own
///     unprefixed codes), OR a prefixed code appears in the generic spec
///     (per-domain prefixes are forbidden there). Either direction makes
///     the catalog ownership model ambiguous.
///   </item>
///   <item>
///     <c>D2ERC006</c> — malformed or invalid registry spec: the spec file
///     could not be parsed or is missing required fields. The build fails;
///     no registry is emitted.
///   </item>
///   <item>
///     <c>D2ERC007</c> — a code's <c>category</c> wire string is not a member
///     of the closed <c>ErrorCategory</c> set defined in
///     <c>error-category.spec.json</c>. Without this check the generated file
///     would reference a non-existent enum member and fail to compile with an
///     opaque error. Build fails; no registry is emitted.
///   </item>
/// </list>
/// Kept as plain string constants so pure-logic callers reference them
/// without pulling <c>Microsoft.CodeAnalysis</c>.
/// </summary>
internal static class RegistryDiagnosticIds
{
    /// <summary>
    /// Two catalogs declare the same error <c>code</c> string — cross-catalog
    /// collision. Build fails; no registry is emitted.
    /// </summary>
    public const string CrossCatalogDuplicateCode = "D2ERC004";

    /// <summary>
    /// Reserved-namespace violation: either an unprefixed code in a per-domain
    /// spec, or a prefixed code in the generic spec. Build fails; no registry
    /// is emitted.
    /// </summary>
    public const string ReservedNamespaceViolation = "D2ERC005";

    /// <summary>
    /// Malformed or invalid registry spec: the spec file could not be parsed,
    /// or a registry entry is missing one or more of the required factory
    /// fields (<c>category</c> / <c>userMessageKey</c> / <c>factoryName</c> /
    /// <c>factoryShape</c>). Build fails; no registry is emitted.
    /// </summary>
    public const string MalformedRegistrySpec = "D2ERC006";

    /// <summary>
    /// A code's <c>category</c> wire string is not a member of the closed
    /// <c>ErrorCategory</c> set declared in <c>error-category.spec.json</c>.
    /// Build fails; no registry is emitted.
    /// </summary>
    public const string UnknownCategory = "D2ERC007";
}
