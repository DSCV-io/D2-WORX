// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Audiences.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce
/// <see cref="EmitDiagnostic"/> instances with auth-audiences-source-gen
/// descriptor IDs (<c>D2AUD*</c>). The diagnostic record itself lives in
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
