// -----------------------------------------------------------------------
// <copyright file="WellKnownAudiences.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Abstractions;

/// <summary>
/// Hand-declared well-known JWT <c>aud</c> audiences that are NOT spec-derived
/// (distinct from the spec-generated <see cref="Audiences"/>). These are protocol
/// constants, not token-exchange targets, so they live in code rather than in
/// <c>contracts/auth-audiences/audiences.spec.json</c>.
/// </summary>
public static class WellKnownAudiences
{
    /// <summary>
    /// The single broad internal audience every internal service accepts. The Edge
    /// mints exactly one internal transaction-token with <c>aud</c> set to this
    /// value, and every hop validates <c>aud == </c> this value — the universal
    /// <i>receive</i> audience that makes forward-unchanged possible.
    /// <para>
    /// It is deliberately not an <see cref="Audiences"/> spec entry: those entries
    /// are token-exchange <i>targets</i> (a call exchanges <i>to</i> one of them),
    /// whereas this is the universal <i>receive</i> audience and is never an
    /// exchange target. That is why it is a hand-declared constant here and not a
    /// spec-derived <c>Audiences</c> member.
    /// </para>
    /// </summary>
    public const string D2_INTERNAL_AUDIENCE = "d2.internal";
}
