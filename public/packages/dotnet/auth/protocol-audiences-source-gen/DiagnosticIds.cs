// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.ProtocolAudiences.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by
/// <see cref="ProtocolAudiencesGenerator"/>. Mirror the TypeScript
/// <c>tools/ts-codegen</c> <c>D2PAUD*</c> IDs byte-for-byte — the same spec source
/// on both sides means the same predicate-violation surface. Kept in a separate
/// class from <see cref="DiagnosticDescriptors"/> so non-Roslyn-host consumers
/// (e.g. unit tests of the pure-logic <see cref="ProtocolAudiencesEmitter"/> /
/// <see cref="ProtocolAudienceSpecLoader"/>) can reference the IDs without
/// dragging in <c>Microsoft.CodeAnalysis</c> (which the SrcGen csproj marks
/// <c>PrivateAssets="all"</c>).
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Two protocol-audience entries share the exact same name (duplicate).</summary>
    public const string DuplicateName = "D2PAUD001";

    /// <summary>
    /// Protocol-audience name violates the SCREAMING_SNAKE_CASE convention (must
    /// match <c>^[A-Z][A-Z0-9_]*$</c>).
    /// </summary>
    public const string InvalidName = "D2PAUD002";

    /// <summary>Two protocol-audience entries share the exact same value (duplicate).</summary>
    public const string DuplicateValue = "D2PAUD003";

    /// <summary>Protocol-audience value is empty (would never match a real aud claim).</summary>
    public const string EmptyValue = "D2PAUD004";

    /// <summary>Protocol-audience spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2PAUD009";

    /// <summary>No protocol-audience spec file found among <c>AdditionalFiles</c>.</summary>
    public const string MissingSpecFile = "D2PAUD010";
}
