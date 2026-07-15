// -----------------------------------------------------------------------
// <copyright file="ISigningDomainAuthorityPolicy.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Configuration;

/// <summary>
/// Resolves the set of signing key domains a cross-process workload is permitted to
/// sign with — the policy arm of the capability-general authority. The sign handler
/// resolves this provider and passes its result into the pure
/// <c>WorkloadCapabilityAuthority.AuthorizeSigning</c> rule.
/// </summary>
/// <remarks>
/// The default implementation reads <c>IOptions&lt;SigningDomainAuthorityOptions&gt;</c>.
/// An unknown workload resolves to the EMPTY set (default-deny) — a workload that is
/// not in the policy may sign with nothing.
/// </remarks>
public interface ISigningDomainAuthorityPolicy
{
    /// <summary>
    /// Resolves the allowed cross-process signing domains for the supplied workload.
    /// </summary>
    /// <param name="workloadId">The caller workload service id (e.g. <c>"edge"</c>).</param>
    /// <returns>
    /// The set of signing-domain wire values the workload may sign with, or the
    /// EMPTY set when the workload is unknown / null / empty (default-deny).
    /// </returns>
    IReadOnlySet<string> AllowedSigningDomainsFor(string? workloadId);
}
