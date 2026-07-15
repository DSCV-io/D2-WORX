// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Headers.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by <see cref="HeadersGenerator"/>.
/// Kept in a separate class from <see cref="DiagnosticDescriptors"/> so
/// non-Roslyn-host consumers (e.g. unit tests of the pure-logic
/// <see cref="HeadersEmitter"/> / <see cref="HeadersSpecLoader"/>) can
/// reference the IDs without dragging in <c>Microsoft.CodeAnalysis</c> (which
/// the SrcGen csproj marks <c>PrivateAssets="all"</c>).
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2HDR001";

    /// <summary>
    /// <c>applicability</c> array contains an unknown transport (closed enum:
    /// <c>http</c> / <c>grpc</c> / <c>amqp</c>).
    /// </summary>
    public const string UnknownTransport = "D2HDR002";

    /// <summary><c>constName</c> violates UPPER_SNAKE_CASE pattern.</summary>
    public const string InvalidConstName = "D2HDR003";

    /// <summary>
    /// <c>constName</c> collides with another header in any catalog this entry
    /// is applicable to.
    /// </summary>
    public const string DuplicateConstName = "D2HDR004";

    /// <summary>
    /// <c>applicability</c> is empty (each header MUST belong to at least one
    /// transport).
    /// </summary>
    public const string EmptyApplicability = "D2HDR005";

    /// <summary>
    /// <c>convention</c> is outside the closed enum (typo guard — emitter falls
    /// back to documenting the value verbatim). Warning severity.
    /// </summary>
    public const string UnknownConvention = "D2HDR006";

    /// <summary>
    /// <c>headers.spec.json</c> not present in <c>&lt;AdditionalFiles&gt;</c>
    /// for the consuming csproj.
    /// </summary>
    public const string MissingSpec = "D2HDR007";
}
