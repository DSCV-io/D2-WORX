// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.ErrorCodes.SourceGen;

using D2.Shared.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce
/// <see cref="EmitDiagnostic"/> instances with auth-error-codes-source-gen
/// descriptor IDs (<c>D2AEC*</c>). The diagnostic record itself lives in
/// <c>D2.Shared.SourceGen</c> (shared across every source generator); only
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
    /// Constructs a <see cref="DiagnosticDescriptors.UnknownCategoryEnum"/> diagnostic.
    /// </summary>
    /// <param name="code">The error code whose category is unknown.</param>
    /// <param name="value">The offending category value.</param>
    /// <param name="validValues">A comma-separated list of accepted values.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic UnknownCategoryEnum(
        string code, string value, string validValues) =>
        new(DiagnosticIds.UnknownCategoryEnum, [code, value, validValues]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateCode"/> diagnostic.
    /// </summary>
    /// <param name="code">The duplicated error code.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateCode(string code) =>
        new(DiagnosticIds.DuplicateCode, [code]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.DuplicateFactoryName"/> diagnostic.
    /// </summary>
    /// <param name="factoryName">The duplicated factory name.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic DuplicateFactoryName(string factoryName) =>
        new(DiagnosticIds.DuplicateFactoryName, [factoryName]);

    /// <summary>
    /// Constructs a <see cref="DiagnosticDescriptors.InvalidHttpStatus"/> diagnostic.
    /// </summary>
    /// <param name="code">The error code.</param>
    /// <param name="httpStatus">The unsupported HTTP status value.</param>
    /// <param name="supportedValues">A comma-separated list of supported values.</param>
    /// <returns>A new <see cref="EmitDiagnostic"/>.</returns>
    public static EmitDiagnostic InvalidHttpStatus(
        string code, int httpStatus, string supportedValues) =>
        new(DiagnosticIds.InvalidHttpStatus, [code, httpStatus, supportedValues]);
}
