// -----------------------------------------------------------------------
// <copyright file="AnonymizeKind.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.Abstractions;

/// <summary>
/// Specifies the overwrite strategy the anonymization engine uses when erasing a
/// decorated PII field.
/// </summary>
/// <remarks>
/// This is a closed set — there are exactly four strategies. Adding a fifth requires
/// a coordinated change here, in the anonymization engine, and in the startup
/// divergence guard.
///
/// Crypto-shred (derive a field-specific key from a per-subject secret and encrypt the
/// value so that destroying the key renders the data unrecoverable) is not currently
/// supported and would require a fifth member plus engine support.
/// </remarks>
public enum AnonymizeKind
{
    /// <summary>
    /// Overwrite the field with <see langword="null"/>. The column must be nullable in the
    /// schema. Use when downstream consumers must be able to distinguish "user deleted" from
    /// "empty string".
    /// </summary>
    SetNull = 0,

    /// <summary>
    /// Overwrite the field with an empty string (<c>""</c>). The column must be a string
    /// type. Prefer over <see cref="Constant"/> with an empty constant for clarity of intent.
    /// </summary>
    SetEmpty = 1,

    /// <summary>
    /// Overwrite the field with a fixed developer-supplied tombstone string (e.g.
    /// <c>"[deleted]"</c> or <c>"deleted@deleted.invalid"</c>). The constant value is the
    /// <see cref="AnonymizationRule.ConstantValue"/> on the resolved rule.
    /// </summary>
    Constant = 2,

    /// <summary>
    /// Overwrite the field with a computed tombstone that incorporates sibling field values
    /// via <c>{FieldName}</c> interpolation tokens. For example,
    /// <c>"deletedUser{UserId}@deleted.user.dcsv.io"</c> produces a unique, deterministic
    /// placeholder per row. The template string is the <see cref="AnonymizationRule.Template"/>
    /// on the resolved rule. Field names are resolved against sibling properties on the same
    /// entity; <c>{Guid}</c> tokens are rendered without dashes (32 hex chars).
    /// </summary>
    Template = 3,
}
