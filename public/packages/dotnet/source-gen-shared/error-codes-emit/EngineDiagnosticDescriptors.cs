// -----------------------------------------------------------------------
// <copyright file="EngineDiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the catalog-neutral
/// engine diagnostics declared in <see cref="EngineDiagnosticIds"/>. Only
/// loaded inside the Roslyn host (the generator shell's <c>Initialize</c> call
/// site); pure-logic callers use <see cref="EngineDiagnosticIds"/> string
/// constants directly to avoid pulling <c>Microsoft.CodeAnalysis</c>.
/// </summary>
internal static class EngineDiagnosticDescriptors
{
    /// <inheritdoc cref="EngineDiagnosticIds.DomainPrefixViolation"/>
    public static readonly DiagnosticDescriptor DomainPrefixViolation = new(
        id: EngineDiagnosticIds.DomainPrefixViolation,
        title: "Error code does not start with the enforced domain prefix",
        messageFormat:
            "Error code '{0}' in catalog '{1}' must start with the enforced "
            + "domain prefix '{2}'",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="EngineDiagnosticIds.TkKeyNotFound"/>
    public static readonly DiagnosticDescriptor TkKeyNotFound = new(
        id: EngineDiagnosticIds.TkKeyNotFound,
        title: "Error code userMessageKey does not resolve to a real translation key",
        messageFormat:
            "Error code '{0}' references userMessageKey '{1}' which does not "
            + "resolve to a key in en-US.json (expected snake_case key '{2}')",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="EngineDiagnosticIds.UnsupportedFactoryShape"/>
    public static readonly DiagnosticDescriptor UnsupportedFactoryShape = new(
        id: EngineDiagnosticIds.UnsupportedFactoryShape,
        title: "factoryShape value is not emitted on the delegating per-domain path",
        messageFormat:
            "factoryShape '{0}' is not emitted on the delegating per-domain path; only "
            + "'standard' and 'none' are supported there",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.ErrorCodes.SourceGen";
}
