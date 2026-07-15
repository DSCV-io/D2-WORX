// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Headers.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared in
/// <see cref="DiagnosticIds"/>. Only loaded inside the Roslyn host (the
/// generator's <see cref="HeadersGenerator.Initialize"/> call site);
/// pure-logic callers should use <see cref="DiagnosticIds"/> string constants
/// directly to avoid pulling <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Headers spec is malformed",
        messageFormat: "Headers spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.UnknownTransport"/>
    public static readonly DiagnosticDescriptor UnknownTransport = new(
        id: DiagnosticIds.UnknownTransport,
        title: "Header applicability contains an unknown transport",
        messageFormat:
            "Header '{0}' has unknown transport '{1}' in applicability (valid: {2})",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidConstName"/>
    public static readonly DiagnosticDescriptor InvalidConstName = new(
        id: DiagnosticIds.InvalidConstName,
        title: "Header constName violates UPPER_SNAKE_CASE pattern",
        messageFormat:
            "Header '{0}' has invalid constName '{1}' — must match ^[A-Z][A-Z0-9_]*$",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateConstName"/>
    public static readonly DiagnosticDescriptor DuplicateConstName = new(
        id: DiagnosticIds.DuplicateConstName,
        title: "Duplicate header constName within a catalog",
        messageFormat:
            "Header constName '{0}' is duplicated in catalog '{1}'",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.EmptyApplicability"/>
    public static readonly DiagnosticDescriptor EmptyApplicability = new(
        id: DiagnosticIds.EmptyApplicability,
        title: "Header applicability is empty",
        messageFormat:
            "Header '{0}' has empty applicability — every header must belong " +
            "to at least one transport",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.UnknownConvention"/>
    public static readonly DiagnosticDescriptor UnknownConvention = new(
        id: DiagnosticIds.UnknownConvention,
        title: "Header convention is outside the recognized set",
        messageFormat:
            "Header '{0}' has unrecognized convention '{1}' (recognized: {2})",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingSpec"/>
    public static readonly DiagnosticDescriptor MissingSpec = new(
        id: DiagnosticIds.MissingSpec,
        title: "Headers spec file missing from AdditionalFiles",
        messageFormat:
            "Headers spec file is missing from <AdditionalFiles> for assembly '{0}'",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.Headers.SourceGen";
}
