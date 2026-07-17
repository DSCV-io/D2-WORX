// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.InProcessKeys.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by
/// <see cref="InProcessKeysGenerator"/>. Kept in a separate class from
/// <see cref="DiagnosticDescriptors"/> so non-Roslyn-host consumers can
/// reference the IDs without dragging in <c>Microsoft.CodeAnalysis</c>.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2IPK001";

    /// <summary>
    /// <c>bindings</c> array contains an unknown binding (closed enum:
    /// <c>http</c> / <c>grpc</c>).
    /// </summary>
    public const string UnknownBinding = "D2IPK002";

    /// <summary><c>constName</c> violates UPPER_SNAKE_CASE pattern.</summary>
    public const string InvalidConstName = "D2IPK003";

    /// <summary>
    /// <c>keys.spec.json</c> not present in <c>&lt;AdditionalFiles&gt;</c>
    /// for the consuming csproj.
    /// </summary>
    public const string MissingSpec = "D2IPK004";
}
