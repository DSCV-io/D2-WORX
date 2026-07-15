// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionFrame.SourceGen;

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

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.SealedMalformedSpec"/> diagnostic.
    /// </summary>
    /// <param name="path">The spec file path.</param>
    /// <param name="reason">The parse-failure reason.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic SealedMalformedSpec(string path, string reason) =>
        new(DiagnosticIds.SealedMalformedSpec, [path, reason]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.SealedDuplicateFieldName"/> diagnostic.
    /// </summary>
    /// <param name="constName">The duplicated constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic SealedDuplicateFieldName(string constName) =>
        new(DiagnosticIds.SealedDuplicateFieldName, [constName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.SealedOverlappingFields"/> diagnostic.
    /// </summary>
    /// <param name="a">First field constName.</param>
    /// <param name="b">Second field constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic SealedOverlappingFields(string a, string b) =>
        new(DiagnosticIds.SealedOverlappingFields, [a, b]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.SealedInvalidLength"/> diagnostic.
    /// </summary>
    /// <param name="constName">The offending field constName.</param>
    /// <param name="length">The invalid length.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic SealedInvalidLength(string constName, int length) =>
        new(DiagnosticIds.SealedInvalidLength, [constName, length]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.SealedInvalidVersion"/> diagnostic.
    /// </summary>
    /// <param name="version">The invalid version value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic SealedInvalidVersion(int version) =>
        new(DiagnosticIds.SealedInvalidVersion, [version]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.SealedUnknownFieldKind"/> diagnostic.
    /// </summary>
    /// <param name="constName">The offending field constName.</param>
    /// <param name="kind">The unknown kind value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic SealedUnknownFieldKind(string constName, string kind) =>
        new(DiagnosticIds.SealedUnknownFieldKind, [constName, kind]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.SealedBinaryLengthPrefixMissing"/>
    /// diagnostic.
    /// </summary>
    /// <param name="constName">The offending field constName.</param>
    /// <param name="prefixSize">The required length-prefix width in bytes.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic SealedBinaryLengthPrefixMissing(
        string constName, int prefixSize) =>
        new(DiagnosticIds.SealedBinaryLengthPrefixMissing, [constName, prefixSize]);
}
