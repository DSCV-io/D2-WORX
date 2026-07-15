// -----------------------------------------------------------------------
// <copyright file="IKeyringDomainAuthorityPolicy.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Configuration;

/// <summary>
/// Resolves the set of payload key domains a workload is permitted to fetch a keyring for
/// — the policy arm of the capability-general authority. The keyring handler resolves this
/// provider and passes its result into the pure
/// <c>WorkloadCapabilityAuthority.AuthorizeKeyringFetch</c> rule.
/// </summary>
/// <remarks>
/// The default implementation reads <c>IOptions&lt;KeyringDomainAuthorityOptions&gt;</c>.
/// An unknown workload resolves to the EMPTY set (default-deny) — a workload not in the
/// policy may fetch no keyring. Unlike the signing policy, the workload key may be EITHER
/// a cross-process SPIFFE workload id OR an in-process module id (both share the bare
/// <c>[a-z0-9-]</c> identifier grammar and both flow through <c>ImmediateCaller</c>).
/// </remarks>
public interface IKeyringDomainAuthorityPolicy
{
    /// <summary>
    /// Resolves the allowed keyring domains for the supplied workload / module id.
    /// </summary>
    /// <param name="workloadId">
    /// The caller id (a cross-process SPIFFE workload id like <c>"audit"</c>, or an
    /// in-process module id like <c>"edge"</c>).
    /// </param>
    /// <returns>
    /// The set of keyring-domain wire values the caller may fetch, or the EMPTY set when
    /// the caller is unknown / null / empty (default-deny).
    /// </returns>
    IReadOnlySet<string> AllowedKeyringDomainsFor(string? workloadId);
}
