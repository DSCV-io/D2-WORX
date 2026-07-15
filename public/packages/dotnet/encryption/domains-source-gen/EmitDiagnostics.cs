// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionDomains.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>Factory helpers producing per-topic <see cref="EmitDiagnostic"/>.</summary>
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
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateConstName"/> diagnostic.
    /// </summary>
    /// <param name="constName">The duplicated constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateConstName(string constName) =>
        new(DiagnosticIds.DuplicateConstName, [constName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateValue"/> diagnostic.
    /// </summary>
    /// <param name="value">The duplicated wire value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateValue(string value) =>
        new(DiagnosticIds.DuplicateValue, [value]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidConstName"/> diagnostic.
    /// </summary>
    /// <param name="constName">The offending constName.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidConstName(string constName) =>
        new(DiagnosticIds.InvalidConstName, [constName]);

    /// <summary>Constructs a <see cref="DiagnosticDescriptors.EmptyValue"/> diagnostic.</summary>
    /// <param name="constName">The constName whose wire value is empty.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic EmptyValue(string constName) =>
        new(DiagnosticIds.EmptyValue, [constName]);

    /// <summary>Constructs a <see cref="DiagnosticDescriptors.InvalidMode"/> diagnostic.</summary>
    /// <param name="constName">The offending constName.</param>
    /// <param name="mode">The invalid mode literal.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidMode(string constName, string mode) =>
        new(DiagnosticIds.InvalidMode, [constName, mode]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MissingConsumerService"/> diagnostic.
    /// </summary>
    /// <param name="constName">The sealed domain missing a consumerService.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingConsumerService(string constName) =>
        new(DiagnosticIds.MissingConsumerService, [constName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.UnexpectedConsumerService"/> diagnostic.
    /// </summary>
    /// <param name="constName">The non-sealed domain declaring a consumerService.</param>
    /// <param name="consumerService">The unexpected consumerService value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic UnexpectedConsumerService(
        string constName, string consumerService) =>
        new(DiagnosticIds.UnexpectedConsumerService, [constName, consumerService]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidConsumerService"/> diagnostic.
    /// </summary>
    /// <param name="constName">The domain whose consumerService is malformed.</param>
    /// <param name="consumerService">The offending consumerService value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidConsumerService(
        string constName, string consumerService) =>
        new(DiagnosticIds.InvalidConsumerService, [constName, consumerService]);
}
