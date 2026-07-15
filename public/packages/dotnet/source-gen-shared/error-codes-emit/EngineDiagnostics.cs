// -----------------------------------------------------------------------
// <copyright file="EngineDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Factory helpers producing <see cref="EmitDiagnostic"/> instances for the
/// catalog-neutral engine diagnostics (<c>D2ERC*</c>). The diagnostic record
/// itself lives in <c>DcsvIo.D2.SourceGen</c> (shared across every source
/// generator); only the engine-specific factory shape lives here.
/// </summary>
internal static class EngineDiagnostics
{
    /// <summary>
    /// Constructs a <see cref="EngineDiagnosticDescriptors.DomainPrefixViolation"/>
    /// diagnostic.
    /// </summary>
    /// <param name="code">The offending error code.</param>
    /// <param name="catalog">The catalog (consuming assembly) name.</param>
    /// <param name="domainPrefix">The enforced domain prefix.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DomainPrefixViolation(
        string code, string catalog, string domainPrefix) =>
        new(EngineDiagnosticIds.DomainPrefixViolation, [code, catalog, domainPrefix]);

    /// <summary>
    /// Constructs a <see cref="EngineDiagnosticDescriptors.TkKeyNotFound"/>
    /// diagnostic.
    /// </summary>
    /// <param name="code">The error code carrying the unresolved key.</param>
    /// <param name="userMessageKey">The TK symbol-path reference.</param>
    /// <param name="expectedSnakeKey">The inverse-transformed snake_case key.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic TkKeyNotFound(
        string code, string userMessageKey, string expectedSnakeKey) =>
        new(EngineDiagnosticIds.TkKeyNotFound, [code, userMessageKey, expectedSnakeKey]);

    /// <summary>
    /// Constructs a <see cref="EngineDiagnosticDescriptors.UnsupportedFactoryShape"/>
    /// diagnostic.
    /// </summary>
    /// <param name="shape">The unimplemented <c>factoryShape</c> value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic UnsupportedFactoryShape(string shape) =>
        new(EngineDiagnosticIds.UnsupportedFactoryShape, [shape]);
}
