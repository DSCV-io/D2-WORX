// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.ProtocolAudiences.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce <see cref="EmitDiagnostic"/>
/// instances with protocol-audiences-source-gen descriptor IDs (<c>D2PAUD*</c>).
/// The diagnostic record itself lives in <c>DcsvIo.D2.SourceGen</c> (shared
/// across every source generator); only the per-topic factory shape lives here.
/// </summary>
internal static class EmitDiagnostics
{
    /// <summary>Constructs a <see cref="DiagnosticDescriptors.DuplicateName"/> diagnostic.</summary>
    /// <param name="name">The duplicated protocol-audience name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateName(string name) =>
        new(DiagnosticIds.DuplicateName, [name]);

    /// <summary>Constructs a <see cref="DiagnosticDescriptors.InvalidName"/> diagnostic.</summary>
    /// <param name="name">The offending protocol-audience name.</param>
    /// <param name="reason">Explanation of why the name was rejected.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidName(string name, string reason) =>
        new(DiagnosticIds.InvalidName, [name, reason]);

    /// <summary>Constructs a <see cref="DiagnosticDescriptors.DuplicateValue"/> diagnostic.</summary>
    /// <param name="firstName">The first protocol-audience name using the value.</param>
    /// <param name="secondName">The second protocol-audience name using the value.</param>
    /// <param name="value">The duplicated value.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateValue(
        string firstName,
        string secondName,
        string value) =>
        new(DiagnosticIds.DuplicateValue, [firstName, secondName, value]);

    /// <summary>Constructs a <see cref="DiagnosticDescriptors.EmptyValue"/> diagnostic.</summary>
    /// <param name="name">The protocol-audience whose value is empty.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic EmptyValue(string name) =>
        new(DiagnosticIds.EmptyValue, [name]);

    /// <summary>Constructs a <see cref="DiagnosticDescriptors.MalformedSpec"/> diagnostic.</summary>
    /// <param name="path">The spec file path.</param>
    /// <param name="reason">The parse-failure reason.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MalformedSpec(string path, string reason) =>
        new(DiagnosticIds.MalformedSpec, [path, reason]);

    /// <summary>Constructs a <see cref="DiagnosticDescriptors.MissingSpecFile"/> diagnostic.</summary>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingSpecFile() =>
        new(DiagnosticIds.MissingSpecFile, []);
}
