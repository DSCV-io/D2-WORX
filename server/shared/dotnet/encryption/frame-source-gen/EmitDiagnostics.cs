// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.EncryptionFrame.SourceGen;

using D2.Shared.SourceGen;

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
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateFieldName"/> diagnostic.
    /// </summary>
    /// <param name="constName">The duplicated constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateFieldName(string constName) =>
        new(DiagnosticIds.DuplicateFieldName, [constName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.OverlappingFields"/> diagnostic.
    /// </summary>
    /// <param name="a">First field constName.</param>
    /// <param name="b">Second field constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic OverlappingFields(string a, string b) =>
        new(DiagnosticIds.OverlappingFields, [a, b]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidLength"/> diagnostic.
    /// </summary>
    /// <param name="constName">The offending field constName.</param>
    /// <param name="length">The invalid length.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidLength(string constName, int length) =>
        new(DiagnosticIds.InvalidLength, [constName, length]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidVersion"/> diagnostic.
    /// </summary>
    /// <param name="version">The invalid version value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidVersion(int version) =>
        new(DiagnosticIds.InvalidVersion, [version]);
}
