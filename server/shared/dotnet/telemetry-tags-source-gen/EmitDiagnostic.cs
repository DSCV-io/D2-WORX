// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostic.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Telemetry.Tags.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// A diagnostic produced by <see cref="TelemetrySpecLoader"/> /
/// <see cref="TelemetryTagsEmitter"/> / <see cref="CrossSpecResolver"/>.
/// Decoupled from <c>Microsoft.CodeAnalysis.Diagnostic</c> so the loader,
/// emitter, and resolver are unit-testable without instantiating a Roslyn
/// host. The generator's <see cref="TelemetryTagsGenerator.Initialize"/>
/// translates these to real Roslyn <c>Diagnostic</c> instances.
/// </summary>
/// <param name="DescriptorId">
/// Matches a <see cref="DiagnosticIds"/> identifier (e.g. <c>"D2TEL001"</c>).
/// </param>
/// <param name="Args">
/// Arguments to format into the descriptor's <c>messageFormat</c> template.
/// </param>
internal sealed record EmitDiagnostic(string DescriptorId, ImmutableArray<object> Args)
{
    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MalformedSpec"/> diagnostic.
    /// </summary>
    /// <param name="path">The spec file path.</param>
    /// <param name="reason">The parse-failure reason.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MalformedSpec(string path, string reason) =>
        new(DiagnosticIds.MalformedSpec, [path, reason]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateMeter"/> diagnostic.
    /// </summary>
    /// <param name="meter">The duplicated meter name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateMeter(string meter) =>
        new(DiagnosticIds.DuplicateMeter, [meter]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateInstrument"/> diagnostic.
    /// </summary>
    /// <param name="instrument">The duplicated instrument name.</param>
    /// <param name="meter">The owning meter.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateInstrument(string instrument, string meter) =>
        new(DiagnosticIds.DuplicateInstrument, [instrument, meter]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.UnknownInstrumentKind"/> diagnostic.
    /// </summary>
    /// <param name="instrument">The instrument name.</param>
    /// <param name="meter">The owning meter.</param>
    /// <param name="kind">The offending kind value.</param>
    /// <param name="validValues">A comma-separated list of accepted kinds.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic UnknownInstrumentKind(
        string instrument, string meter, string kind, string validValues) =>
        new(DiagnosticIds.UnknownInstrumentKind, [instrument, meter, kind, validValues]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateTagValue"/> diagnostic.
    /// </summary>
    /// <param name="instrument">The instrument name.</param>
    /// <param name="tag">The tag name.</param>
    /// <param name="meter">The owning meter.</param>
    /// <param name="value">The duplicated value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateTagValue(
        string instrument, string tag, string meter, string value) =>
        new(DiagnosticIds.DuplicateTagValue, [instrument, tag, meter, value]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.CrossSpecInconsistency"/> diagnostic.
    /// </summary>
    /// <param name="instrument">The instrument name.</param>
    /// <param name="tag">The tag name.</param>
    /// <param name="meter">The owning meter.</param>
    /// <param name="specName">The cross-spec reference value.</param>
    /// <param name="reason">Human-readable reason.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic CrossSpecInconsistency(
        string instrument, string tag, string meter, string specName, string reason) =>
        new(
            DiagnosticIds.CrossSpecInconsistency,
            [instrument, tag, meter, specName, reason]);
}
