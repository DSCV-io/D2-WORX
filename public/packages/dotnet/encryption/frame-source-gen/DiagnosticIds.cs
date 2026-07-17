// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionFrame.SourceGen;

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

    /// <summary>Sealed spec file is malformed JSON or violates the schema.</summary>
    public const string SealedMalformedSpec = "D2EF006";

    /// <summary>Two sealed-spec field entries share the same <c>constName</c>.</summary>
    public const string SealedDuplicateFieldName = "D2EF007";

    /// <summary>Two fixed-offset sealed-spec fields overlap each other.</summary>
    public const string SealedOverlappingFields = "D2EF008";

    /// <summary>
    /// Sealed-spec field declares an invalid length
    /// (negative when not -1 sentinel; zero on fixed-length).
    /// </summary>
    public const string SealedInvalidLength = "D2EF009";

    /// <summary>
    /// Sealed spec version is invalid (must be ≥ 2 — version 1 is the
    /// symmetric frame's discriminator).
    /// </summary>
    public const string SealedInvalidVersion = "D2EF010";

    /// <summary>
    /// Sealed-spec field declares a kind outside the closed set the sealed
    /// codec knows how to read.
    /// </summary>
    public const string SealedUnknownFieldKind = "D2EF011";

    /// <summary>
    /// A <c>variable_binary_u16be</c> field is not immediately preceded by a
    /// <c>byte_fixed</c> length-prefix field of the declared prefix width.
    /// </summary>
    public const string SealedBinaryLengthPrefixMissing = "D2EF012";
}
