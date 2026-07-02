// -----------------------------------------------------------------------
// <copyright file="KeyringDomainAuthorityOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Infrastructure.Configuration;

using D2.Shared.WorkloadIdentity;

/// <summary>
/// Configuration-bindable shape of the keyring-domain authority policy: which payload
/// key domains each workload is permitted to fetch a keyring for.
/// </summary>
/// <remarks>
/// <para>
/// Binds from the <c>KEYCUSTODIAN_KEYRING_AUTHORITY</c> configuration section
/// (environment-variable prefix <c>KEYCUSTODIAN_KEYRING_AUTHORITY__</c>). The startup
/// binding + the fail-loud validator that refuses to boot a dangerous configuration live
/// in the Infra layer; this type is the App-owned options shape.
/// </para>
/// <para>
/// <b>Both caller namespaces.</b> Unlike the signing policy (cross-process only), the
/// keyring surface serves BOTH cross-process backend services (keyed by their SPIFFE
/// workload id) AND the in-host module consuming the leaf (keyed by its module id). The
/// two share the bare <c>[a-z0-9-]</c> identifier grammar
/// (<see cref="SpiffeWorkloadIdentity.Create"/> validates that bare form and does NOT
/// require a <c>spiffe://…</c> URI), so ONE grammar check admits both — the map is keyed
/// by whichever <c>ImmediateCaller</c> the establishing boundary set.
/// </para>
/// <para>
/// <b>Only payload domains are grantable.</b> A keyring is a full encrypt+decrypt
/// capability for its domain. The non-payload crown-jewel domains (<c>jwks-signing</c>,
/// <c>cookie</c>, <c>client-secret</c>, the <c>mtls-ca-*</c> trust anchors) must NEVER
/// appear in any workload's allowed set — granting one is a fail-loud startup error. This
/// boot gate is the production guard behind the handler's defense-in-depth key-type fork:
/// because no caller can ever hold a non-payload grant, the authority arm denies a
/// non-payload domain with a uniform 403 before the fork is reachable.
/// </para>
/// <para>
/// <b>Empty is legitimately fine (fail-CLOSED).</b> In dev there may be no keyring
/// consumers yet; an empty policy makes every lookup return the empty set ⇒ deny-all ⇒
/// safe. Emptiness is NOT a boot error; a dangerous-VALUE (a non-payload-domain grant, an
/// empty-string key, a non-catalog domain) IS.
/// </para>
/// </remarks>
public sealed class KeyringDomainAuthorityOptions
{
    /// <summary>The configuration section name this options type binds from.</summary>
    public const string SECTION = "KEYCUSTODIAN_KEYRING_AUTHORITY";

    /// <summary>
    /// Gets the per-workload allowed keyring domains. Key = a bare lowercase caller id —
    /// a cross-process SPIFFE workload service id (e.g. <c>"audit"</c>) OR an in-process
    /// module id (e.g. <c>"edge"</c>); value = the payload key domains that caller may
    /// fetch a keyring for. A caller absent from this map resolves to the empty set
    /// (default-deny). A non-payload domain must NOT appear under ANY key — the config
    /// validator rejects it at boot.
    /// </summary>
    /// <remarks>
    /// The comparer is <c>OrdinalIgnoreCase</c> because <c>IConfiguration</c>'s
    /// environment-variable provider uppercases keys on Windows (<c>EDGE</c>) while
    /// caller ids are lowercase (<c>edge</c>). A case-sensitive comparer would silently
    /// miss a configured workload on a Windows deployment.
    /// </remarks>
    public Dictionary<string, List<string>> AllowedKeyringDomainsByWorkload { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Validates the policy fail-loud, returning the first invariant violation (or
    /// <see langword="null"/> when valid). An EMPTY policy is valid (deny-all). The
    /// host's startup binding (Infra) calls this through <c>ValidateOnStart</c> so a
    /// dangerous configuration refuses to boot. The dangerous configurations are:
    /// <list type="bullet">
    ///   <item>
    ///     An empty / whitespace / non-grammar workload key (incl. a <c>*</c> wildcard).
    ///     The grammar is the shared bare <c>[a-z0-9-]</c> identifier form that admits
    ///     BOTH cross-process SPIFFE workload ids AND in-process module ids.
    ///   </item>
    ///   <item>A value naming a non-catalog key domain (incl. a <c>*</c> wildcard).</item>
    ///   <item>
    ///     A grant of a NON-payload domain (any domain whose bound
    ///     <see cref="KeyType"/> is not <see cref="KeyType.AesPayload"/>) to ANY workload
    ///     — a keyring is a full encrypt+decrypt capability, and only payload domains are
    ///     keyring-grantable.
    ///   </item>
    /// </list>
    /// </summary>
    /// <returns>The first violation message, or <see langword="null"/> when valid.</returns>
    public string? Validate()
    {
        foreach (var (workloadKey, domains) in AllowedKeyringDomainsByWorkload)
        {
            // The workload key must be a well-formed bare lowercase identifier. This SAME
            // check admits both a cross-process SPIFFE workload id and an in-process
            // module id (they share the [a-z0-9-] grammar) and rejects empty / whitespace
            // keys AND a "*" wildcard (outside [a-z0-9-]).
            if (!SpiffeWorkloadIdentity.Create(workloadKey).Success)
            {
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"KEYCUSTODIAN_KEYRING_AUTHORITY has an invalid workload key "
                    + $"'{workloadKey}' (must be a bare lowercase [a-z0-9-] identifier — a "
                    + $"SPIFFE workload id or an in-process module id).");
            }

            foreach (var domain in domains)
            {
                // The value must be a member of the closed key-domain catalog (this also
                // rejects a "*" wildcard value).
                var createResult = KeyDomain.Create(domain);

                if (!createResult.Success)
                {
                    return string.Create(
                        CultureInfo.InvariantCulture,
                        $"KEYCUSTODIAN_KEYRING_AUTHORITY workload '{workloadKey}' is granted "
                        + $"a non-catalog key domain '{domain}'.");
                }

                // Only payload (AES) domains are keyring-grantable. A non-payload domain
                // (jwks-signing / cookie / client-secret / the CA trust anchors) is a
                // crown-jewel key whose keyring is never distributable — reject it at boot.
                // The bound KeyType is derived from the catalog entry (a domain fact), so
                // a case variant like "COOKIE" is caught after KeyDomain.Create normalizes.
                if (createResult.Data!.KeyType != KeyType.AesPayload)
                {
                    return string.Create(
                        CultureInfo.InvariantCulture,
                        $"KEYCUSTODIAN_KEYRING_AUTHORITY workload '{workloadKey}' is granted "
                        + $"the non-payload key domain '{domain}' (only payload-encryption "
                        + $"domains are keyring-grantable — a keyring is a full "
                        + $"encrypt+decrypt capability, and the non-payload crown-jewel "
                        + $"domains must not be granted to any workload).");
                }
            }
        }

        return null;
    }
}
