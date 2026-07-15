// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Tags.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce
/// <see cref="EmitDiagnostic"/> instances with telemetry-tags-source-gen
/// descriptor IDs (<c>D2TEL*</c>). The diagnostic record itself lives in
/// <c>DcsvIo.D2.SourceGen</c> (shared across every source generator); only
/// the per-topic factory shape lives here.
/// </summary>
internal static class EmitDiagnostics
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
