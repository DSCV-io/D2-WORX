// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.EncryptionFrame.SourceGen;

/// <summary>Diagnostic IDs for encryption-frame source-gen.</summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2EF001";

    /// <summary>Two field entries share the same <c>constName</c>.</summary>
    public const string DuplicateFieldName = "D2EF002";

    /// <summary>Two fixed-offset fields overlap each other.</summary>
    public const string OverlappingFields = "D2EF003";

    /// <summary>
    /// Field declares an invalid length (negative when not -1 sentinel; zero on fixed-length).
    /// </summary>
    public const string InvalidLength = "D2EF004";

    /// <summary>Spec version is invalid (must be ≥ 1).</summary>
    public const string InvalidVersion = "D2EF005";
}
