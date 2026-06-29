// -----------------------------------------------------------------------
// <copyright file="OptionsSigningDomainAuthorityPolicy.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Infrastructure.Configuration;

using D2.Shared.Utilities.Extensions;

/// <summary>
/// Resolves a workload's allowed cross-process signing domains from
/// <see cref="SigningDomainAuthorityOptions"/>. A workload absent from the policy
/// (or a null / empty workload id) resolves to the EMPTY set — default-deny.
/// </summary>
/// <param name="options">The bound signing-domain authority options.</param>
public sealed class OptionsSigningDomainAuthorityPolicy(
    IOptions<SigningDomainAuthorityOptions> options)
    : ISigningDomainAuthorityPolicy
{
    private static readonly IReadOnlySet<string> sr_empty =
        new HashSet<string>(StringComparer.Ordinal);

    /// <inheritdoc/>
    public IReadOnlySet<string> AllowedSigningDomainsFor(string? workloadId)
    {
        // Default-deny: no caller id ⇒ nothing is allowed.
        if (workloadId.Falsey())
            return sr_empty;

        var map = options.Value.AllowedSigningDomainsByWorkload;

        // workloadId is non-null here (Falsey() early-returned for null/empty); the
        // null-forgiving operator tells the compiler what Falsey() proved at runtime.
        if (!map.TryGetValue(workloadId!, out var domains) || domains.Falsey())
            return sr_empty;

        // Domain values are case-sensitive lowercase wire strings; the rule compares
        // them ordinally against KeyDomain.Value.
        return new HashSet<string>(domains, StringComparer.Ordinal);
    }
}
