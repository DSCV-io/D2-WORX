// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce <see cref="EmitDiagnostic"/>
/// instances with field-constraints-source-gen descriptor IDs (<c>D2FC*</c>).
/// The diagnostic record itself lives in <c>DcsvIo.D2.SourceGen</c> (shared
/// across every source generator); only the per-topic factory shape lives here.
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
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateConstName"/> diagnostic.
    /// </summary>
    /// <param name="name">The duplicated field-length constant name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateConstName(string name) =>
        new(DiagnosticIds.DuplicateConstName, [name]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidConstName"/> diagnostic.
    /// </summary>
    /// <param name="name">The malformed constant name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidConstName(string name) =>
        new(DiagnosticIds.InvalidConstName, [name]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.NonPositiveValue"/> diagnostic.
    /// </summary>
    /// <param name="name">The constant name carrying the bad value.</param>
    /// <param name="value">The non-positive value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic NonPositiveValue(string name, int value) =>
        new(DiagnosticIds.NonPositiveValue, [name, value]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateEnumName"/> diagnostic.
    /// </summary>
    /// <param name="name">The duplicated enum name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateEnumName(string name) =>
        new(DiagnosticIds.DuplicateEnumName, [name]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidEnumName"/> diagnostic.
    /// </summary>
    /// <param name="name">The malformed enum name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidEnumName(string name) =>
        new(DiagnosticIds.InvalidEnumName, [name]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.EmptyEnumMemberList"/> diagnostic.
    /// </summary>
    /// <param name="name">The enum declaring no members.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic EmptyEnumMemberList(string name) =>
        new(DiagnosticIds.EmptyEnumMemberList, [name]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateEnumMember"/> diagnostic.
    /// </summary>
    /// <param name="enumName">The enum carrying the duplicate.</param>
    /// <param name="memberName">The duplicated member name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateEnumMember(string enumName, string memberName) =>
        new(DiagnosticIds.DuplicateEnumMember, [enumName, memberName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidEnumMemberName"/> diagnostic.
    /// </summary>
    /// <param name="enumName">The enum carrying the malformed member.</param>
    /// <param name="memberName">The malformed member name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidEnumMemberName(string enumName, string memberName) =>
        new(DiagnosticIds.InvalidEnumMemberName, [enumName, memberName]);
}
