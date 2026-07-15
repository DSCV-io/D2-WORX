// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionDomains.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>Roslyn descriptors for the IDs in <see cref="DiagnosticIds"/>.</summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Encryption domains spec is malformed",
        messageFormat: "Encryption domains spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateConstName"/>
    public static readonly DiagnosticDescriptor DuplicateConstName = new(
        id: DiagnosticIds.DuplicateConstName,
        title: "Duplicate encryption-domain constName",
        messageFormat:
            "Encryption-domain constName '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateValue"/>
    public static readonly DiagnosticDescriptor DuplicateValue = new(
        id: DiagnosticIds.DuplicateValue,
        title: "Duplicate encryption-domain wire value",
        messageFormat:
            "Encryption-domain wire value '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidConstName"/>
    public static readonly DiagnosticDescriptor InvalidConstName = new(
        id: DiagnosticIds.InvalidConstName,
        title: "Encryption-domain constName has invalid shape",
        messageFormat:
            "Encryption-domain constName '{0}' must match UPPER_SNAKE_CASE pattern "
                + "^[A-Z][A-Z0-9_]*$",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.EmptyValue"/>
    public static readonly DiagnosticDescriptor EmptyValue = new(
        id: DiagnosticIds.EmptyValue,
        title: "Encryption-domain wire value is empty",
        messageFormat:
            "Encryption-domain '{0}' has empty or whitespace-only wire value",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidMode"/>
    public static readonly DiagnosticDescriptor InvalidMode = new(
        id: DiagnosticIds.InvalidMode,
        title: "Encryption-domain mode is invalid",
        messageFormat:
            "Encryption-domain '{0}' has invalid mode '{1}' — "
                + "must be 'symmetric' or 'sealed'",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.MissingConsumerService"/>
    public static readonly DiagnosticDescriptor MissingConsumerService = new(
        id: DiagnosticIds.MissingConsumerService,
        title: "Sealed encryption-domain is missing consumerService",
        messageFormat:
            "Encryption-domain '{0}' is sealed but declares no consumerService "
                + "(the single decrypting recipient's ServiceId is required)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.UnexpectedConsumerService"/>
    public static readonly DiagnosticDescriptor UnexpectedConsumerService = new(
        id: DiagnosticIds.UnexpectedConsumerService,
        title: "Non-sealed encryption-domain declares a consumerService",
        messageFormat:
            "Encryption-domain '{0}' declares consumerService '{1}' but is not "
                + "sealed — consumerService is meaningful only for sealed domains",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidConsumerService"/>
    public static readonly DiagnosticDescriptor InvalidConsumerService = new(
        id: DiagnosticIds.InvalidConsumerService,
        title: "Encryption-domain consumerService has invalid shape",
        messageFormat:
            "Encryption-domain '{0}' consumerService '{1}' must match the "
                + "workload grammar ^[a-z0-9-]{{1,64}}$",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.EncryptionDomains.SourceGen";
}
