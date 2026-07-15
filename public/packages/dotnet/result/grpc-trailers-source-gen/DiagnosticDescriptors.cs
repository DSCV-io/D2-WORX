// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Grpc.Trailers.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared
/// in <see cref="DiagnosticIds"/>. Only loaded inside the Roslyn host (the
/// generator's <see cref="GrpcTrailersGenerator.Initialize"/> call site);
/// pure-logic callers should use <see cref="DiagnosticIds"/> string
/// constants directly to avoid pulling <c>Microsoft.CodeAnalysis</c> at
/// runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "gRPC trailers spec is malformed",
        messageFormat: "gRPC trailers spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateConstName"/>
    public static readonly DiagnosticDescriptor DuplicateConstName = new(
        id: DiagnosticIds.DuplicateConstName,
        title: "Duplicate gRPC trailer constName",
        messageFormat:
            "gRPC trailer constName '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateValue"/>
    public static readonly DiagnosticDescriptor DuplicateValue = new(
        id: DiagnosticIds.DuplicateValue,
        title: "Duplicate gRPC trailer wire value",
        messageFormat:
            "gRPC trailer wire value '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.InvalidConstName"/>
    public static readonly DiagnosticDescriptor InvalidConstName = new(
        id: DiagnosticIds.InvalidConstName,
        title: "gRPC trailer constName has invalid shape",
        messageFormat:
            "gRPC trailer constName '{0}' must match UPPER_SNAKE_CASE pattern ^[A-Z][A-Z0-9_]*$",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.EmptyValue"/>
    public static readonly DiagnosticDescriptor EmptyValue = new(
        id: DiagnosticIds.EmptyValue,
        title: "gRPC trailer wire value is empty",
        messageFormat:
            "gRPC trailer '{0}' has empty or whitespace-only wire value "
                + "(every trailer must carry a non-empty wire key)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.Grpc.Trailers.SourceGen";
}
