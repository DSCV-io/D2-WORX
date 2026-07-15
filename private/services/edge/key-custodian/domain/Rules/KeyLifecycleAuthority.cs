// -----------------------------------------------------------------------
// <copyright file="KeyLifecycleAuthority.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Rules;

/// <summary>
/// The pure authority rule over every destructive key-lifecycle mutation
/// (generate / activate / rotate / retire / compromise / run-due-rotations /
/// seed-certificate-authority): the ONLY plane allowed to mutate the key
/// state machine is the in-host System plane — the background workers that
/// establish <see cref="RequestOrigin.System"/> under the host's own service
/// identity. Every other plane is denied fail-closed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure rule, no IO / port / options.</b> Same shape discipline as
/// <see cref="WorkloadCapabilityAuthority"/>: no DB, no <c>IOptions</c>, no logging,
/// never throws — every arm returns a <see cref="D2Result"/> (allow = <c>Ok</c>,
/// deny = a typed failure). The calling handler owns the counter / log on a deny.
/// </para>
/// <para>
/// <b>Why System-only.</b> Rotation is initiated by the in-host System scheduler;
/// other services only CONSUME its outputs (keyring / JWKS / CA fetch + the
/// rotation-event fanout) — they never initiate a state-machine mutation. No
/// lifecycle op is on any transport contract, and the System plane deliberately
/// carries NO scopes (scopes come from validated tokens), so the origin gate — not a
/// <c>ScopeRequirement</c> — is the correct, stronger control for a System-only op.
/// </para>
/// <para>
/// <b>Consciously-extended, never widened by default.</b> A future admin transport
/// (the operator compromise-key action is the standing candidate) is FORCED by the
/// deny arm to extend this rule explicitly — and must add its own per-op scope with
/// that transport. Until then every non-System established plane (edge-inbound,
/// cross-process, in-process module) is <c>Forbidden</c>.
/// </para>
/// </remarks>
public static class KeyLifecycleAuthority
{
    /// <summary>
    /// Decides whether the current hop may execute a destructive key-lifecycle
    /// mutation, keyed on the locally-established <see cref="RequestOrigin"/>
    /// (never a propagated / wire-supplied value). Layered, fail-closed:
    /// <list type="number">
    ///   <item>
    ///     <b>Unestablished-origin deny</b> — an origin no boundary positively
    ///     established denies with <c>RequestOriginUnestablished</c> (the type-zero
    ///     explicit FIRST-checked deny; the scoped default fails closed).
    ///   </item>
    ///   <item><b>System allow</b> — the in-host System worker plane is authorized.</item>
    ///   <item>
    ///     <b>Everything else deny</b> — every other established plane is
    ///     <c>Forbidden</c>; there is no cross-process or user-plane lifecycle
    ///     authority, and a future admin transport must consciously extend this rule.
    ///   </item>
    /// </list>
    /// </summary>
    /// <param name="origin">
    /// The locally-established <see cref="RequestOrigin"/> for this hop. The default
    /// <see cref="RequestOrigin.Unestablished"/> fails closed.
    /// </param>
    /// <returns>
    /// <c>Ok</c> for <see cref="RequestOrigin.System"/>;
    /// <c>RequestOriginUnestablished</c> (403) for an unestablished origin;
    /// <c>Forbidden</c> (403) for every other established origin.
    /// </returns>
    public static D2Result AuthorizeLifecycleMutation(RequestOrigin origin)
    {
        // (1) Fail-closed: an unestablished origin never authorizes a lifecycle
        // mutation. Checked FIRST so a context no boundary established can never
        // fall through to an allow branch.
        if (origin == RequestOrigin.Unestablished)
            return KeyCustodianFailures.RequestOriginUnestablished();

        // (2) The in-host System plane is the only legitimate lifecycle driver.
        if (origin == RequestOrigin.System)
            return D2Result.Ok();

        // (3) Every other established plane (EdgeInbound / CrossProcessHop /
        // InProcessModule) is denied — a future admin transport must consciously
        // extend this rule (and add its own per-op scope) to gain an allow arm.
        return D2Result.Forbidden();
    }
}
