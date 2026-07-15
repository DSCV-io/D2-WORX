// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ResultErrorCodes.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared
/// in <see cref="DiagnosticIds"/>. Only loaded inside the Roslyn host (the
/// generator's <see cref="ErrorCodesGenerator.Initialize"/> call site);
/// pure-logic callers should use <see cref="DiagnosticIds"/> string constants
/// directly to avoid pulling <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Error codes spec is malformed",
        messageFormat: "Error codes spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateCode"/>
    public static readonly DiagnosticDescriptor DuplicateCode = new(
        id: DiagnosticIds.DuplicateCode,
        title: "Duplicate error code",
        messageFormat: "Error code '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidHttpStatus"/>
    public static readonly DiagnosticDescriptor InvalidHttpStatus = new(
        id: DiagnosticIds.InvalidHttpStatus,
        title: "Error code httpStatus is not supported by the codegen mapping",
        messageFormat:
            "Error code '{0}' has unsupported httpStatus '{1}' (supported: {2}). "
            + "Add the new status to the codegen mapping matrix when expanding.",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidCode"/>
    public static readonly DiagnosticDescriptor InvalidCode = new(
        id: DiagnosticIds.InvalidCode,
        title: "Error code is empty or does not match SCREAMING_SNAKE",
        messageFormat:
            "Error code '{0}' is empty or violates the SCREAMING_SNAKE convention "
            + "(expected pattern: ^[A-Z][A-Z0-9_]*$)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingDoc"/>
    public static readonly DiagnosticDescriptor MissingDoc = new(
        id: DiagnosticIds.MissingDoc,
        title: "Error code doc is missing",
        messageFormat: "Error code '{0}' is missing the required 'doc' summary text",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.Result.ErrorCodes.SourceGen";
}
