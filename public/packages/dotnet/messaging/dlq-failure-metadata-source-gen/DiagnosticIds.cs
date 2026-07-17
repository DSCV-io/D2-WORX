// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.DlqMetadata.SourceGen;

/// <summary>Diagnostic IDs for dlq-failure-metadata source-gen.</summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2DLQ001";

    /// <summary>Two field entries share the same <c>constName</c>.</summary>
    public const string DuplicateFieldConstName = "D2DLQ002";

    /// <summary>Two field entries share the same wire <c>value</c>.</summary>
    public const string DuplicateFieldValue = "D2DLQ003";

    /// <summary>Two cause entries share the same <c>constName</c> or <c>value</c>.</summary>
    public const string DuplicateCause = "D2DLQ004";

    /// <summary>Entry's <c>constName</c> doesn't match UPPER_SNAKE_CASE.</summary>
    public const string InvalidConstName = "D2DLQ005";

    /// <summary>Entry's <c>value</c> is empty or whitespace-only.</summary>
    public const string EmptyValue = "D2DLQ006";
}
