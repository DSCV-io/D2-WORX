// -----------------------------------------------------------------------
// <copyright file="WorkloadCertificateAuthority.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Rules;

/// <summary>
/// The authority rules over the certificate-authority consumer surface: who may be
/// issued a workload leaf certificate (<see cref="AuthorizeIssuance"/>), and who may
/// fetch the CA chain (<see cref="AuthorizeCaCertificateFetch"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure rules, no IO / port / options.</b> Same shape discipline as
/// <see cref="WorkloadCapabilityAuthority"/>: never throws, every arm returns a
/// <see cref="D2Result"/>. The handler that calls a rule owns the counter / log on
/// a deny.
/// </para>
/// <para>
/// <b>Self-issue is structural, not compared.</b> There is no subject anywhere on
/// the issuance wire: the input is a PKCS#10 certificate-signing request whose
/// subject / SAN / requested extensions are IGNORED, and the handler sets the leaf
/// subject-alternative-name from the authenticated mTLS peer identity
/// (<c>IRequestContext.ImmediateCaller</c>, sourced from the validated peer
/// certificate's SPIFFE SAN). An impersonation request is therefore unrepresentable
/// on this surface — the rule carries no subject parameter and no compare arm.
/// Delegated issuance is NOT an arm here: a dedicated capability would be its own
/// isolated seam (the first-leaf bootstrap design decides whether one exists),
/// never a branch on this general surface.
/// </para>
/// <para>
/// <b>Fail-closed.</b> The workload id is an authenticated PUBLIC service label,
/// not a secret, so the presence checks are plain comparisons (no constant-time
/// requirement) — mirroring <see cref="WorkloadCapabilityAuthority"/>.
/// </para>
/// </remarks>
public static class WorkloadCertificateAuthority
{
    /// <summary>
    /// Decides whether the current hop may be issued a workload leaf certificate.
    /// Layered, fail-closed:
    /// <list type="number">
    ///   <item>
    ///     <b>Unestablished-origin deny</b> — an origin no boundary positively
    ///     established denies with <c>RequestOriginUnestablished</c> (the type-zero
    ///     explicit FIRST-checked deny).
    ///   </item>
    ///   <item>
    ///     <b>Plane deny</b> — issuance is CROSS-PROCESS ONLY. The in-process plane's
    ///     <c>ImmediateCaller</c> is caller-supplied (not transport-authenticated), so
    ///     it can never authorize minting a workload identity; the edge-inbound and
    ///     system planes have no business requesting a leaf. Every
    ///     non-<see cref="RequestOrigin.CrossProcessHop"/> origin is denied with the
    ///     uniform <c>IssuanceNotAuthorized</c> (403).
    ///   </item>
    ///   <item>
    ///     <b>Fail-closed peer</b> — a cross-process hop with no authenticated mTLS
    ///     peer identity is denied with <c>Forbidden</c>.
    ///   </item>
    /// </list>
    /// There is no subject arm: self-issue is enforced by construction (the handler
    /// derives the leaf SAN from the authenticated peer identity and never reads the
    /// CSR's subject), so nothing exists to compare.
    /// </summary>
    /// <param name="immediateCaller">
    /// The authenticated caller workload id this hop (the established
    /// <c>IRequestContext.ImmediateCaller</c>, sourced from the validated mTLS peer
    /// certificate on a cross-process hop), or <see langword="null"/> when none is
    /// present (fail-closed).
    /// </param>
    /// <param name="origin">
    /// The locally-established <see cref="RequestOrigin"/> for this hop — recomputed
    /// by the receiving boundary from its own unforgeable transport facts, never a
    /// propagated wire value. The default <see cref="RequestOrigin.Unestablished"/>
    /// fails closed.
    /// </param>
    /// <returns>
    /// <c>Ok</c> when the authenticated cross-process caller may be issued a leaf;
    /// <c>RequestOriginUnestablished</c> (403) for an unestablished origin;
    /// <c>IssuanceNotAuthorized</c> (403) for every non-cross-process origin;
    /// <c>Forbidden</c> (403) when a cross-process hop presents no caller identity.
    /// </returns>
    public static D2Result AuthorizeIssuance(string? immediateCaller, RequestOrigin origin)
    {
        // (1) Fail-closed: an unestablished origin never authorizes issuance. Checked
        // FIRST so a context no boundary established can never fall through to an
        // allow branch.
        if (origin == RequestOrigin.Unestablished)
            return KeyCustodianFailures.RequestOriginUnestablished();

        // (2) Plane deny: issuance is cross-process only. The in-process plane's
        // ImmediateCaller is caller-supplied (not transport-authenticated), so it can
        // never authorize minting an identity; EdgeInbound / System have no issuance
        // business either.
        if (origin != RequestOrigin.CrossProcessHop)
            return KeyCustodianFailures.IssuanceNotAuthorized();

        // (3) Fail-closed peer: a cross-process hop with no authenticated mTLS peer
        // identity is denied — there is no one to issue to.
        if (immediateCaller.Falsey())
            return D2Result.Forbidden();

        return D2Result.Ok();
    }

    /// <summary>
    /// Decides whether the current hop may fetch the certificate-authority chain
    /// (root + issuing intermediate). Layered, fail-closed:
    /// <list type="number">
    ///   <item>
    ///     <b>Unestablished-origin deny</b> — denies with
    ///     <c>RequestOriginUnestablished</c> (the type-zero FIRST-checked deny).
    ///   </item>
    ///   <item>
    ///     <b>Plane deny</b> — the chain is distributed over already-trusted internal
    ///     channels only: the cross-process hop and the in-process module planes.
    ///     <see cref="RequestOrigin.EdgeInbound"/> would serve the internal trust
    ///     anchor on the public plane for no reason, and System workers reach the CA
    ///     through the CA provider — both deny with
    ///     <c>CaCertificateNotAuthorized</c> (403).
    ///   </item>
    ///   <item>
    ///     <b>Fail-closed identity</b> — a served plane with no caller identity is
    ///     denied with <c>Forbidden</c>.
    ///   </item>
    /// </list>
    /// The material is PUBLIC trust anchor / chain material (presented on the wire in
    /// every TLS handshake), so the authority is broad within the served planes — no
    /// per-workload policy map (over-sharing a public certificate is harmless; a grant
    /// map would add config burden with zero security delta, the same broad rationale
    /// as <see cref="WorkloadCapabilityAuthority.AuthorizeSealEncrypt"/>).
    /// </summary>
    /// <param name="immediateCaller">
    /// The authenticated caller id this hop — the validated mTLS peer workload id on a
    /// cross-process hop, or the calling module id on an in-process hop — or
    /// <see langword="null"/> when none is present (fail-closed).
    /// </param>
    /// <param name="origin">
    /// The locally-established <see cref="RequestOrigin"/> for this hop. The default
    /// <see cref="RequestOrigin.Unestablished"/> fails closed.
    /// </param>
    /// <returns>
    /// <c>Ok</c> when the authenticated caller may fetch the chain;
    /// <c>RequestOriginUnestablished</c> (403) for an unestablished origin;
    /// <c>CaCertificateNotAuthorized</c> (403) for an unserved plane;
    /// <c>Forbidden</c> (403) when a served plane presents no caller identity.
    /// </returns>
    public static D2Result AuthorizeCaCertificateFetch(
        string? immediateCaller, RequestOrigin origin)
    {
        // (1) Fail-closed: an unestablished origin never authorizes a chain fetch.
        if (origin == RequestOrigin.Unestablished)
            return KeyCustodianFailures.RequestOriginUnestablished();

        // (2) Plane deny — the internal trust anchor is distributed over
        // already-trusted internal channels only (cross-process + in-process module);
        // it has no business on the public EdgeInbound plane, and System workers have
        // the CA provider.
        if (origin is not (RequestOrigin.CrossProcessHop or RequestOrigin.InProcessModule))
            return KeyCustodianFailures.CaCertificateNotAuthorized();

        // (3) A served plane with no caller identity is fail-closed.
        if (immediateCaller.Falsey())
            return D2Result.Forbidden();

        return D2Result.Ok();
    }
}
