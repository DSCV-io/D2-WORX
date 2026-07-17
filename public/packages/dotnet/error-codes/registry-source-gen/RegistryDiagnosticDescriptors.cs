// -----------------------------------------------------------------------
// <copyright file="RegistryDiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Registry.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the cross-catalog
/// registry diagnostics declared in <see cref="RegistryDiagnosticIds"/>.
/// Only loaded inside the Roslyn host (the generator's <c>Initialize</c>
/// call site); pure-logic callers use <see cref="RegistryDiagnosticIds"/>
/// string constants directly to avoid pulling <c>Microsoft.CodeAnalysis</c>.
/// </summary>
internal static class RegistryDiagnosticDescriptors
{
    /// <inheritdoc cref="RegistryDiagnosticIds.CrossCatalogDuplicateCode"/>
    public static readonly DiagnosticDescriptor CrossCatalogDuplicateCode = new(
        id: RegistryDiagnosticIds.CrossCatalogDuplicateCode,
        title: "Cross-catalog duplicate error code",
        messageFormat:
            "Error code '{0}' is declared in both catalog '{1}' and catalog '{2}'. "
            + "Each error code must be unique across all catalogs. "
            + "Rename the code in one catalog before the registry can be emitted.",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="RegistryDiagnosticIds.ReservedNamespaceViolation"/>
    public static readonly DiagnosticDescriptor ReservedNamespaceViolation = new(
        id: RegistryDiagnosticIds.ReservedNamespaceViolation,
        title: "Error code reserved-namespace violation",
        messageFormat:
            "Error code '{0}' in catalog '{1}' violates the reserved-namespace rule: {2}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="RegistryDiagnosticIds.MalformedRegistrySpec"/>
    public static readonly DiagnosticDescriptor MalformedRegistrySpec = new(
        id: RegistryDiagnosticIds.MalformedRegistrySpec,
        title: "Malformed registry spec",
        messageFormat:
            "Registry spec '{0}' is malformed or invalid: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="RegistryDiagnosticIds.UnknownCategory"/>
    public static readonly DiagnosticDescriptor UnknownCategory = new(
        id: RegistryDiagnosticIds.UnknownCategory,
        title: "Unknown error category",
        messageFormat:
            "Error code '{0}' in catalog '{1}' declares category '{2}', which is not "
            + "a member of the closed ErrorCategory set in error-category.spec.json. "
            + "Add the category to the spec or correct the code's category.",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.ErrorCodes.Registry.SourceGen";
}
