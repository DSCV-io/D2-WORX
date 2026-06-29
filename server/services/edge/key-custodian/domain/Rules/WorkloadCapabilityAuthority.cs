// -----------------------------------------------------------------------
// <copyright file="WorkloadCapabilityAuthority.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Rules;

/// <summary>
/// The capability-general workload→target authority — the pure decision every
/// signing / sealing consumer plugs into. It answers "may workload W use
/// capability C (sign / seal-encrypt / seal-decrypt) on target D".
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure rule, no IO / port / options.</b> No DB, no <c>IOptions</c>, no logging,
/// never throws — every arm returns a <see cref="D2Result"/> (allow = <c>Ok</c>,
/// deny = a typed failure). The policy (the workload→allowed-signing-domains map)
/// is a method PARAMETER, never an injected option; the handler that calls this
/// rule resolves the policy and owns the counter / log on a deny (the same shape as
/// <see cref="WorkloadCertificateIssuance"/> returning a result the handler audits).
/// </para>
/// <para>
/// <b>The three capabilities have structurally distinct authority.</b>
/// </para>
/// <list type="bullet">
///   <item>
///     <b>Signing</b> is policy-driven with a structural in-process-only backstop.
///     A cross-process caller can NEVER sign with an in-process-only domain
///     (<c>jwks-signing</c>) even if the policy were misconfigured to grant it — the
///     structural deny runs BEFORE the policy check (defense-in-depth: two
///     independent guards must fail to reach the cluster signing key — this
///     structural one, and the boot-time config validator that refuses to grant an
///     in-process-only domain to any workload).
///   </item>
///   <item>
///     <b>Seal-encrypt</b> is broad — any caller with an authenticated identity may
///     fetch any public seal key (public material is harmless to over-share; the
///     transport scope already gated WHETHER the caller may seal at all).
///   </item>
///   <item>
///     <b>Seal-decrypt</b> is self-only, enforced by the op SHAPE: the
///     <c>getOwnSealPrivateKey()</c> op carries NO target, so the key is selected by
///     the authenticated identity alone — there is nothing to compare. This rule's
///     contribution is the explicit statement that no in-handler
///     <c>caller == target</c> check is needed; it returns <c>Ok</c> keyed on the
///     authenticated identity being present (deny when absent — fail-closed).
///   </item>
/// </list>
/// <para>
/// <b>Fail-closed.</b> The authenticated caller identity is surfaced by the single
/// capability-general peer-identity accessor (<c>GetD2PeerWorkloadIdentity()</c>) —
/// absent (e.g. a non-mTLS connection) ⇒ the accessor yields <c>null</c> ⇒ every arm
/// here denies. The workload id is an authenticated PUBLIC service label, not a
/// secret, so the membership / presence checks are plain comparisons (no
/// constant-time requirement).
/// </para>
/// </remarks>
public static class WorkloadCapabilityAuthority
{
    /// <summary>
    /// The closed set of key domains whose key material is in-process-only — it MUST
    /// never be reachable by a cross-process caller. <c>jwks-signing</c> is the root
    /// of mint-once-forward (the cluster JWT signing key); it never leaves the Edge
    /// host process. The structural <see cref="AuthorizeSigning"/> deny + the
    /// boot-time config validator both key on this set.
    /// </summary>
    /// <remarks>
    /// The comparer is <c>OrdinalIgnoreCase</c> so a value stored verbatim via
    /// <c>KeyDomain.FromTrusted</c> (EF read path) or typed non-lowercase in
    /// configuration still matches — a case variant must not silently bypass the
    /// in-process-only deny or the boot-gate check in
    /// <c>SigningDomainAuthorityOptions.Validate()</c>.
    /// </remarks>
    public static readonly IReadOnlySet<string> InProcessOnlySigningDomains =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { KeyDomain.JWKS_SIGNING };

    /// <summary>
    /// Decides whether a caller may sign with a key domain. Layered deny
    /// (defense-in-depth):
    /// <list type="number">
    ///   <item>
    ///     <b>Structural in-process-only deny</b> — when the call is cross-process AND
    ///     the target is an in-process-only domain, deny with
    ///     <c>CrossProcessDomainRejected</c> REGARDLESS of policy.
    ///   </item>
    ///   <item>
    ///     <b>In-process clean-dual invariant</b> — the in-process plane may sign
    ///     ONLY with in-process-only domains (<c>jwks-signing</c>); a request from
    ///     the in-process leaf for any other domain is denied with
    ///     <c>SigningDomainNotAuthorized</c>. Each domain is signable from exactly
    ///     one plane: in-process-only domains in-process; all others cross-process
    ///     via policy.
    ///   </item>
    ///   <item>
    ///     <b>Policy deny</b> — cross-process allow when the target is in the caller's
    ///     allowed-signing-domains set; else deny with
    ///     <c>SigningDomainNotAuthorized</c>.
    ///   </item>
    /// </list>
    /// </summary>
    /// <param name="callerWorkloadId">
    /// The authenticated caller workload id (from the peer-identity accessor), or
    /// <see langword="null"/> when no mTLS peer identity is present (fail-closed).
    /// </param>
    /// <param name="isCrossProcess">
    /// Whether this request crossed a process boundary (set <c>true</c> by the
    /// gRPC-service-layer guard; the in-process minter passes <c>false</c>). Defaults
    /// to the cross-process-safe value <c>true</c> so a caller that fails to set it
    /// cannot reach the in-process-allow branch.
    /// </param>
    /// <param name="target">The signing key domain the caller wants to sign with.</param>
    /// <param name="allowedSigningDomainsForCaller">
    /// The set of signing-domain wire values the caller is permitted to sign with
    /// (resolved from the signing-domain authority policy; an unknown workload
    /// resolves to the empty set — default-deny).
    /// </param>
    /// <returns>
    /// <c>Ok</c> when the caller may sign with <paramref name="target"/>;
    /// <c>CrossProcessDomainRejected</c> (403) for a cross-process in-process-only-domain
    /// request; <c>SigningDomainNotAuthorized</c> (403) when the domain is not in the
    /// caller's allowed set or when an in-process caller requests a non-in-process-only
    /// domain; <c>Forbidden</c> (403) when no caller identity is present.
    /// </returns>
    public static D2Result AuthorizeSigning(
        string? callerWorkloadId,
        bool isCrossProcess,
        KeyDomain target,
        IReadOnlySet<string> allowedSigningDomainsForCaller)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(allowedSigningDomainsForCaller);

        // Fail-closed: a cross-process call with no authenticated peer identity can
        // never be authorized. (The in-process leaf legitimately has no peer cert —
        // it passes isCrossProcess=false and is callable in-process only.)
        if (isCrossProcess && callerWorkloadId.Falsey())
            return D2Result.Forbidden();

        // 1) Structural in-process-only deny — independent of policy. A cross-process
        // caller can NEVER sign with jwks-signing even if the policy granted it.
        if (isCrossProcess && InProcessOnlySigningDomains.Contains(target.Value))
            return KeyCustodianFailures.CrossProcessDomainRejected();

        // 2) In-process clean-dual invariant: the in-process leaf may sign ONLY with
        // in-process-only domains (jwks-signing). An in-process request for any other
        // domain is denied — each domain is signable from exactly one plane.
        // (The in-process leaf is structurally reachable ONLY from within the Edge host
        // process; the gRPC entry always carries a peer cert because RequireCertificate
        // is on, so it can never present isCrossProcess=false.)
        if (!isCrossProcess)
        {
            return InProcessOnlySigningDomains.Contains(target.Value)
                ? D2Result.Ok()
                : KeyCustodianFailures.SigningDomainNotAuthorized();
        }

        // 3) Cross-process policy deny — the target must be in the caller's
        // allowed-signing-domains set (a non-in-process-only domain reaches here).
        if (!allowedSigningDomainsForCaller.Contains(target.Value))
            return KeyCustodianFailures.SigningDomainNotAuthorized();

        return D2Result.Ok();
    }

    /// <summary>
    /// Decides whether a caller may fetch a target service's PUBLIC seal key
    /// (seal-encrypt). Broad: any caller with an authenticated identity is allowed —
    /// public key material is harmless to over-share, and the transport scope already
    /// gated whether the caller may seal at all. The decision is per-caller, not
    /// per-target (fetching any public key is harmless).
    /// </summary>
    /// <param name="callerWorkloadId">
    /// The authenticated caller workload id, or <see langword="null"/> when no peer
    /// identity is present (fail-closed).
    /// </param>
    /// <returns>
    /// <c>Ok</c> when a caller identity is present; <c>Forbidden</c> (403) when absent.
    /// </returns>
    public static D2Result AuthorizeSealEncrypt(string? callerWorkloadId)
    {
        // Fail-closed: no authenticated identity ⇒ deny. Any present identity is
        // authorized (broad) — the per-target check is intentionally absent.
        if (callerWorkloadId.Falsey())
            return D2Result.Forbidden();

        return D2Result.Ok();
    }

    /// <summary>
    /// Decides whether a caller may fetch its OWN PRIVATE seal key (seal-decrypt).
    /// Self-only is enforced STRUCTURALLY by the op shape — the
    /// <c>getOwnSealPrivateKey()</c> op carries no target, so the key is selected by
    /// the authenticated identity alone and there is nothing to compare. This arm's
    /// only contribution is the fail-closed presence check: a present authenticated
    /// identity is authorized to fetch its own key; an absent one is denied. No
    /// in-handler <c>caller == target</c> comparison exists because there is no target.
    /// </summary>
    /// <param name="callerWorkloadId">
    /// The authenticated caller workload id, or <see langword="null"/> when no peer
    /// identity is present (fail-closed).
    /// </param>
    /// <returns>
    /// <c>Ok</c> when a caller identity is present (it may fetch its own key);
    /// <c>Forbidden</c> (403) when absent.
    /// </returns>
    public static D2Result AuthorizeSealDecrypt(string? callerWorkloadId)
    {
        // Fail-closed: no authenticated identity ⇒ no key to select ⇒ deny. A present
        // identity may only ever get ITS OWN key — the op carries no target parameter,
        // so self-only is structural, not a comparison.
        if (callerWorkloadId.Falsey())
            return D2Result.Forbidden();

        return D2Result.Ok();
    }
}
