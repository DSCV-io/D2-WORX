// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Headers.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce
/// <see cref="EmitDiagnostic"/> instances with headers-source-gen
/// descriptor IDs (<c>D2HDR*</c>). The diagnostic record itself lives in
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

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.UnknownTransport"/> diagnostic.
    /// </summary>
    /// <param name="constName">The header constName.</param>
    /// <param name="value">The offending transport value.</param>
    /// <param name="validValues">A comma-separated list of accepted values.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic UnknownTransport(
        string constName, string value, string validValues) =>
        new(DiagnosticIds.UnknownTransport, [constName, value, validValues]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidConstName"/> diagnostic.
    /// </summary>
    /// <param name="name">The header wire name.</param>
    /// <param name="constName">The offending constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidConstName(string name, string constName) =>
        new(DiagnosticIds.InvalidConstName, [name, constName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateConstName"/> diagnostic.
    /// </summary>
    /// <param name="constName">The duplicated constName.</param>
    /// <param name="catalog">The catalog the duplicate appears in.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateConstName(string constName, string catalog) =>
        new(DiagnosticIds.DuplicateConstName, [constName, catalog]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.EmptyApplicability"/> diagnostic.
    /// </summary>
    /// <param name="constName">The header constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic EmptyApplicability(string constName) =>
        new(DiagnosticIds.EmptyApplicability, [constName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.UnknownConvention"/> diagnostic (warning).
    /// </summary>
    /// <param name="constName">The header constName.</param>
    /// <param name="value">The unrecognized convention value.</param>
    /// <param name="recognized">A comma-separated list of recognized values.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic UnknownConvention(
        string constName, string value, string recognized) =>
        new(DiagnosticIds.UnknownConvention, [constName, value, recognized]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MissingSpec"/> diagnostic.
    /// </summary>
    /// <param name="assemblyName">The consuming assembly name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingSpec(string assemblyName) =>
        new(DiagnosticIds.MissingSpec, [assemblyName]);
}
