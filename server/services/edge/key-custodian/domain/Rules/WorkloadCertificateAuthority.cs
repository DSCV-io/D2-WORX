// -----------------------------------------------------------------------
// <copyright file="WorkloadCertificateAuthority.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Rules;

/// <summary>
/// The authority rule over workload leaf-certificate issuance. The COMMITTED body is
/// a fail-closed DENY-ALL skeleton: no caller↔subject binding authority exists yet,
/// so EVERY origin is denied and the issuance handler cannot mint a leaf for anyone.
/// A premature transport wiring therefore denies 100% instead of issuing workload
/// identities to arbitrary callers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure rule, no IO / port / options.</b> Same shape discipline as
/// <see cref="WorkloadCapabilityAuthority"/>: never throws, every arm returns a
/// <see cref="D2Result"/>.
/// </para>
/// <para>
/// <b>Replacement contract (hard gate for the cross-process issuance wiring).</b>
/// The gRPC issuance transport MUST land together with the REAL rule replacing the
/// deny-all arm — never before it. The real rule is fail-closed on a non-cross-process
/// origin and on an absent authenticated peer identity, and binds caller to subject:
/// the requested workload id must equal the mTLS-authenticated caller identity (or
/// route through an explicit, isolated delegated-issuer capability — never the
/// general surface). The wiring step also adds the per-handler
/// <c>ScopeRequirement</c> and the per-mismatch deny tests: a transport scope alone
/// would be an impersonation-issuance oracle, which is exactly what this interim
/// deny-all prevents.
/// </para>
/// </remarks>
public static class WorkloadCertificateAuthority
{
    /// <summary>
    /// Decides whether the current hop may be issued a workload leaf certificate.
    /// Interim fail-closed skeleton:
    /// <list type="number">
    ///   <item>
    ///     <b>Unestablished-origin deny</b> — an origin no boundary positively
    ///     established denies with <c>RequestOriginUnestablished</c> (the type-zero
    ///     explicit FIRST-checked deny).
    ///   </item>
    ///   <item>
    ///     <b>Deny-all</b> — every established origin is <c>Forbidden</c> until the
    ///     real caller↔subject binding rule replaces this arm (see the type-level
    ///     replacement contract).
    ///   </item>
    /// </list>
    /// </summary>
    /// <param name="origin">
    /// The locally-established <see cref="RequestOrigin"/> for this hop. The default
    /// <see cref="RequestOrigin.Unestablished"/> fails closed.
    /// </param>
    /// <returns>
    /// <c>RequestOriginUnestablished</c> (403) for an unestablished origin;
    /// <c>Forbidden</c> (403) for every established origin — there is no allow arm.
    /// </returns>
    public static D2Result AuthorizeIssuance(RequestOrigin origin)
    {
        // (1) Fail-closed: an unestablished origin never authorizes issuance.
        // Checked FIRST so a context no boundary established can never fall
        // through to an allow branch.
        if (origin == RequestOrigin.Unestablished)
            return KeyCustodianFailures.RequestOriginUnestablished();

        // (2) Deny-all: no caller↔subject binding authority exists yet, so every
        // established origin is denied. The cross-process issuance wiring replaces
        // this arm with the real binding rule (see the type-level contract).
        return D2Result.Forbidden();
    }
}
