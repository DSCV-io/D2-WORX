// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostic.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Audiences.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// A diagnostic produced by <see cref="AudienceSpecLoader"/> or
/// <see cref="AudiencesEmitter"/>. Decoupled from
/// <c>Microsoft.CodeAnalysis.Diagnostic</c> so the loader and emitter are
/// unit-testable without instantiating a Roslyn host. The generator's
/// <see cref="AudiencesGenerator.Initialize"/> translates these to real Roslyn
/// <c>Diagnostic</c> instances.
/// </summary>
/// <param name="DescriptorId">
/// Matches a <see cref="DiagnosticIds"/> identifier (e.g. <c>"D2AUD001"</c>).
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
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidAudienceName"/> diagnostic.
    /// </summary>
    /// <param name="audienceName">The offending audience name.</param>
    /// <param name="reason">Explanation of why the name was rejected.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidAudienceName(string audienceName, string reason) =>
        new(DiagnosticIds.InvalidAudienceName, [audienceName, reason]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateAudienceName"/> diagnostic.
    /// </summary>
    /// <param name="audienceName">The duplicated audience name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateAudienceName(string audienceName) =>
        new(DiagnosticIds.DuplicateAudienceName, [audienceName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateAudienceUrl"/> diagnostic.
    /// </summary>
    /// <param name="firstName">The first audience name using the URL.</param>
    /// <param name="secondName">The second audience name using the URL.</param>
    /// <param name="url">The duplicated URL.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateAudienceUrl(
        string firstName,
        string secondName,
        string url) =>
        new(DiagnosticIds.DuplicateAudienceUrl, [firstName, secondName, url]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidAudienceUrl"/> diagnostic.
    /// </summary>
    /// <param name="audienceName">The audience whose URL is invalid.</param>
    /// <param name="url">The offending URL string.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidAudienceUrl(string audienceName, string url) =>
        new(DiagnosticIds.InvalidAudienceUrl, [audienceName, url]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.MissingSpecFile"/> diagnostic.
    /// </summary>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic MissingSpecFile() =>
        new(DiagnosticIds.MissingSpecFile, []);
}
