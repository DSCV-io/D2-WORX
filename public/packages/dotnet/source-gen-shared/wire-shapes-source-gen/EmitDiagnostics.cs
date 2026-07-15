// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.WireShapes.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce
/// <see cref="EmitDiagnostic"/> instances with wire-shapes-source-gen
/// descriptor IDs (<c>D2WS*</c>). The diagnostic record itself lives in
/// <c>DcsvIo.D2.SourceGen</c> (shared across every source generator);
/// only the per-topic factory shape lives here.
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
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicatePropertyConstName"/>
    /// diagnostic.
    /// </summary>
    /// <param name="constName">The duplicated property constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicatePropertyConstName(string constName) =>
        new(DiagnosticIds.DuplicatePropertyConstName, [constName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicatePropertyValue"/>
    /// diagnostic.
    /// </summary>
    /// <param name="value">The duplicated property wire value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicatePropertyValue(string value) =>
        new(DiagnosticIds.DuplicatePropertyValue, [value]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidConstName"/>
    /// diagnostic.
    /// </summary>
    /// <param name="constName">The offending constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidConstName(string constName) =>
        new(DiagnosticIds.InvalidConstName, [constName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MissingSpec"/> diagnostic.
    /// </summary>
    /// <param name="targetAssembly">The target catalog assembly name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingSpec(string targetAssembly) =>
        new(DiagnosticIds.MissingSpec, [targetAssembly]);
}
