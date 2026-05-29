// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.ErrorCodes.SourceGen;

/// <summary>
/// String identifiers for the diagnostics emitted by <see cref="ErrorCodesGenerator"/>.
/// Kept in a separate class from <see cref="DiagnosticDescriptors"/> so
/// non-Roslyn-host consumers (e.g. unit tests of the pure-logic
/// <see cref="ErrorCodesEmitter"/> / <see cref="ErrorCodeSpecLoader"/>) can
/// reference the IDs without dragging in <c>Microsoft.CodeAnalysis</c> (which
/// the SrcGen csproj marks <c>PrivateAssets="all"</c>).
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2AEC001";

    /// <summary>
    /// Entry's <c>category</c> is not one of the closed enum values
    /// (<c>validation_failure</c> / <c>infrastructure_unavailable</c> /
    /// <c>policy_denied</c>).
    /// </summary>
    public const string UnknownCategoryEnum = "D2AEC002";

    /// <summary>Two entries share the same <c>code</c>.</summary>
    public const string DuplicateCode = "D2AEC003";

    /// <summary>Two entries share the same <c>factoryName</c>.</summary>
    public const string DuplicateFactoryName = "D2AEC004";

    /// <summary>
    /// Entry's <c>httpStatus</c> is not in the supported set (<c>401</c> / <c>503</c>);
    /// expanding the matrix requires updating the codegen mapping.
    /// </summary>
    public const string InvalidHttpStatus = "D2AEC005";
}
