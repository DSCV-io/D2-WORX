// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.InProcessKeys.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce
/// <see cref="EmitDiagnostic"/> instances with in-process-keys-source-gen
/// descriptor IDs (<c>D2IPK*</c>). The diagnostic record itself lives in
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
    /// Constructs a <see cref="DiagnosticDescriptors.UnknownBinding"/> diagnostic.
    /// </summary>
    /// <param name="constName">The key constName.</param>
    /// <param name="value">The offending binding value.</param>
    /// <param name="validValues">A comma-separated list of accepted values.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic UnknownBinding(
        string constName, string value, string validValues) =>
        new(DiagnosticIds.UnknownBinding, [constName, value, validValues]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidConstName"/> diagnostic.
    /// </summary>
    /// <param name="constName">The offending constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidConstName(string constName) =>
        new(DiagnosticIds.InvalidConstName, [constName]);

    /// <summary>Constructs a <see cref="DiagnosticDescriptors.MissingSpec"/> diagnostic.</summary>
    /// <param name="assemblyName">The consuming assembly name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingSpec(string assemblyName) =>
        new(DiagnosticIds.MissingSpec, [assemblyName]);
}
