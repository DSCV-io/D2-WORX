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
/// <remarks>
/// Each configured grant is canonicalized through <c>KeyDomain.Create</c> (identical
/// normalization to the boot validator — trim + lowercase + catalog membership) when the
/// per-workload set is built, so a legitimately-configured but non-canonical grant
/// (<c>"Audit"</c>, <c>" audit "</c>) authorizes at enforce time instead of booting clean
/// then silently never matching the lowercase <c>KeyDomain.Value</c> the rule compares
/// against. The normalization runs ONCE per options instance (this policy is a singleton),
/// not per <see cref="AllowedSigningDomainsFor"/> call.
/// </remarks>
public sealed class OptionsSigningDomainAuthorityPolicy : ISigningDomainAuthorityPolicy
{
    private static readonly IReadOnlySet<string> sr_empty =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> r_normalizedByWorkload;

    /// <summary>
    /// Initializes a new instance of the <see cref="OptionsSigningDomainAuthorityPolicy"/>
    /// class, normalizing every configured grant once.
    /// </summary>
    /// <param name="options">The bound signing-domain authority options.</param>
    public OptionsSigningDomainAuthorityPolicy(IOptions<SigningDomainAuthorityOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        r_normalizedByWorkload = BuildNormalizedMap(options.Value);
    }

    /// <inheritdoc/>
    public IReadOnlySet<string> AllowedSigningDomainsFor(string? workloadId)
    {
        // Default-deny: no caller id ⇒ nothing is allowed.
        if (workloadId.Falsey())
            return sr_empty;

        // workloadId is non-null here (Falsey() early-returned for null/empty); the
        // null-forgiving operator tells the compiler what Falsey() proved at runtime.
        return r_normalizedByWorkload.TryGetValue(workloadId!, out var domains)
            ? domains
            : sr_empty;
    }

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildNormalizedMap(
        SigningDomainAuthorityOptions options)
    {
        // Match the options map's OrdinalIgnoreCase workload-key comparer (IConfiguration's
        // env-var provider uppercases keys on Windows) so the lookup stays case-robust.
        var result = new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (workloadId, domains) in options.AllowedSigningDomainsByWorkload)
        {
            if (domains.Falsey())
                continue;

            // Domain values compare ordinally against the lowercase KeyDomain.Value, so
            // each grant is canonicalized via KeyDomain.Create (same normalization as the
            // boot validator). An entry that fails Create cannot exist post-boot (the
            // fail-loud validator rejected it), so skip it defensively rather than leak a
            // non-catalog value into the enforce-set.
            var normalized = new HashSet<string>(StringComparer.Ordinal);

            foreach (var domain in domains)
            {
                var createResult = KeyDomain.Create(domain);

                if (createResult.Success)
                    normalized.Add(createResult.Data!.Value);
            }

            if (normalized.Truthy())
                result[workloadId] = normalized;
        }

        return result;
    }
}
