// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.JwtClaims.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared in
/// <see cref="DiagnosticIds"/>.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "JWT claims spec is malformed",
        messageFormat: "JWT claims spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.UnknownKind"/>
    public static readonly DiagnosticDescriptor UnknownKind = new(
        id: DiagnosticIds.UnknownKind,
        title: "Claim kind is not in the closed enum",
        messageFormat: "Claim '{0}' has unknown kind '{1}' (valid: {2})",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidConstName"/>
    public static readonly DiagnosticDescriptor InvalidConstName = new(
        id: DiagnosticIds.InvalidConstName,
        title: "Claim constName violates UPPER_SNAKE_CASE pattern",
        messageFormat:
            "Claim constName '{0}' is invalid — must match ^[A-Z][A-Z0-9_]*$",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateConstName"/>
    public static readonly DiagnosticDescriptor DuplicateConstName = new(
        id: DiagnosticIds.DuplicateConstName,
        title: "Duplicate claim constName",
        messageFormat: "Claim constName '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingSpec"/>
    public static readonly DiagnosticDescriptor MissingSpec = new(
        id: DiagnosticIds.MissingSpec,
        title: "JWT claims spec missing from AdditionalFiles",
        messageFormat:
            "JWT claims spec is missing from <AdditionalFiles> for assembly '{0}'",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.EmptyValue"/>
    public static readonly DiagnosticDescriptor EmptyValue = new(
        id: DiagnosticIds.EmptyValue,
        title: "Claim value is empty",
        messageFormat: "Claim '{0}' has an empty / whitespace-only value",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.Auth.JwtClaims.SourceGen";
}
