// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.ProtocolAudiences.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared in
/// <see cref="DiagnosticIds"/>. Only loaded inside the Roslyn host (the
/// generator's <see cref="ProtocolAudiencesGenerator.Initialize"/> call site);
/// pure-logic callers should use <see cref="DiagnosticIds"/> string constants
/// directly to avoid pulling <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.DuplicateName"/>
    public static readonly DiagnosticDescriptor DuplicateName = new(
        id: DiagnosticIds.DuplicateName,
        title: "Duplicate protocol-audience name",
        messageFormat:
            "Protocol-audience name '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidName"/>
    public static readonly DiagnosticDescriptor InvalidName = new(
        id: DiagnosticIds.InvalidName,
        title: "Protocol-audience name violates identifier convention",
        messageFormat: "Protocol-audience name '{0}' is invalid: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateValue"/>
    public static readonly DiagnosticDescriptor DuplicateValue = new(
        id: DiagnosticIds.DuplicateValue,
        title: "Duplicate protocol-audience value",
        messageFormat:
            "Protocol-audiences '{0}' and '{1}' both map to value '{2}' — rename or "
            + "remove one to avoid silent aud-claim aliasing",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.EmptyValue"/>
    public static readonly DiagnosticDescriptor EmptyValue = new(
        id: DiagnosticIds.EmptyValue,
        title: "Protocol-audience value is empty",
        messageFormat:
            "Protocol-audience '{0}' has an empty value — a protocol audience must be "
            + "a non-empty bare token (for example d2.internal)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Protocol-audience spec is malformed",
        messageFormat:
            "Protocol-audience spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingSpecFile"/>
    public static readonly DiagnosticDescriptor MissingSpecFile = new(
        id: DiagnosticIds.MissingSpecFile,
        title: "Protocol-audience spec file not found",
        messageFormat:
            "The ProtocolAudiences source generator could not locate "
            + "'protocol-audiences.spec.json' among AdditionalFiles; verify the "
            + "consuming csproj declares the "
            + "contracts/auth-protocol-audiences/protocol-audiences.spec.json glob",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.Auth.ProtocolAudiences.SourceGen";
}
