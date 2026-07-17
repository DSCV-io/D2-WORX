// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Category.SourceGen;

/// <summary>Diagnostic IDs for error-category source-gen.</summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2ECAT001";

    /// <summary>Two entries share the same wire <c>wire</c> string.</summary>
    public const string DuplicateWire = "D2ECAT002";

    /// <summary>Entry's <c>wire</c> doesn't match the snake_case pattern.</summary>
    public const string InvalidWire = "D2ECAT003";

    /// <summary>Entry's <c>doc</c> is empty or whitespace-only.</summary>
    public const string EmptyDoc = "D2ECAT004";
}
