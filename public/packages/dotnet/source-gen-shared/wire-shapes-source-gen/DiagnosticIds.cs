// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.WireShapes.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by
/// <see cref="WireShapesGenerator"/>. Kept in a separate class from
/// <see cref="DiagnosticDescriptors"/> so non-Roslyn-host consumers
/// (e.g. unit tests of the pure-logic <see cref="WireShapeEmitter"/> /
/// <see cref="WireShapeSpecLoader"/>) can reference the IDs without
/// dragging in <c>Microsoft.CodeAnalysis</c> (which the SrcGen csproj
/// marks <c>PrivateAssets="all"</c>).
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2WS001";

    /// <summary>Two properties share the same <c>constName</c>.</summary>
    public const string DuplicatePropertyConstName = "D2WS002";

    /// <summary>Two properties share the same wire <c>value</c>.</summary>
    public const string DuplicatePropertyValue = "D2WS003";

    /// <summary>
    /// A property's <c>constName</c> does not match
    /// <c>^[A-Z][A-Z0-9_]*$</c> (UPPER_SNAKE_CASE).
    /// </summary>
    public const string InvalidConstName = "D2WS004";

    /// <summary>
    /// No spec file was passed to the analyzer for a target catalog
    /// assembly. The consuming csproj is missing an
    /// <c>&lt;AdditionalFiles&gt;</c> entry for the spec.
    /// </summary>
    public const string MissingSpec = "D2WS005";
}
