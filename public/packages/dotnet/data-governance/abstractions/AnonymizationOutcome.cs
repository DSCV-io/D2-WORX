// -----------------------------------------------------------------------
// <copyright file="AnonymizationOutcome.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.Abstractions;

/// <summary>
/// Immutable summary of a completed anonymization sweep returned by
/// <see cref="IAnonymizationEngine"/>. All four counters are non-negative integers.
/// </summary>
/// <remarks>
/// <para>
/// A zero-valued outcome (all counters at 0) is valid — it represents the case where
/// no entities matched the subject id (e.g. the user had no data in a particular domain).
/// The engine returns <c>Ok(outcome)</c> with all zeros in this case, NOT a failure.
/// </para>
/// <para>
/// Counter semantics:
/// </para>
/// <list type="bullet">
/// <item>
///   <description>
///     <see cref="EntityTypesProcessed"/> — how many distinct entity CLR types were
///     examined (one per registered entity that implements <see cref="IUserOwned"/> or
///     <see cref="IOrgOwned"/> and is not <see cref="IExemptFromAnonymization"/>).
///   </description>
/// </item>
/// <item>
///   <description>
///     <see cref="RowsAnonymized"/> — total row count actually overwritten across all
///     entity types in this sweep (excludes already-anonymized rows, see
///     <see cref="AlreadyAnonymizedRows"/>).
///   </description>
/// </item>
/// <item>
///   <description>
///     <see cref="EntityTypesSkippedExempt"/> — how many distinct entity CLR types were
///     skipped because they implement <see cref="IExemptFromAnonymization"/>.
///   </description>
/// </item>
/// <item>
///   <description>
///     <see cref="AlreadyAnonymizedRows"/> — rows excluded from the sweep because their
///     <see cref="IAnonymizationTrackable.IsAnonymized"/> flag was already
///     <see langword="true"/>. These rows were not touched; they are counted here to
///     prove idempotency when the caller re-runs the engine.
///   </description>
/// </item>
/// </list>
/// </remarks>
public sealed record AnonymizationOutcome
{
    /// <summary>
    /// Gets the number of distinct entity CLR types processed in this sweep (ownership-
    /// marked, not exempt). Populated regardless of whether any rows were found.
    /// </summary>
    public required int EntityTypesProcessed { get; init; }

    /// <summary>
    /// Gets the total number of rows overwritten by this sweep across all processed entity
    /// types. Does not include rows skipped because
    /// <see cref="IAnonymizationTrackable.IsAnonymized"/> was already
    /// <see langword="true"/>.
    /// </summary>
    public required int RowsAnonymized { get; init; }

    /// <summary>
    /// Gets the number of distinct entity CLR types skipped because they implement
    /// <see cref="IExemptFromAnonymization"/>. Fields on exempt entities are never touched.
    /// </summary>
    public required int EntityTypesSkippedExempt { get; init; }

    /// <summary>
    /// Gets the number of rows excluded from overwriting because their
    /// <see cref="IAnonymizationTrackable.IsAnonymized"/> flag was already
    /// <see langword="true"/>. A non-zero value here confirms that the engine is idempotent
    /// on a repeated invocation for the same subject.
    /// </summary>
    public required int AlreadyAnonymizedRows { get; init; }
}
