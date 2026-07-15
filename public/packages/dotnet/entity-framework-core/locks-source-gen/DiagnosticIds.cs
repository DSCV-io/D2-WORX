// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AdvisoryLocks.SourceGen;

/// <summary>Diagnostic IDs for the advisory-locks source generator.</summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2LCK001";

    /// <summary>Two entries in the same database share the same <c>constName</c>.</summary>
    public const string DuplicateConstNameInDatabase = "D2LCK002";

    /// <summary>Two entries in the same database share the same <c>key</c> value.</summary>
    public const string DuplicateKeyInDatabase = "D2LCK003";

    /// <summary>Entry's <c>constName</c> does not match the UPPER_SNAKE_CASE pattern.</summary>
    public const string InvalidConstName = "D2LCK004";

    /// <summary>Entry's <c>key</c> value is outside the signed 64-bit integer range.</summary>
    public const string KeyOutOfRange = "D2LCK005";
}
