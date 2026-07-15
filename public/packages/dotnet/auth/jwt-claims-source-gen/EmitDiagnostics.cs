// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.JwtClaims.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce
/// <see cref="EmitDiagnostic"/> instances with jwt-claims-source-gen
/// descriptor IDs (<c>D2JCT*</c>). The diagnostic record itself lives in
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

    /// <summary>Constructs a <see cref="DiagnosticDescriptors.UnknownKind"/> diagnostic.</summary>
    /// <param name="constName">The claim constName.</param>
    /// <param name="value">The offending kind value.</param>
    /// <param name="validValues">A comma-separated list of accepted kinds.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic UnknownKind(
        string constName, string value, string validValues) =>
        new(DiagnosticIds.UnknownKind, [constName, value, validValues]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidConstName"/> diagnostic.
    /// </summary>
    /// <param name="constName">The offending constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidConstName(string constName) =>
        new(DiagnosticIds.InvalidConstName, [constName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateConstName"/> diagnostic.
    /// </summary>
    /// <param name="constName">The duplicated constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateConstName(string constName) =>
        new(DiagnosticIds.DuplicateConstName, [constName]);

    /// <summary>Constructs a <see cref="DiagnosticDescriptors.MissingSpec"/> diagnostic.</summary>
    /// <param name="assemblyName">The consuming assembly name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingSpec(string assemblyName) =>
        new(DiagnosticIds.MissingSpec, [assemblyName]);

    /// <summary>Constructs a <see cref="DiagnosticDescriptors.EmptyValue"/> diagnostic.</summary>
    /// <param name="constName">The claim constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic EmptyValue(string constName) =>
        new(DiagnosticIds.EmptyValue, [constName]);
}
