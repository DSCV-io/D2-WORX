// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.EncryptionFrame.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>Roslyn descriptors for the IDs in <see cref="DiagnosticIds"/>.</summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Encryption-frame spec is malformed",
        messageFormat: "Encryption-frame spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateFieldName"/>
    public static readonly DiagnosticDescriptor DuplicateFieldName = new(
        id: DiagnosticIds.DuplicateFieldName,
        title: "Duplicate encryption-frame field constName",
        messageFormat:
            "Encryption-frame field constName '{0}' is declared more than once",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.OverlappingFields"/>
    public static readonly DiagnosticDescriptor OverlappingFields = new(
        id: DiagnosticIds.OverlappingFields,
        title: "Encryption-frame fixed-offset fields overlap",
        messageFormat:
            "Encryption-frame field '{0}' overlaps field '{1}' at fixed offsets",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidLength"/>
    public static readonly DiagnosticDescriptor InvalidLength = new(
        id: DiagnosticIds.InvalidLength,
        title: "Encryption-frame field has invalid length",
        messageFormat:
            "Encryption-frame field '{0}' has invalid length {1} "
                + "(must be ≥ 1 or the -1 variable sentinel)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidVersion"/>
    public static readonly DiagnosticDescriptor InvalidVersion = new(
        id: DiagnosticIds.InvalidVersion,
        title: "Encryption-frame spec version is invalid",
        messageFormat:
            "Encryption-frame spec version {0} is invalid (must be ≥ 1)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "D2.Shared.EncryptionFrame.SourceGen";
}
