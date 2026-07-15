// -----------------------------------------------------------------------
// <copyright file="AnonymizationColumn.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

using DcsvIo.D2.DataGovernance.Abstractions;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Carries the resolved anonymization metadata for a single annotated property on an
/// entity type. Assembled by <see cref="AnonymizationTierClassifier"/> from live EF Core
/// model metadata and the stored <c>D2:Anonymize</c> annotation.
/// </summary>
/// <remarks>
/// <para>
/// This record is a metadata container, not a wire or spec shape — it mirrors no
/// <c>.proto</c>, <c>.spec.json</c>, or schema definition. It aggregates the relational
/// column name, the EF property reference, the anonymization rule, and the column shape into
/// one value the anonymization engine and the startup model validator consume.
/// </para>
/// <para>
/// The <see cref="Property"/> reference is safe to cache for the lifetime of the process:
/// the EF Core model is immutable after build, so the reference remains valid and stable.
/// </para>
/// </remarks>
public sealed record AnonymizationColumn
{
    /// <summary>
    /// Gets the relational column name as resolved by EF Core
    /// (<c>IProperty.GetColumnName()</c>). This is the column the anonymization engine
    /// targets in bulk-update SQL or reads/writes via the CLR property in the
    /// materialize-mutate path.
    /// </summary>
    public required string ColumnName { get; init; }

    /// <summary>
    /// Gets the CLR property name (<c>IProperty.Name</c>). Used by the startup model
    /// validator for diagnostics and by the anonymization engine's materialize-mutate path
    /// to set the value on a materialized instance.
    /// </summary>
    public required string PropertyName { get; init; }

    /// <summary>
    /// Gets the anonymization rule stored as the <c>D2:Anonymize</c> EF Core model
    /// annotation on this property.
    /// </summary>
    public required AnonymizationRule Rule { get; init; }

    /// <summary>
    /// Gets the EF Core mapping shape that determines which anonymization path the engine
    /// uses and whether the entity is reachable at all.
    /// </summary>
    public required AnonymizationColumnShape Shape { get; init; }

    /// <summary>
    /// Gets the live EF Core <see cref="IProperty"/> reference for this column. The
    /// anonymization engine uses this to access the CLR property getter/setter on materialized
    /// instances in the Tier-B path.
    /// </summary>
    public required IProperty Property { get; init; }
}
