// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AdvisoryLocks.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>Factory helpers producing per-topic <see cref="EmitDiagnostic"/>.</summary>
internal static class EmitDiagnostics
{
    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MalformedSpec"/> diagnostic.
    /// </summary>
    /// <param name="path">The spec file path.</param>
    /// <param name="reason">The parse-failure reason.</param>
    public static EmitDiagnostic MalformedSpec(string path, string reason) =>
        new(DiagnosticIds.MalformedSpec, [path, reason]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateConstNameInDatabase"/>
    /// diagnostic.
    /// </summary>
    /// <param name="constName">The duplicated constName.</param>
    /// <param name="database">The database the duplicate is in.</param>
    public static EmitDiagnostic DuplicateConstNameInDatabase(
        string constName, string database) =>
        new(DiagnosticIds.DuplicateConstNameInDatabase, [constName, database]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateKeyInDatabase"/>
    /// diagnostic.
    /// </summary>
    /// <param name="key">The duplicated key value.</param>
    /// <param name="existingConstName">The constName already occupying this key.</param>
    /// <param name="database">The database the duplicate is in.</param>
    public static EmitDiagnostic DuplicateKeyInDatabase(
        long key,
        string existingConstName,
        string database) =>
        new(
            DiagnosticIds.DuplicateKeyInDatabase,
            [key.ToString(), existingConstName, database]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidConstName"/> diagnostic.
    /// </summary>
    /// <param name="constName">The offending constName.</param>
    public static EmitDiagnostic InvalidConstName(string constName) =>
        new(DiagnosticIds.InvalidConstName, [constName]);
}
