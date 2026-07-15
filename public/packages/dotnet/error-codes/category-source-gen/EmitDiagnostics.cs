// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Category.SourceGen;

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
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateWire"/> diagnostic.
    /// </summary>
    /// <param name="wire">The duplicated wire string.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateWire(string wire) =>
        new(DiagnosticIds.DuplicateWire, [wire]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidWire"/> diagnostic.
    /// </summary>
    /// <param name="wire">The offending wire string.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidWire(string wire) =>
        new(DiagnosticIds.InvalidWire, [wire]);

    /// <summary>Constructs a <see cref="DiagnosticDescriptors.EmptyDoc"/> diagnostic.</summary>
    /// <param name="wire">The wire string whose doc is empty.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic EmptyDoc(string wire) =>
        new(DiagnosticIds.EmptyDoc, [wire]);
}
