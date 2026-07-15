// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Category.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>Roslyn descriptors for the IDs in <see cref="DiagnosticIds"/>.</summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Error category spec is malformed",
        messageFormat: "Error category spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateWire"/>
    public static readonly DiagnosticDescriptor DuplicateWire = new(
        id: DiagnosticIds.DuplicateWire,
        title: "Duplicate error-category wire string",
        messageFormat:
            "Error-category wire string '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidWire"/>
    public static readonly DiagnosticDescriptor InvalidWire = new(
        id: DiagnosticIds.InvalidWire,
        title: "Error-category wire string has invalid shape",
        messageFormat:
            "Error-category wire string '{0}' must match snake_case pattern "
                + "^[a-z][a-z0-9_]*$",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.EmptyDoc"/>
    public static readonly DiagnosticDescriptor EmptyDoc = new(
        id: DiagnosticIds.EmptyDoc,
        title: "Error-category doc is empty",
        messageFormat:
            "Error-category '{0}' has empty or whitespace-only doc text",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.ErrorCodes.Category.SourceGen";
}
