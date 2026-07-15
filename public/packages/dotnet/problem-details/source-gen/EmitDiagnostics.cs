// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ProblemDetails.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce
/// <see cref="EmitDiagnostic"/> instances with problem-details-source-gen
/// descriptor IDs (<c>D2PRB*</c>). The diagnostic record itself lives in
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
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateExtensionKeyConstName"/>
    /// diagnostic.
    /// </summary>
    /// <param name="constName">The duplicated extension key constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateExtensionKeyConstName(string constName) =>
        new(DiagnosticIds.DuplicateExtensionKeyConstName, [constName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateExtensionKeyValue"/>
    /// diagnostic.
    /// </summary>
    /// <param name="value">The duplicated extension key wire value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateExtensionKeyValue(string value) =>
        new(DiagnosticIds.DuplicateExtensionKeyValue, [value]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateTitleConstName"/>
    /// diagnostic.
    /// </summary>
    /// <param name="constName">The duplicated title constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateTitleConstName(string constName) =>
        new(DiagnosticIds.DuplicateTitleConstName, [constName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateTitleHttpStatus"/>
    /// diagnostic.
    /// </summary>
    /// <param name="httpStatus">
    /// The duplicated httpStatus (or the string "null" for the fallback).
    /// </param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateTitleHttpStatus(string httpStatus) =>
        new(DiagnosticIds.DuplicateTitleHttpStatus, [httpStatus]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.TypeUriPrefixMissingTrailingSlash"/>
    /// diagnostic.
    /// </summary>
    /// <param name="typeUriPrefix">The offending prefix value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic TypeUriPrefixMissingTrailingSlash(string typeUriPrefix) =>
        new(DiagnosticIds.TypeUriPrefixMissingTrailingSlash, [typeUriPrefix]);
}
