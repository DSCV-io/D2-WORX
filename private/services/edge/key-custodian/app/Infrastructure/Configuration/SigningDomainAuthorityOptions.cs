// -----------------------------------------------------------------------
// <copyright file="SigningDomainAuthorityOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Configuration;

using DcsvIo.D2.Spiffe;

/// <summary>
/// Configuration-bindable shape of the signing-domain authority policy: which
/// signing key domains each cross-process workload is permitted to sign with.
/// </summary>
/// <remarks>
/// <para>
/// Binds from the <c>KEYCUSTODIAN_SIGNING_AUTHORITY</c> configuration section
/// (environment-variable prefix <c>KEYCUSTODIAN_SIGNING_AUTHORITY__</c>). The
/// startup binding + the fail-loud validator that refuses to boot a dangerous
/// configuration live in the Infra layer; this type is the App-owned options shape.
/// </para>
/// <para>
/// <b>This is the CROSS-PROCESS signing policy.</b> The never-cross-process-signable
/// domains (<c>jwks-signing</c> — the root of mint-once-forward, signable only by the
/// in-process Edge minter — plus the <c>mtls-ca-*</c> trust anchors, whose private
/// keys sign only certificates through the dedicated issuance path) must NEVER appear
/// in any workload's allowed set; a cross-process caller is denied structurally by
/// the authority rule AND at boot by the config validator. Granting a never-signable
/// domain here is a fail-loud startup error.
/// </para>
/// <para>
/// <b>Empty is legitimately fine (fail-CLOSED).</b> In dev there may be no
/// cross-process signing workloads yet; an empty policy makes every lookup return
/// the empty set ⇒ deny-all ⇒ safe. Emptiness is NOT a boot error; a
/// dangerous-VALUE (an in-process-only-domain grant, an empty-string key, a
/// non-catalog domain) IS.
/// </para>
/// </remarks>
public sealed class SigningDomainAuthorityOptions
{
    /// <summary>The configuration section name this options type binds from.</summary>
    public const string SECTION = "KEYCUSTODIAN_SIGNING_AUTHORITY";

    /// <summary>
    /// Gets the per-workload allowed cross-process signing domains. Key = lowercase
    /// SPIFFE workload service id (e.g. <c>"edge"</c>); value = the signing key
    /// domains that workload may sign with. A workload absent from this map resolves
    /// to the empty set (default-deny). A never-cross-process-signable domain
    /// (<c>jwks-signing</c>, <c>mtls-ca-root</c>, <c>mtls-ca-intermediate</c>) must NOT
    /// appear under ANY key — the config validator rejects it at boot.
    /// </summary>
    /// <remarks>
    /// The comparer is <c>OrdinalIgnoreCase</c> because <c>IConfiguration</c>'s
    /// environment-variable provider uppercases keys on Windows (<c>EDGE</c>) while
    /// workload ids are lowercase (<c>edge</c>). A case-sensitive comparer would
    /// silently miss a configured workload on a Windows deployment.
    /// </remarks>
    public Dictionary<string, List<string>> AllowedSigningDomainsByWorkload { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Validates the policy fail-loud, returning the first invariant violation (or
    /// <see langword="null"/> when valid). An EMPTY policy is valid (deny-all). The
    /// host's startup binding (Infra) calls this through <c>ValidateOnStart</c> so a
    /// dangerous configuration refuses to boot. The dangerous configurations are:
    /// <list type="bullet">
    ///   <item>An empty / whitespace / non-grammar workload key (incl. a <c>*</c> wildcard).</item>
    ///   <item>A value naming a non-catalog key domain (incl. a <c>*</c> wildcard).</item>
    ///   <item>
    ///     A grant of a never-cross-process-signable domain (<c>jwks-signing</c> or a
    ///     certificate-authority domain) to ANY workload — a crown-jewel key is never
    ///     cross-process grantable.
    ///   </item>
    /// </list>
    /// </summary>
    /// <returns>The first violation message, or <see langword="null"/> when valid.</returns>
    public string? Validate()
    {
        foreach (var (workloadKey, domains) in AllowedSigningDomainsByWorkload)
        {
            // The workload key must be a well-formed lowercase SPIFFE workload id —
            // this rejects empty / whitespace keys AND a "*" wildcard (outside [a-z0-9-]).
            if (!SpiffeWorkloadIdentity.Create(workloadKey).Success)
            {
                return string.Create(
                    CultureInfo.InvariantCulture,
                    $"KEYCUSTODIAN_SIGNING_AUTHORITY has an invalid workload key "
                    + $"'{workloadKey}' (must be a lowercase [a-z0-9-] SPIFFE workload id).");
            }

            foreach (var domain in domains)
            {
                // The value must be a member of the closed key-domain catalog (this
                // also rejects a "*" wildcard value). KeyDomain.Create normalizes to
                // lowercase — use the normalized value for the in-process-only check so
                // "JWKS-SIGNING" / "Jwks-Signing" are caught by the case-robust
                // OrdinalIgnoreCase set in WorkloadCapabilityAuthority.
                var createResult = KeyDomain.Create(domain);

                if (!createResult.Success)
                {
                    return string.Create(
                        CultureInfo.InvariantCulture,
                        $"KEYCUSTODIAN_SIGNING_AUTHORITY workload '{workloadKey}' is granted "
                        + $"a non-catalog signing domain '{domain}'.");
                }

                // A never-cross-process-signable domain (jwks-signing + both CA trust
                // anchors) must NEVER be granted to a cross-process workload. Compare the
                // normalized value (from KeyDomain.Create) against the OrdinalIgnoreCase
                // set so a non-lowercase input like "JWKS-SIGNING" is still caught.
                var normalized = createResult.Data!.Value;

                var neverSignable =
                    WorkloadCapabilityAuthority.NeverCrossProcessSignableDomains;

                if (neverSignable.Contains(normalized))
                {
                    return string.Create(
                        CultureInfo.InvariantCulture,
                        $"KEYCUSTODIAN_SIGNING_AUTHORITY workload '{workloadKey}' is granted "
                        + $"the never-cross-process-signable domain '{domain}' (the "
                        + $"cluster-signing root and the certificate-authority trust anchors "
                        + $"are never signable on the general surface and must not be "
                        + $"granted to any workload).");
                }
            }
        }

        return null;
    }
}
