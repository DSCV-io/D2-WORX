// -----------------------------------------------------------------------
// <copyright file="AnonymizationClassification.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

using System.Collections.Generic;

/// <summary>
/// The result of classifying an EF Core entity type for anonymization. Produced by
/// <see cref="AnonymizationTierClassifier.Classify"/> and consumed by both the
/// anonymization engine and the startup model validator.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Columns"/> is ordered deterministically: root entity scalars in
/// <c>IEntityType.GetProperties()</c> order, then complex sub-properties, then owned
/// sub-properties. This ordering makes the anonymization engine's chained
/// <c>ExecuteUpdate.SetProperty</c> sequence and the startup validator's diagnostics
/// stable across runs.
/// </para>
/// <para>
/// When <see cref="Tier"/> is <see cref="AnonymizationTier.TierC"/>, <see cref="TierCBlocker"/>
/// names the first property whose shape forced the demotion. The blocker value contains only
/// model metadata (property name, column name, shape) — never runtime user data — so it is
/// safe to include in diagnostics.
/// </para>
/// </remarks>
public sealed record AnonymizationClassification
{
    /// <summary>
    /// Gets the anonymization tier for the classified entity type.
    /// </summary>
    public required AnonymizationTier Tier { get; init; }

    /// <summary>
    /// Gets the ordered list of annotated columns the anonymization engine acts on.
    /// Empty when no property on the entity carries a <c>D2:Anonymize</c> annotation.
    /// </summary>
    public required IReadOnlyList<AnonymizationColumn> Columns { get; init; }

    /// <summary>
    /// Gets the first annotated column whose shape forced a <see cref="AnonymizationTier.TierC"/>
    /// classification, or <see langword="null"/> when <see cref="Tier"/> is not
    /// <see cref="AnonymizationTier.TierC"/>. The startup model validator uses this to emit a
    /// PII-safe diagnostic identifying the offending property.
    /// </summary>
    public AnonymizationColumn? TierCBlocker { get; init; }
}
