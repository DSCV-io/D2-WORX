// -----------------------------------------------------------------------
// <copyright file="Role.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// User role within an organization. Stored as a lowercase string in the
/// <c>d2_org_role</c> JWT claim and in the database. Roles are <b>discrete
/// capability sets</b> — they do NOT form a hierarchy (e.g., Auditors can
/// READ more than Agents but can WRITE nothing, so Auditor is not "below
/// Agent" in any privilege ordering).
/// </summary>
/// <remarks>
/// Capability assignment is per-(role, org_type) tuple, declared in the
/// scope spec via each scope's <c>grantedTo</c> field (in
/// <c>contracts/auth-scopes/scopes.spec.json</c>). The codegen-emitted
/// <c>Scopes.GrantedScopes</c> dictionary is the canonical
/// <c>(OrgType, Role) → scope-set</c> lookup that Edge consumes at JWT mint time.
/// </remarks>
public enum Role
{
    /// <summary>
    /// Auditor — extensive read access across the organization (compliance / oversight
    /// purpose). No write capability. Distinct from "low-privilege" — auditors typically
    /// see MORE data than Agents, just can't change it.
    /// </summary>
    Auditor,

    /// <summary>
    /// Agent — standard operational role. Day-to-day reads and writes within the
    /// agent's assigned scope of work.
    /// </summary>
    Agent,

    /// <summary>
    /// Officer — org-management capabilities (member management, configuration changes,
    /// organizational policy administration).
    /// </summary>
    Officer,

    /// <summary>
    /// Owner — full control over the organization, including org-level destructive
    /// operations.
    /// </summary>
    Owner,
}
