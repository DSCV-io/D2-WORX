// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.WireShapes.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs
/// declared in <see cref="DiagnosticIds"/>. Only loaded inside the Roslyn
/// host (the generator's <see cref="WireShapesGenerator.Initialize"/>
/// call site); pure-logic callers should use <see cref="DiagnosticIds"/>
/// string constants directly to avoid pulling
/// <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Wire-shape spec is malformed",
        messageFormat: "Wire-shape spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicatePropertyConstName"/>
    public static readonly DiagnosticDescriptor DuplicatePropertyConstName = new(
        id: DiagnosticIds.DuplicatePropertyConstName,
        title: "Duplicate wire-shape property constName",
        messageFormat:
            "Wire-shape property constName '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicatePropertyValue"/>
    public static readonly DiagnosticDescriptor DuplicatePropertyValue = new(
        id: DiagnosticIds.DuplicatePropertyValue,
        title: "Duplicate wire-shape property wire value",
        messageFormat:
            "Wire-shape property wire value '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidConstName"/>
    public static readonly DiagnosticDescriptor InvalidConstName = new(
        id: DiagnosticIds.InvalidConstName,
        title: "Invalid wire-shape property constName",
        messageFormat:
            "Wire-shape property constName '{0}' is invalid — must match "
            + "UPPER_SNAKE_CASE pattern ^[A-Z][A-Z0-9_]*$",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingSpec"/>
    public static readonly DiagnosticDescriptor MissingSpec = new(
        id: DiagnosticIds.MissingSpec,
        title: "Wire-shape spec file missing for target catalog assembly",
        messageFormat:
            "No wire-shape spec file was provided via <AdditionalFiles> for target "
            + "catalog assembly '{0}'. Add <AdditionalFiles Include=\"...\" /> to "
            + "the consuming csproj pointing at the spec under contracts/.",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.WireShapes.SourceGen";
}
