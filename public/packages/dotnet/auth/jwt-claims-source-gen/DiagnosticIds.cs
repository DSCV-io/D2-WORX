// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.JwtClaims.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by
/// <see cref="JwtClaimsGenerator"/>.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2JWT001";

    /// <summary>
    /// <c>kind</c> is not in the closed enum (<c>standard</c> / <c>d2-custom</c> /
    /// <c>inside-act</c>).
    /// </summary>
    public const string UnknownKind = "D2JWT002";

    /// <summary><c>constName</c> violates UPPER_SNAKE_CASE pattern.</summary>
    public const string InvalidConstName = "D2JWT003";

    /// <summary>Two entries share the same <c>constName</c>.</summary>
    public const string DuplicateConstName = "D2JWT004";

    /// <summary>
    /// <c>jwt-claims.spec.json</c> not present in <c>&lt;AdditionalFiles&gt;</c>.
    /// </summary>
    public const string MissingSpec = "D2JWT005";

    /// <summary><c>value</c> is empty / whitespace-only.</summary>
    public const string EmptyValue = "D2JWT006";
}
