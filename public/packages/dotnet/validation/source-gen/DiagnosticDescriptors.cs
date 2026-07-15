// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared
/// in <see cref="DiagnosticIds"/>. Only loaded inside the Roslyn host (the
/// generator's <see cref="FieldConstraintsGenerator.Initialize"/> call site);
/// pure-logic callers should use <see cref="DiagnosticIds"/> string constants
/// directly to avoid pulling <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Field-constraints spec is malformed",
        messageFormat: "Field-constraints spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateConstName"/>
    public static readonly DiagnosticDescriptor DuplicateConstName = new(
        id: DiagnosticIds.DuplicateConstName,
        title: "Duplicate field-length constant name",
        messageFormat: "Field-length constant '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidConstName"/>
    public static readonly DiagnosticDescriptor InvalidConstName = new(
        id: DiagnosticIds.InvalidConstName,
        title: "Field-length constant name is empty or does not match SCREAMING_SNAKE",
        messageFormat:
            "Field-length constant '{0}' is empty or violates the SCREAMING_SNAKE convention "
            + "(expected pattern: ^[A-Z][A-Z0-9_]*$)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.NonPositiveValue"/>
    public static readonly DiagnosticDescriptor NonPositiveValue = new(
        id: DiagnosticIds.NonPositiveValue,
        title: "Field-length value is not a positive integer",
        messageFormat: "Field-length constant '{0}' has non-positive value '{1}' (must be > 0)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateEnumName"/>
    public static readonly DiagnosticDescriptor DuplicateEnumName = new(
        id: DiagnosticIds.DuplicateEnumName,
        title: "Duplicate taxonomy enum name",
        messageFormat: "Taxonomy enum '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidEnumName"/>
    public static readonly DiagnosticDescriptor InvalidEnumName = new(
        id: DiagnosticIds.InvalidEnumName,
        title: "Taxonomy enum name is empty or not a valid identifier",
        messageFormat:
            "Taxonomy enum '{0}' is empty or is not a valid PascalCase C# identifier",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.EmptyEnumMemberList"/>
    public static readonly DiagnosticDescriptor EmptyEnumMemberList = new(
        id: DiagnosticIds.EmptyEnumMemberList,
        title: "Taxonomy enum declares no members",
        messageFormat: "Taxonomy enum '{0}' declares an empty members list (must have >= 1)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateEnumMember"/>
    public static readonly DiagnosticDescriptor DuplicateEnumMember = new(
        id: DiagnosticIds.DuplicateEnumMember,
        title: "Duplicate taxonomy enum member",
        messageFormat: "Taxonomy enum '{0}' declares member '{1}' more than once",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidEnumMemberName"/>
    public static readonly DiagnosticDescriptor InvalidEnumMemberName = new(
        id: DiagnosticIds.InvalidEnumMemberName,
        title: "Taxonomy enum member name is empty or not a valid identifier",
        messageFormat:
            "Taxonomy enum '{0}' member '{1}' is empty or is not a valid C# identifier",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.Validation.SourceGen";
}
