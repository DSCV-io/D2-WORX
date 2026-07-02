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
///     <b>Signing</b> is gated on the locally-established request
///     <see cref="RequestOrigin"/> (recomputed by the receiving boundary from its own
///     unforgeable transport facts, never a propagated wire value) plus the per-workload
///     policy. The cluster-signing root (<c>jwks-signing</c>) is STRUCTURALLY unreachable
///     on this general surface for EVERY established origin — it is signable only through
///     the dedicated minter capability (possession-based, wired in the auth-module
///     composition; see <see cref="AuthorizeMinterSigning"/>). An unestablished origin
///     fails closed; every non-root domain signs cross-process only, per the caller's
///     allowed-signing-domains set.
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
    /// The closed set of key domains that are signable ONLY through the dedicated JWT
    /// minter capability — <c>jwks-signing</c>, the cluster JWT signing key and the root
    /// of mint-once-forward. The general <see cref="AuthorizeSigning"/> surface rejects
    /// every member for EVERY established origin (returning <c>MinterCapabilityRequired</c>);
    /// the boot-time config validator <c>SigningDomainAuthorityOptions.Validate()</c> also
    /// refuses to grant a member to any cross-process workload. The key material never
    /// leaves the Edge host process.
    /// </summary>
    /// <remarks>
    /// The comparer is <c>OrdinalIgnoreCase</c> so a value typed non-lowercase in
    /// configuration (or reaching the rule through any non-<c>Create</c> path) still
    /// matches — a case variant must not silently bypass the minter-only deny or the
    /// boot-gate check.
    /// </remarks>
    public static readonly IReadOnlySet<string> MinterOnlySigningDomains =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { KeyDomain.JWKS_SIGNING };

    /// <summary>
    /// The closed superset of key domains that are NEVER signable on the general
    /// cross-process signing surface: the cluster-signing root (<c>jwks-signing</c>,
    /// the <see cref="MinterOnlySigningDomains"/> subset — signable only through the
    /// dedicated minter capability) plus both certificate-authority domains
    /// (<c>mtls-ca-root</c> / <c>mtls-ca-intermediate</c> — trust anchors whose private
    /// keys sign ONLY certificates through the dedicated issuance path, never arbitrary
    /// caller-supplied bytes). <see cref="AuthorizeSigning"/> structurally denies every
    /// member for EVERY origin, and the boot-time config validator
    /// <c>SigningDomainAuthorityOptions.Validate()</c> refuses to grant a member to any
    /// workload. The two sets answer different questions: "never signable on the
    /// general surface" (this set) vs "signable only via the minter"
    /// (<see cref="MinterOnlySigningDomains"/>).
    /// </summary>
    /// <remarks>
    /// Same <c>OrdinalIgnoreCase</c> rationale as <see cref="MinterOnlySigningDomains"/>:
    /// a case variant must not silently bypass the structural deny or the boot gate.
    /// </remarks>
    public static readonly IReadOnlySet<string> NeverCrossProcessSignableDomains =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            KeyDomain.JWKS_SIGNING,
            KeyDomain.MTLS_CA_ROOT,
            KeyDomain.MTLS_CA_INTERMEDIATE,
        };

    /// <summary>
    /// Decides whether a caller may sign with a key domain on the GENERAL signing
    /// surface, keyed on the locally-established <see cref="RequestOrigin"/> (never a
    /// propagated / wire-supplied value). Layered, fail-closed:
    /// <list type="number">
    ///   <item>
    ///     <b>Unestablished-origin deny</b> — an origin that no boundary positively
    ///     established denies with <c>RequestOriginUnestablished</c>. The scoped default
    ///     is <see cref="RequestOrigin.Unestablished"/>, so a context no boundary
    ///     established can never reach an allow branch.
    ///   </item>
    ///   <item>
    ///     <b>Minter-only structural deny</b> — the cluster-signing root
    ///     (<c>jwks-signing</c>) is rejected with <c>MinterCapabilityRequired</c> for
    ///     EVERY established origin; it is reachable only through the dedicated minter
    ///     capability. This kills the confused-deputy: a request that became in-process
    ///     downstream cannot reach the root, and there is no caller id to spoof.
    ///   </item>
    ///   <item>
    ///     <b>Never-signable structural deny</b> — every other member of
    ///     <see cref="NeverCrossProcessSignableDomains"/> (the CA domains) is rejected
    ///     with <c>CrossProcessDomainRejected</c> for EVERY origin, independent of
    ///     policy: a trust anchor's private key signs only certificates through the
    ///     dedicated issuance path, never arbitrary caller-supplied bytes.
    ///   </item>
    ///   <item>
    ///     <b>Plane deny</b> — every non-root domain signs cross-process only; a
    ///     non-<see cref="RequestOrigin.CrossProcessHop"/> origin is denied with
    ///     <c>SigningDomainNotAuthorized</c>.
    ///   </item>
    ///   <item>
    ///     <b>Fail-closed peer</b> — a cross-process hop with no authenticated peer
    ///     identity is denied with <c>Forbidden</c>.
    ///   </item>
    ///   <item>
    ///     <b>Policy deny</b> — the target must be in the caller's allowed-signing-domains
    ///     set; else deny with <c>SigningDomainNotAuthorized</c>.
    ///   </item>
    /// </list>
    /// </summary>
    /// <param name="immediateCaller">
    /// The authenticated caller workload id this hop (the established
    /// <c>IRequestContext.ImmediateCaller</c>, sourced from the validated mTLS peer
    /// certificate on a cross-process hop), or <see langword="null"/> when none is
    /// present (fail-closed).
    /// </param>
    /// <param name="origin">
    /// The locally-established <see cref="RequestOrigin"/> for this hop. The default
    /// <see cref="RequestOrigin.Unestablished"/> fails closed — only a boundary that has
    /// positively established the origin can reach an allow branch.
    /// </param>
    /// <param name="target">The signing key domain the caller wants to sign with.</param>
    /// <param name="allowedSigningDomainsForCaller">
    /// The set of signing-domain wire values the caller is permitted to sign with
    /// (resolved from the signing-domain authority policy; an unknown workload
    /// resolves to the empty set — default-deny).
    /// </param>
    /// <returns>
    /// <c>Ok</c> when the caller may sign with <paramref name="target"/>;
    /// <c>RequestOriginUnestablished</c> (403) for an unestablished origin;
    /// <c>MinterCapabilityRequired</c> (403) for the cluster-signing root on the general
    /// surface; <c>CrossProcessDomainRejected</c> (403) for a certificate-authority
    /// domain (never signable on this surface for any origin);
    /// <c>SigningDomainNotAuthorized</c> (403) for a non-cross-process origin or
    /// a domain outside the caller's allowed set; <c>Forbidden</c> (403) when a
    /// cross-process hop presents no caller identity.
    /// </returns>
    public static D2Result AuthorizeSigning(
        string? immediateCaller,
        RequestOrigin origin,
        KeyDomain target,
        IReadOnlySet<string> allowedSigningDomainsForCaller)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(allowedSigningDomainsForCaller);

        // (1) Fail-closed: an unestablished origin never authorizes signing. The scoped
        // default IRequestContext.Origin is Unestablished, so a context that no boundary
        // established can never reach an allow branch.
        if (origin == RequestOrigin.Unestablished)
            return KeyCustodianFailures.RequestOriginUnestablished();

        // (2) The cluster-signing root is STRUCTURALLY unreachable on the general surface
        // — only the dedicated minter capability (possession-based, wired in the auth
        // module composition) may sign it. Kills the confused-deputy: a request that
        // became in-process downstream cannot reach the root, and there is no caller id
        // to spoof on the in-process plane.
        if (MinterOnlySigningDomains.Contains(target.Value))
            return KeyCustodianFailures.MinterCapabilityRequired();

        // (2b) Every other never-signable domain (the CA trust anchors) is rejected for
        // EVERY origin, independent of policy — a CA private key signs only certificates
        // through the dedicated issuance path, never arbitrary caller-supplied bytes.
        if (NeverCrossProcessSignableDomains.Contains(target.Value))
            return KeyCustodianFailures.CrossProcessDomainRejected();

        // (3) Every other (non-root) domain signs cross-process only.
        if (origin != RequestOrigin.CrossProcessHop)
            return KeyCustodianFailures.SigningDomainNotAuthorized();

        // (4) Cross-process requires an authenticated peer (fail-closed on no cert).
        if (immediateCaller.Falsey())
            return D2Result.Forbidden();

        // (5) Per-workload policy — the target must be in the caller's allowed set (an
        // unknown workload resolves to the empty set — default-deny).
        if (!allowedSigningDomainsForCaller.Contains(target.Value))
            return KeyCustodianFailures.SigningDomainNotAuthorized();

        return D2Result.Ok();
    }

    /// <summary>
    /// Decides whether a caller may fetch a payload domain's encryption keyring on the
    /// general keyring-distribution surface, keyed on the locally-established
    /// <see cref="RequestOrigin"/> (never a propagated / wire-supplied value) plus the
    /// per-workload keyring policy. Layered, fail-closed:
    /// <list type="number">
    ///   <item>
    ///     <b>Unestablished-origin deny</b> — an origin that no boundary positively
    ///     established denies with <c>RequestOriginUnestablished</c> (the fail-closed
    ///     first arm; the scoped default is <see cref="RequestOrigin.Unestablished"/>).
    ///   </item>
    ///   <item>
    ///     <b>Plane deny</b> — the keyring surface serves the cross-process hop and the
    ///     in-process module planes ONLY (the two designed keyring consumers: a
    ///     cross-process backend service authenticated by its mTLS peer id, and the
    ///     in-host module consuming the leaf). Any other established plane
    ///     (<see cref="RequestOrigin.EdgeInbound"/>, <see cref="RequestOrigin.System"/>)
    ///     is denied with <c>KeyringDomainNotAuthorized</c> — the same uniform 403 wire
    ///     code as the policy miss, so a caller cannot probe which domains exist.
    ///   </item>
    ///   <item>
    ///     <b>Fail-closed peer</b> — an authorized plane with no caller identity is denied
    ///     with <c>Forbidden</c>.
    ///   </item>
    ///   <item>
    ///     <b>Policy deny</b> — the target must be in the caller's allowed-keyring-domains
    ///     set; else deny with <c>KeyringDomainNotAuthorized</c>. An unknown caller
    ///     resolves to the empty set (default-deny). This arm is what denies a
    ///     non-payload domain IN PRODUCTION: the boot validator refuses to grant any
    ///     non-payload domain, so no caller can ever hold such a grant, and a non-payload
    ///     domain is therefore never in any allowed set.
    ///   </item>
    /// </list>
    /// A keyring is a full encrypt+decrypt capability for its domain, so there is no
    /// broad "any authenticated caller" arm (unlike seal-encrypt's public-key fetch);
    /// every plane is policy-gated. The crown-jewel non-payload domains
    /// (<c>jwks-signing</c> / <c>cookie</c> / <c>client-secret</c> / the CA domains) are
    /// excluded in layers — the boot validator refuses to grant them, this arm denies
    /// them structurally (never in an allowed set), and the handler's key-type fork is
    /// belt-and-braces.
    /// </summary>
    /// <param name="immediateCaller">
    /// The authenticated caller id this hop — the validated mTLS peer workload id on a
    /// cross-process hop, or the calling module id on an in-process hop (both surfaced as
    /// the established <c>IRequestContext.ImmediateCaller</c>), or <see langword="null"/>
    /// when none is present (fail-closed).
    /// </param>
    /// <param name="origin">
    /// The locally-established <see cref="RequestOrigin"/> for this hop. The default
    /// <see cref="RequestOrigin.Unestablished"/> fails closed.
    /// </param>
    /// <param name="target">The payload key domain whose keyring the caller wants to fetch.</param>
    /// <param name="allowedKeyringDomainsForCaller">
    /// The set of keyring-domain wire values the caller is permitted to fetch (resolved
    /// from the keyring-domain authority policy; an unknown workload resolves to the empty
    /// set — default-deny).
    /// </param>
    /// <returns>
    /// <c>Ok</c> when the caller may fetch <paramref name="target"/>'s keyring;
    /// <c>RequestOriginUnestablished</c> (403) for an unestablished origin;
    /// <c>KeyringDomainNotAuthorized</c> (403) for an unauthorized plane or a domain
    /// outside the caller's allowed set; <c>Forbidden</c> (403) when an authorized plane
    /// presents no caller identity.
    /// </returns>
    public static D2Result AuthorizeKeyringFetch(
        string? immediateCaller,
        RequestOrigin origin,
        KeyDomain target,
        IReadOnlySet<string> allowedKeyringDomainsForCaller)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(allowedKeyringDomainsForCaller);

        // (1) Fail-closed: an unestablished origin never authorizes a keyring fetch. The
        // scoped default IRequestContext.Origin is Unestablished, so a context that no
        // boundary established can never reach an allow branch.
        if (origin == RequestOrigin.Unestablished)
            return KeyCustodianFailures.RequestOriginUnestablished();

        // (2) Plane deny — the keyring surface serves the cross-process hop + the
        // in-process module planes only. Any other established plane (EdgeInbound /
        // System) is denied with the uniform 403 (telemetry distinguishes the plane deny
        // from the policy miss; the wire code stays uniform so no domain-existence oracle).
        if (origin is not (RequestOrigin.CrossProcessHop or RequestOrigin.InProcessModule))
            return KeyCustodianFailures.KeyringDomainNotAuthorized();

        // (3) An authorized plane with no caller identity is fail-closed.
        if (immediateCaller.Falsey())
            return D2Result.Forbidden();

        // (4) Per-workload policy — the target must be in the caller's allowed set (an
        // unknown workload resolves to the empty set — default-deny). In production this
        // is what denies a non-payload domain: no caller can ever hold a non-payload
        // grant (the boot validator refuses it), so it is never in any allowed set.
        if (!allowedKeyringDomainsForCaller.Contains(target.Value))
            return KeyCustodianFailures.KeyringDomainNotAuthorized();

        return D2Result.Ok();
    }

    /// <summary>
    /// Decides whether the dedicated JWT minter capability may sign with the
    /// cluster-signing root. The minter runs IN-PROCESS inside the auth module;
    /// possession of the capability (it is registered ONLY in the auth-module
    /// composition, never in the general client composition) PLUS the in-process-module
    /// plane IS the authority — there is no caller id to read. Fail-closed on any other
    /// origin.
    /// </summary>
    /// <param name="origin">
    /// The locally-established <see cref="RequestOrigin"/> for this hop. Only
    /// <see cref="RequestOrigin.InProcessModule"/> authorizes the minter.
    /// </param>
    /// <returns>
    /// <c>Ok</c> when <paramref name="origin"/> is
    /// <see cref="RequestOrigin.InProcessModule"/>; <c>RequestOriginUnestablished</c>
    /// (403) for an unestablished origin; <c>Forbidden</c> (403) for any other origin.
    /// </returns>
    public static D2Result AuthorizeMinterSigning(RequestOrigin origin)
    {
        // Fail-closed: an unestablished origin never authorizes the minter.
        if (origin == RequestOrigin.Unestablished)
            return KeyCustodianFailures.RequestOriginUnestablished();

        // The minter is an in-process auth-module capability; only the in-process-module
        // plane may reach the cluster-signing root. Possession + plane = authority.
        if (origin != RequestOrigin.InProcessModule)
            return D2Result.Forbidden();

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
