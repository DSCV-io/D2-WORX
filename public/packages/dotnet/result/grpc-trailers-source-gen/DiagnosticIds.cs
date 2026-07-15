// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Grpc.Trailers.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by
/// <see cref="GrpcTrailersGenerator"/>. Kept in a separate class from
/// <see cref="DiagnosticDescriptors"/> so non-Roslyn-host consumers (e.g.
/// unit tests of the pure-logic <see cref="GrpcTrailersEmitter"/> /
/// <see cref="GrpcTrailersSpecLoader"/>) can reference the IDs without
/// dragging in <c>Microsoft.CodeAnalysis</c> (which the SrcGen csproj marks
/// <c>PrivateAssets="all"</c>).
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2GT001";

    /// <summary>Two entries share the same <c>constName</c>.</summary>
    public const string DuplicateConstName = "D2GT002";

    /// <summary>Two entries share the same wire <c>value</c>.</summary>
    public const string DuplicateValue = "D2GT003";

    /// <summary>Entry's <c>constName</c> doesn't match the UPPER_SNAKE_CASE pattern.</summary>
    public const string InvalidConstName = "D2GT004";

    /// <summary>Entry's <c>value</c> is empty or whitespace-only.</summary>
    public const string EmptyValue = "D2GT005";
}
