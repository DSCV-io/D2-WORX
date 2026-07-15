// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ProblemDetails.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared
/// in <see cref="DiagnosticIds"/>. Only loaded inside the Roslyn host (the
/// generator's <see cref="ProblemDetailsGenerator.Initialize"/> call site);
/// pure-logic callers should use <see cref="DiagnosticIds"/> string
/// constants directly to avoid pulling <c>Microsoft.CodeAnalysis</c> at
/// runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "ProblemDetails spec is malformed",
        messageFormat: "ProblemDetails spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateExtensionKeyConstName"/>
    public static readonly DiagnosticDescriptor DuplicateExtensionKeyConstName = new(
        id: DiagnosticIds.DuplicateExtensionKeyConstName,
        title: "Duplicate ProblemDetails extension key constName",
        messageFormat:
            "ProblemDetails extension key constName '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateExtensionKeyValue"/>
    public static readonly DiagnosticDescriptor DuplicateExtensionKeyValue = new(
        id: DiagnosticIds.DuplicateExtensionKeyValue,
        title: "Duplicate ProblemDetails extension key wire value",
        messageFormat:
            "ProblemDetails extension key wire value '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateTitleConstName"/>
    public static readonly DiagnosticDescriptor DuplicateTitleConstName = new(
        id: DiagnosticIds.DuplicateTitleConstName,
        title: "Duplicate ProblemDetails title constName",
        messageFormat:
            "ProblemDetails title constName '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateTitleHttpStatus"/>
    public static readonly DiagnosticDescriptor DuplicateTitleHttpStatus = new(
        id: DiagnosticIds.DuplicateTitleHttpStatus,
        title: "Duplicate ProblemDetails title httpStatus",
        messageFormat:
            "ProblemDetails title httpStatus '{0}' is declared more than once in the spec "
            + "(only one entry may map to each HTTP status; null is the singular fallback)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.TypeUriPrefixMissingTrailingSlash"/>
    public static readonly DiagnosticDescriptor TypeUriPrefixMissingTrailingSlash = new(
        id: DiagnosticIds.TypeUriPrefixMissingTrailingSlash,
        title: "ProblemDetails typeUriPrefix must end with a trailing slash",
        messageFormat:
            "ProblemDetails typeUriPrefix '{0}' must end with a trailing slash; "
            + "the runtime appends the kebab-cased error code directly",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.ProblemDetails.SourceGen";
}
