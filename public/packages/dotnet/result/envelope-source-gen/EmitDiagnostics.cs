// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result.Envelope.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>Factory helpers producing per-topic <see cref="EmitDiagnostic"/>.</summary>
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
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateFieldConstName"/> diagnostic.
    /// </summary>
    /// <param name="constName">The duplicated field constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateFieldConstName(string constName) =>
        new(DiagnosticIds.DuplicateFieldConstName, [constName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateFieldValue"/> diagnostic.
    /// </summary>
    /// <param name="value">The duplicated field value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateFieldValue(string value) =>
        new(DiagnosticIds.DuplicateFieldValue, [value]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidConstName"/> diagnostic.
    /// </summary>
    /// <param name="constName">The offending constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidConstName(string constName) =>
        new(DiagnosticIds.InvalidConstName, [constName]);

    /// <summary>Constructs a <see cref="DiagnosticDescriptors.EmptyValue"/> diagnostic.</summary>
    /// <param name="constName">The constName whose value is empty.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic EmptyValue(string constName) =>
        new(DiagnosticIds.EmptyValue, [constName]);
}
