// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.EncryptionDomains.SourceGen;

/// <summary>Diagnostic IDs for encryption-domains source-gen.</summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2ED001";

    /// <summary>Two entries share the same <c>constName</c>.</summary>
    public const string DuplicateConstName = "D2ED002";

    /// <summary>Two entries share the same wire <c>value</c>.</summary>
    public const string DuplicateValue = "D2ED003";

    /// <summary>Entry's <c>constName</c> doesn't match the UPPER_SNAKE_CASE pattern.</summary>
    public const string InvalidConstName = "D2ED004";

    /// <summary>Entry's <c>value</c> is empty or whitespace-only.</summary>
    public const string EmptyValue = "D2ED005";
}
