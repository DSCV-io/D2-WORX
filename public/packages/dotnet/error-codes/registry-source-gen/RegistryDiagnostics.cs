// -----------------------------------------------------------------------
// <copyright file="RegistryDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Registry.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Factory helpers producing <see cref="EmitDiagnostic"/> instances for the
/// cross-catalog registry diagnostics declared in
/// <see cref="RegistryDiagnosticIds"/>. Pure-logic callers use these
/// factories without pulling <c>Microsoft.CodeAnalysis</c>.
/// </summary>
internal static class RegistryDiagnostics
{
    /// <summary>
    /// Constructs a <see cref="RegistryDiagnosticDescriptors.CrossCatalogDuplicateCode"/>
    /// diagnostic.
    /// </summary>
    /// <param name="code">The duplicate error code.</param>
    /// <param name="firstCatalog">The first catalog (spec filename) that declared the code.</param>
    /// <param name="secondCatalog">The second catalog (spec filename) that also declared the code.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic CrossCatalogDuplicateCode(
        string code, string firstCatalog, string secondCatalog) =>
        new(RegistryDiagnosticIds.CrossCatalogDuplicateCode, [code, firstCatalog, secondCatalog]);

    /// <summary>
    /// Constructs a <see cref="RegistryDiagnosticDescriptors.ReservedNamespaceViolation"/>
    /// diagnostic.
    /// </summary>
    /// <param name="code">The offending error code.</param>
    /// <param name="catalog">The catalog (spec filename) that declared the code.</param>
    /// <param name="reason">Human-readable description of the violation.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic ReservedNamespaceViolation(
        string code, string catalog, string reason) =>
        new(RegistryDiagnosticIds.ReservedNamespaceViolation, [code, catalog, reason]);

    /// <summary>
    /// Constructs a <see cref="RegistryDiagnosticDescriptors.MalformedRegistrySpec"/>
    /// diagnostic.
    /// </summary>
    /// <param name="specFileName">The spec file name (without path).</param>
    /// <param name="reason">Human-readable description of why the spec is malformed.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MalformedRegistrySpec(string specFileName, string reason) =>
        new(RegistryDiagnosticIds.MalformedRegistrySpec, [specFileName, reason]);

    /// <summary>
    /// Constructs a <see cref="RegistryDiagnosticDescriptors.UnknownCategory"/>
    /// diagnostic.
    /// </summary>
    /// <param name="code">The offending error code.</param>
    /// <param name="catalog">The catalog (spec filename) that declared the code.</param>
    /// <param name="category">The unknown category wire string.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic UnknownCategory(
        string code, string catalog, string category) =>
        new(RegistryDiagnosticIds.UnknownCategory, [code, catalog, category]);
}
