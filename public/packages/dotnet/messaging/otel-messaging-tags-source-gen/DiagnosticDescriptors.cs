// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.OtelMessagingTags.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared
/// in <see cref="DiagnosticIds"/>.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "OTel messaging tags spec is malformed",
        messageFormat: "OTel messaging tags spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateConstName"/>
    public static readonly DiagnosticDescriptor DuplicateConstName = new(
        id: DiagnosticIds.DuplicateConstName,
        title: "Duplicate OTel messaging tag constName",
        messageFormat:
            "OTel messaging tag constName '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateValue"/>
    public static readonly DiagnosticDescriptor DuplicateValue = new(
        id: DiagnosticIds.DuplicateValue,
        title: "Duplicate OTel messaging tag wire value",
        messageFormat:
            "OTel messaging tag wire value '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidConstName"/>
    public static readonly DiagnosticDescriptor InvalidConstName = new(
        id: DiagnosticIds.InvalidConstName,
        title: "OTel messaging tag constName has invalid shape",
        messageFormat:
            "OTel messaging tag constName '{0}' must match UPPER_SNAKE_CASE pattern "
                + "^[A-Z][A-Z0-9_]*$",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.EmptyValue"/>
    public static readonly DiagnosticDescriptor EmptyValue = new(
        id: DiagnosticIds.EmptyValue,
        title: "OTel messaging tag wire value is empty",
        messageFormat:
            "OTel messaging tag '{0}' has empty or whitespace-only wire value",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.OtelMessagingTags.SourceGen";
}
