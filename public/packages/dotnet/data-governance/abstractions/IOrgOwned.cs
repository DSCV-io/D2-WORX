// -----------------------------------------------------------------------
// <copyright file="IOrgOwned.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.Abstractions;

/// <summary>
/// Marks an entity as owned by an organization subject. The anonymization engine reads
/// <see cref="OrgId"/> to scope an erasure sweep — only rows where
/// <c>OrgId == subjectId</c> are processed.
/// </summary>
/// <remarks>
/// A record may implement both <see cref="IOrgOwned"/> and <see cref="IUserOwned"/>
/// when it is owned by both an organization and a user within that organization.
///
/// The id is nullable because a row may be created before the owning organization is
/// assigned (e.g. draft entities or multi-tenant bootstrapping flows). The engine builds
/// its filter against the nullable column and skips rows where <c>OrgId</c> is
/// <see langword="null"/>.
///
/// Reuses the entity's existing identity column — no duplicate ownership column is added.
/// </remarks>
public interface IOrgOwned
{
    /// <summary>
    /// Gets the id of the organization that owns this entity, or <see langword="null"/>
    /// if ownership has not been assigned.
    /// </summary>
    Guid? OrgId { get; }
}
