// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AdvisoryLocks.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>Roslyn descriptors for the IDs in <see cref="DiagnosticIds"/>.</summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Advisory locks spec is malformed",
        messageFormat: "Advisory locks spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateConstNameInDatabase"/>
    public static readonly DiagnosticDescriptor DuplicateConstNameInDatabase = new(
        id: DiagnosticIds.DuplicateConstNameInDatabase,
        title: "Duplicate advisory-lock constName within database",
        messageFormat:
            "Advisory-lock constName '{0}' is declared more than once "
            + "in database '{1}'",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateKeyInDatabase"/>
    public static readonly DiagnosticDescriptor DuplicateKeyInDatabase = new(
        id: DiagnosticIds.DuplicateKeyInDatabase,
        title: "Duplicate advisory-lock key within database",
        messageFormat:
            "Advisory-lock key {0} is already used by '{1}' in database '{2}'; "
            + "each key must be unique within its database",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidConstName"/>
    public static readonly DiagnosticDescriptor InvalidConstName = new(
        id: DiagnosticIds.InvalidConstName,
        title: "Advisory-lock constName has invalid shape",
        messageFormat:
            "Advisory-lock constName '{0}' must match UPPER_SNAKE_CASE pattern "
            + "^[A-Z][A-Z0-9_]*$",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.KeyOutOfRange"/>
    public static readonly DiagnosticDescriptor KeyOutOfRange = new(
        id: DiagnosticIds.KeyOutOfRange,
        title: "Advisory-lock key is out of signed 64-bit integer range",
        messageFormat:
            "Advisory-lock '{0}' key value {1} is outside the valid range "
            + "[{2}, {3}] for a PostgreSQL int8 advisory lock",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.AdvisoryLocks.SourceGen";
}
