// -----------------------------------------------------------------------
// <copyright file="AnonymizationColumnShape.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

/// <summary>
/// Describes how an annotated property is mapped in the EF Core relational model,
/// which in turn determines whether the anonymization engine can reach it via a bulk
/// update or requires materialization, or cannot reach it at all.
/// </summary>
public enum AnonymizationColumnShape
{
    /// <summary>
    /// A plain scalar property directly on the root entity type, mapped to the entity's own
    /// table. Corresponds to a property returned by <c>IEntityType.GetProperties()</c> where
    /// the entity type is not owned.
    /// </summary>
    Scalar = 0,

    /// <summary>
    /// A scalar property on an owned entity type configured with <c>OwnsOne(...)</c> without
    /// <c>.ToJson()</c>, where the owned type shares the owner's table (table splitting).
    /// The column name already includes the owned-navigation prefix that EF Core applies.
    /// </summary>
    TableSplitOwned = 1,

    /// <summary>
    /// A scalar property on a complex type configured with <c>ComplexProperty(...)</c>,
    /// regardless of whether the complex type is table-split or mapped to JSON
    /// (<c>.ToJson()</c>). EF Core 10 <c>ExecuteUpdate</c> can reach both variants.
    /// </summary>
    Complex = 2,

    /// <summary>
    /// A property on an owned entity type configured with <c>OwnsOne(...).ToJson()</c>.
    /// EF Core 10 <c>ExecuteUpdate</c> throws <c>JsonExecuteUpdateNotSupportedWithOwnedEntities</c>
    /// for these. Entities with any <see cref="OwnedJson"/> property are classified as
    /// <see cref="AnonymizationTier.TierC"/> and rejected by the startup model validator.
    /// </summary>
    OwnedJson = 3,

    /// <summary>
    /// A property on an owned entity type configured with <c>OwnsMany(...)</c>, mapped to its
    /// own child table (a separate table, not the owner's table). Entities with any
    /// <see cref="OwnsManyChild"/> property are classified as <see cref="AnonymizationTier.TierC"/>
    /// and rejected by the startup model validator.
    /// </summary>
    OwnsManyChild = 4,
}
