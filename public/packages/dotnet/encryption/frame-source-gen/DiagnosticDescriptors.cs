// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionFrame.SourceGen;

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

    /// <inheritdoc cref="DiagnosticIds.SealedMalformedSpec"/>
    public static readonly DiagnosticDescriptor SealedMalformedSpec = new(
        id: DiagnosticIds.SealedMalformedSpec,
        title: "Sealed encryption-frame spec is malformed",
        messageFormat: "Sealed encryption-frame spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.SealedDuplicateFieldName"/>
    public static readonly DiagnosticDescriptor SealedDuplicateFieldName = new(
        id: DiagnosticIds.SealedDuplicateFieldName,
        title: "Duplicate sealed encryption-frame field constName",
        messageFormat:
            "Sealed encryption-frame field constName '{0}' is declared more than once",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.SealedOverlappingFields"/>
    public static readonly DiagnosticDescriptor SealedOverlappingFields = new(
        id: DiagnosticIds.SealedOverlappingFields,
        title: "Sealed encryption-frame fixed-offset fields overlap",
        messageFormat:
            "Sealed encryption-frame field '{0}' overlaps field '{1}' at fixed offsets",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.SealedInvalidLength"/>
    public static readonly DiagnosticDescriptor SealedInvalidLength = new(
        id: DiagnosticIds.SealedInvalidLength,
        title: "Sealed encryption-frame field has invalid length",
        messageFormat:
            "Sealed encryption-frame field '{0}' has invalid length {1} "
                + "(must be ≥ 1 or the -1 variable sentinel)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.SealedInvalidVersion"/>
    public static readonly DiagnosticDescriptor SealedInvalidVersion = new(
        id: DiagnosticIds.SealedInvalidVersion,
        title: "Sealed encryption-frame spec version is invalid",
        messageFormat:
            "Sealed encryption-frame spec version {0} is invalid "
                + "(must be ≥ 2 — version 1 is the symmetric frame)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.SealedUnknownFieldKind"/>
    public static readonly DiagnosticDescriptor SealedUnknownFieldKind = new(
        id: DiagnosticIds.SealedUnknownFieldKind,
        title: "Sealed encryption-frame field kind is unknown",
        messageFormat:
            "Sealed encryption-frame field '{0}' declares unknown kind '{1}' "
                + "(the sealed codec has no read arm for it)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.SealedBinaryLengthPrefixMissing"/>
    public static readonly DiagnosticDescriptor SealedBinaryLengthPrefixMissing = new(
        id: DiagnosticIds.SealedBinaryLengthPrefixMissing,
        title: "Sealed encryption-frame binary field lacks its length prefix",
        messageFormat:
            "Sealed encryption-frame field '{0}' is variable_binary_u16be but is not "
                + "immediately preceded by a byte_fixed length field of {1} byte(s)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.EncryptionFrame.SourceGen";
}
