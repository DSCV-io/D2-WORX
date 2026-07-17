// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by
/// <see cref="FieldConstraintsGenerator"/>. Kept in a separate class from
/// <see cref="DiagnosticDescriptors"/> so non-Roslyn-host consumers
/// (e.g. unit tests of the pure-logic <see cref="FieldConstraintsEmitter"/> /
/// <see cref="FieldConstraintsSpecLoader"/>) can reference the IDs without
/// dragging in <c>Microsoft.CodeAnalysis</c> (which the SrcGen csproj
/// marks <c>PrivateAssets="all"</c>).
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2FC001";

    /// <summary>
    /// Two field-length entries share the same <c>name</c>.
    /// </summary>
    public const string DuplicateConstName = "D2FC002";

    /// <summary>
    /// A field-length <c>name</c> is empty / whitespace / does not match the
    /// SCREAMING_SNAKE convention.
    /// </summary>
    public const string InvalidConstName = "D2FC003";

    /// <summary>
    /// A field-length <c>value</c> is not a positive integer.
    /// </summary>
    public const string NonPositiveValue = "D2FC004";

    /// <summary>
    /// Two enum entries share the same <c>name</c>.
    /// </summary>
    public const string DuplicateEnumName = "D2FC005";

    /// <summary>
    /// An enum <c>name</c> is empty or is not a valid PascalCase C# identifier.
    /// </summary>
    public const string InvalidEnumName = "D2FC006";

    /// <summary>An enum declares an empty <c>members</c> list.</summary>
    public const string EmptyEnumMemberList = "D2FC007";

    /// <summary>Two members of the same enum share the same <c>name</c>.</summary>
    public const string DuplicateEnumMember = "D2FC008";

    /// <summary>
    /// An enum member <c>name</c> is empty or is not a valid C# identifier.
    /// </summary>
    public const string InvalidEnumMemberName = "D2FC009";
}
