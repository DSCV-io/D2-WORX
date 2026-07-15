// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce
/// <see cref="EmitDiagnostic"/> instances with context-source-gen
/// descriptor IDs (<c>D2CTX*</c>). The diagnostic record itself lives in
/// <c>DcsvIo.D2.SourceGen</c> (shared across every source generator); only
/// the per-topic factory shape lives here.
/// </summary>
internal static class EmitDiagnostics
{
    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MalformedSpec"/> diagnostic.
    /// </summary>
    /// <param name="path">The spec file path.</param>
    /// <param name="reason">The parse-failure reason.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MalformedSpec(string path, string reason) =>
        new(DiagnosticIds.MalformedSpec, [path, reason]);

    /// <summary>Constructs a <see cref="DiagnosticDescriptors.UnknownType"/> diagnostic.</summary>
    /// <param name="specName">The interface name from the spec.</param>
    /// <param name="propertyName">The property whose type is unknown.</param>
    /// <param name="type">The offending type string.</param>
    /// <param name="validTypes">A comma-separated list of valid types.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic UnknownType(
        string specName, string propertyName, string type, string validTypes) =>
        new(DiagnosticIds.UnknownType, [specName, propertyName, type, validTypes]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.PropertyNameCollision"/> diagnostic.
    /// </summary>
    /// <param name="propertyName">The collision-causing property name.</param>
    /// <param name="firstSpec">First spec the property appears in.</param>
    /// <param name="secondSpec">Second spec the property appears in.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic PropertyNameCollision(
        string propertyName, string firstSpec, string secondSpec) =>
        new(DiagnosticIds.PropertyNameCollision, [propertyName, firstSpec, secondSpec]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.UnresolvableExtends"/> diagnostic.
    /// </summary>
    /// <param name="specName">The spec whose extends is unresolvable.</param>
    /// <param name="extends">The unresolvable extends value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic UnresolvableExtends(string specName, string extends) =>
        new(DiagnosticIds.UnresolvableExtends, [specName, extends]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.UnknownDerivedRule"/> warning.
    /// </summary>
    /// <param name="specName">The spec name.</param>
    /// <param name="propertyName">The property declaring the derived rule.</param>
    /// <param name="rule">The unknown rule name.</param>
    /// <param name="validRules">A comma-separated list of valid rules.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic UnknownDerivedRule(
        string specName, string propertyName, string rule, string validRules) =>
        new(DiagnosticIds.UnknownDerivedRule, [specName, propertyName, rule, validRules]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MissingSpecFile"/> diagnostic.
    /// </summary>
    /// <param name="targetAssembly">The target assembly name expecting spec files.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingSpecFile(string targetAssembly) =>
        new(DiagnosticIds.MissingSpecFile, [targetAssembly]);
}
