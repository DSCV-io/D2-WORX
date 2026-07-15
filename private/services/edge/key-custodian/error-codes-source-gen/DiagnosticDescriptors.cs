// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.ErrorCodes.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared in
/// <see cref="DiagnosticIds"/>. Only loaded inside the Roslyn host (the
/// generator's <see cref="ErrorCodesGenerator.Initialize"/> call site);
/// pure-logic callers should use <see cref="DiagnosticIds"/> string constants
/// directly to avoid pulling <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "KeyCustodian error codes spec is malformed",
        messageFormat: "KeyCustodian error codes spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.UnknownCategoryEnum"/>
    public static readonly DiagnosticDescriptor UnknownCategoryEnum = new(
        id: DiagnosticIds.UnknownCategoryEnum,
        title: "Error code category is not one of the supported values",
        messageFormat:
            "Error code '{0}' has unknown category '{1}' (valid: {2})",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateCode"/>
    public static readonly DiagnosticDescriptor DuplicateCode = new(
        id: DiagnosticIds.DuplicateCode,
        title: "Duplicate KeyCustodian error code",
        messageFormat: "KeyCustodian error code '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateFactoryName"/>
    public static readonly DiagnosticDescriptor DuplicateFactoryName = new(
        id: DiagnosticIds.DuplicateFactoryName,
        title: "Duplicate KeyCustodian error code factory name",
        messageFormat:
            "KeyCustodian error code factory name '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidHttpStatus"/>
    public static readonly DiagnosticDescriptor InvalidHttpStatus = new(
        id: DiagnosticIds.InvalidHttpStatus,
        title: "KeyCustodian error code httpStatus is not supported by the codegen mapping",
        messageFormat:
            "Error code '{0}' has unsupported httpStatus '{1}' (supported: {2}). "
            + "Add the new status to the codegen mapping matrix when expanding.",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "D2.Edge.KeyCustodian.ErrorCodes.SourceGen";
}
