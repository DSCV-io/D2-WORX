// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.DlqMetadata.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>Roslyn descriptors for the IDs in <see cref="DiagnosticIds"/>.</summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "DLQ failure-metadata spec is malformed",
        messageFormat: "DLQ failure-metadata spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateFieldConstName"/>
    public static readonly DiagnosticDescriptor DuplicateFieldConstName = new(
        id: DiagnosticIds.DuplicateFieldConstName,
        title: "Duplicate DLQ failure-metadata field constName",
        messageFormat:
            "DLQ field constName '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateFieldValue"/>
    public static readonly DiagnosticDescriptor DuplicateFieldValue = new(
        id: DiagnosticIds.DuplicateFieldValue,
        title: "Duplicate DLQ failure-metadata field wire value",
        messageFormat:
            "DLQ field wire value '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateCause"/>
    public static readonly DiagnosticDescriptor DuplicateCause = new(
        id: DiagnosticIds.DuplicateCause,
        title: "Duplicate DLQ failure-metadata cause",
        messageFormat:
            "DLQ cause '{0}' is declared more than once in the spec (constName or value)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidConstName"/>
    public static readonly DiagnosticDescriptor InvalidConstName = new(
        id: DiagnosticIds.InvalidConstName,
        title: "DLQ failure-metadata constName has invalid shape",
        messageFormat:
            "DLQ constName '{0}' must match UPPER_SNAKE_CASE pattern ^[A-Z][A-Z0-9_]*$",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.EmptyValue"/>
    public static readonly DiagnosticDescriptor EmptyValue = new(
        id: DiagnosticIds.EmptyValue,
        title: "DLQ failure-metadata wire value is empty",
        messageFormat:
            "DLQ entry '{0}' has empty or whitespace-only wire value",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.Messaging.DlqMetadata.SourceGen";
}
