// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ResultErrorCodes.SourceGen;

/// <summary>
/// String identifiers for the generic catalog's per-catalog validation
/// diagnostics (<c>D2EC*</c>). Kept in a separate class from
/// <see cref="DiagnosticDescriptors"/> so non-Roslyn-host consumers (e.g.
/// unit tests of the shared engine emitters) can reference the IDs without
/// dragging in <c>Microsoft.CodeAnalysis</c> (which the SrcGen csproj marks
/// <c>PrivateAssets="all"</c>). The shell maps these ids to descriptors and
/// supplies them to the shared engine via its <c>CatalogConfig</c>; the
/// catalog-neutral engine diagnostics (<c>D2ERC*</c>) live in the engine.
/// </summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2EC001";

    /// <summary>Two entries share the same <c>code</c>.</summary>
    public const string DuplicateCode = "D2EC002";

    /// <summary>
    /// Entry's <c>httpStatus</c> is not in the supported set
    /// (<c>200</c>/<c>206</c>/<c>207</c>/<c>400</c>/<c>401</c>/<c>403</c>/
    /// <c>404</c>/<c>409</c>/<c>413</c>/<c>429</c>/<c>500</c>/<c>503</c>);
    /// expanding the matrix requires updating the codegen mapping.
    /// </summary>
    public const string InvalidHttpStatus = "D2EC003";

    /// <summary>
    /// Entry's <c>code</c> is empty / whitespace / does not match the
    /// SCREAMING_SNAKE convention.
    /// </summary>
    public const string InvalidCode = "D2EC004";

    /// <summary>Entry's <c>doc</c> is missing or whitespace-only.</summary>
    public const string MissingDoc = "D2EC005";
}
