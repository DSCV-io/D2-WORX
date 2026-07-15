// -----------------------------------------------------------------------
// <copyright file="AnonymizationTier.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

/// <summary>
/// Classifies an entity type into one of three anonymization tiers based on
/// how its annotated properties are mapped in the EF Core model.
/// </summary>
/// <remarks>
/// <para>
/// The tier determines which anonymization path the anonymization engine uses for the entity:
/// <see cref="TierA"/> for a single bulk-update SQL path,
/// <see cref="TierB"/> for a materialize-mutate-save path, and
/// <see cref="TierC"/> for entities the engine cannot process.
/// </para>
/// <para>
/// Roll-up precedence is <see cref="TierC"/> &gt; <see cref="TierB"/> &gt; <see cref="TierA"/>:
/// a single Tier-C shape anywhere in the entity's owned subtree demotes the whole entity to
/// <see cref="TierC"/>; a single
/// <see cref="DcsvIo.D2.DataGovernance.Abstractions.AnonymizeKind.Template"/>
/// field with no C-shape demotes the entity to <see cref="TierB"/>.
/// </para>
/// </remarks>
public enum AnonymizationTier
{
    /// <summary>
    /// All annotated properties are constant-rule fields on scalar, table-split-owned, or
    /// complex-property shapes. The anonymization engine anonymizes these entities with a single
    /// chained <c>ExecuteUpdateAsync</c> call — no materialization required.
    /// </summary>
    TierA = 0,

    /// <summary>
    /// At least one annotated property uses a
    /// <see cref="DcsvIo.D2.DataGovernance.Abstractions.AnonymizeKind.Template"/> rule, and
    /// no property is in an unsupported shape. The anonymization engine must materialize each
    /// row, resolve the template against sibling values, mutate the instance, and call
    /// <c>SaveChangesAsync</c>.
    /// </summary>
    TierB = 1,

    /// <summary>
    /// At least one annotated property is in a mapping shape that the anonymization engine
    /// cannot reach — specifically an owned entity mapped to JSON (<c>OwnsOne(...).ToJson()</c>)
    /// or an <c>OwnsMany</c> child table. The startup model validator rejects entities in this
    /// tier at host startup so they never reach the engine.
    /// </summary>
    TierC = 2,
}
