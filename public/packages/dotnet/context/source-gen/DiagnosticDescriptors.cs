// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared in
/// <see cref="DiagnosticIds"/>. Only loaded inside the Roslyn host (the
/// generator's <see cref="ContextGenerator.Initialize"/> call site); pure-logic
/// callers should use <see cref="DiagnosticIds"/> string constants directly to
/// avoid pulling <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Context spec is malformed",
        messageFormat: "Context spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.UnknownType"/>
    public static readonly DiagnosticDescriptor UnknownType = new(
        id: DiagnosticIds.UnknownType,
        title: "Context spec property uses unknown type",
        messageFormat: "Context spec '{0}' property '{1}' uses unknown type '{2}' (valid: {3})",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.PropertyNameCollision"/>
    public static readonly DiagnosticDescriptor PropertyNameCollision = new(
        id: DiagnosticIds.PropertyNameCollision,
        title: "Context property name collision",
        messageFormat:
            "Property name '{0}' is declared more than once across the spec hierarchy "
            + "('{1}' and '{2}')",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.UnresolvableExtends"/>
    public static readonly DiagnosticDescriptor UnresolvableExtends = new(
        id: DiagnosticIds.UnresolvableExtends,
        title: "Context spec extends references unresolvable interface",
        messageFormat:
            "Context spec '{0}' extends '{1}' which was not found among the parsed specs "
            + "in this generator run",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.UnknownDerivedRule"/>
    public static readonly DiagnosticDescriptor UnknownDerivedRule = new(
        id: DiagnosticIds.UnknownDerivedRule,
        title: "Context spec property uses unknown derived rule",
        messageFormat:
            "Context spec '{0}' property '{1}' declares derived rule '{2}' but the emitter "
            + "does not implement it (valid: {3})",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingSpecFile"/>
    public static readonly DiagnosticDescriptor MissingSpecFile = new(
        id: DiagnosticIds.MissingSpecFile,
        title: "Context spec file not found",
        messageFormat:
            "The Context source generator could not locate any '*.spec.json' among "
            + "AdditionalFiles for target assembly '{0}'; verify the consuming csproj "
            + "declares the contracts/{{auth,request}}-context/*.spec.json glob",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.Context.SourceGen";
}
