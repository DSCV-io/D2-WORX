// -----------------------------------------------------------------------
// <copyright file="IUserOwned.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.Abstractions;

/// <summary>
/// Marks an entity as owned by a user subject. The anonymization engine reads
/// <see cref="UserId"/> to scope an erasure sweep — only rows where
/// <c>UserId == subjectId</c> are processed.
/// </summary>
/// <remarks>
/// A record may implement both <see cref="IUserOwned"/> and <see cref="IOrgOwned"/>
/// when it is owned by both a user and an organization (e.g. a resource created by
/// a user within an org context).
///
/// The id is nullable because a row may be created before the owning user is assigned
/// (e.g. draft entities). The engine builds its filter against the nullable column
/// and skips rows where <c>UserId</c> is <see langword="null"/>.
///
/// Reuses the entity's existing identity column — no duplicate ownership column is added.
/// </remarks>
public interface IUserOwned
{
    /// <summary>
    /// Gets the id of the user who owns this entity, or <see langword="null"/> if
    /// ownership has not been assigned.
    /// </summary>
    Guid? UserId { get; }
}
