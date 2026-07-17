// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ProblemDetails.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by
/// <see cref="ProblemDetailsGenerator"/>. Kept in a separate class from
/// <see cref="DiagnosticDescriptors"/> so non-Roslyn-host consumers
/// (e.g. unit tests of the pure-logic <see cref="ProblemDetailsEmitter"/>
/// / <see cref="ProblemDetailsSpecLoader"/>) can reference the IDs without
/// dragging in <c>Microsoft.CodeAnalysis</c> (which the SrcGen csproj marks
/// <c>PrivateAssets="all"</c>).
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2PRB001";

    /// <summary>Two extension keys share the same <c>constName</c>.</summary>
    public const string DuplicateExtensionKeyConstName = "D2PRB002";

    /// <summary>Two extension keys share the same wire <c>value</c>.</summary>
    public const string DuplicateExtensionKeyValue = "D2PRB003";

    /// <summary>Two titles share the same <c>constName</c>.</summary>
    public const string DuplicateTitleConstName = "D2PRB004";

    /// <summary>
    /// Two titles share the same <c>httpStatus</c> (only one entry MAY carry
    /// each status; <c>null</c> marks the singular fallback entry).
    /// </summary>
    public const string DuplicateTitleHttpStatus = "D2PRB005";

    /// <summary>
    /// The <c>typeUriPrefix</c> value does not end with a trailing slash,
    /// which would produce malformed URIs at runtime when concatenated with
    /// the kebab-cased error code.
    /// </summary>
    public const string TypeUriPrefixMissingTrailingSlash = "D2PRB006";
}
