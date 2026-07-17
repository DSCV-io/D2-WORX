// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Tags.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs declared
/// in <see cref="DiagnosticIds"/>. Only loaded inside the Roslyn host (the
/// generator's <see cref="TelemetryTagsGenerator.Initialize"/> call site);
/// pure-logic callers should use <see cref="DiagnosticIds"/> string constants
/// directly to avoid pulling <c>Microsoft.CodeAnalysis</c> at runtime.
/// </summary>
internal static class DiagnosticDescriptors
{
    /// <inheritdoc cref="DiagnosticIds.MalformedSpec"/>
    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Telemetry spec is malformed",
        messageFormat: "Telemetry spec '{0}' is malformed or schema-violating: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateMeter"/>
    public static readonly DiagnosticDescriptor DuplicateMeter = new(
        id: DiagnosticIds.DuplicateMeter,
        title: "Duplicate telemetry meter name",
        messageFormat: "Telemetry meter '{0}' is declared more than once in the spec",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateInstrument"/>
    public static readonly DiagnosticDescriptor DuplicateInstrument = new(
        id: DiagnosticIds.DuplicateInstrument,
        title: "Duplicate instrument name within meter",
        messageFormat:
            "Instrument '{0}' is declared more than once on meter '{1}'",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.UnknownInstrumentKind"/>
    public static readonly DiagnosticDescriptor UnknownInstrumentKind = new(
        id: DiagnosticIds.UnknownInstrumentKind,
        title: "Unknown telemetry instrument kind",
        messageFormat:
            "Instrument '{0}' on meter '{1}' has unknown kind '{2}' (valid: {3})",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.DuplicateTagValue"/>
    public static readonly DiagnosticDescriptor DuplicateTagValue = new(
        id: DiagnosticIds.DuplicateTagValue,
        title: "Duplicate tag value",
        messageFormat:
            "Instrument '{0}' tag '{1}' on meter '{2}' has duplicate value '{3}'",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc cref="DiagnosticIds.CrossSpecInconsistency"/>
    public static readonly DiagnosticDescriptor CrossSpecInconsistency = new(
        id: DiagnosticIds.CrossSpecInconsistency,
        title: "Telemetry cross-spec reference cannot be resolved",
        messageFormat:
            "Instrument '{0}' tag '{1}' on meter '{2}' references unknown / "
            + "unresolvable spec '{3}': {4}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private const string _CATEGORY = "DcsvIo.D2.Telemetry.Tags.SourceGen";
}
