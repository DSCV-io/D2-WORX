// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.OtelMessagingTags.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by
/// <see cref="OtelMessagingTagsGenerator"/>. See parallel
/// <c>DcsvIo.D2.Grpc.Trailers.SourceGen.DiagnosticIds</c> for the rationale
/// behind the split.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2OMT001";

    /// <summary>Two entries share the same <c>constName</c>.</summary>
    public const string DuplicateConstName = "D2OMT002";

    /// <summary>Two entries share the same wire <c>value</c>.</summary>
    public const string DuplicateValue = "D2OMT003";

    /// <summary>Entry's <c>constName</c> doesn't match the UPPER_SNAKE_CASE pattern.</summary>
    public const string InvalidConstName = "D2OMT004";

    /// <summary>Entry's <c>value</c> is empty or whitespace-only.</summary>
    public const string EmptyValue = "D2OMT005";
}
