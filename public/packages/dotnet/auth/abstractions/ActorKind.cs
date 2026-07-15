// -----------------------------------------------------------------------
// <copyright file="ActorKind.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// Discriminator for what an entry in the RFC 8693 actor chain represents — either a
/// service identity (RFC 6749 §4.4 client_credentials propagated through delegation)
/// or a user impersonation step (per <see cref="Impersonation"/>'s sub-flavors in
/// <see cref="ImpersonationKind"/>).
/// </summary>
/// <remarks>
/// Token "kind" is derived from the shape of the act chain:
/// <list type="bullet">
///   <item><description>
///     No <c>act</c> claim → end-user direct token (originates only at the Edge).
///   </description></item>
///   <item><description>
///     Outermost <c>act</c> entry has <see cref="Service"/> Kind → service-on-behalf-of-user
///     (RFC 8693 token-exchange delegation) OR pure service-identity token (when no user subject
///     is present).
///   </description></item>
///   <item><description>
///     Outermost <c>act</c> entry has <see cref="Impersonation"/> Kind → user impersonation;
///     the entry's <c>ImpersonationKind</c> field distinguishes Consent vs Force.
///   </description></item>
/// </list>
/// </remarks>
public enum ActorKind
{
    /// <summary>
    /// Service identity — the actor is an OAuth client (RFC 6749 §4.4 client_credentials).
    /// In a delegation chain (RFC 8693), Service entries indicate that a backend service
    /// minted / re-minted the token while preserving an upstream subject.
    /// </summary>
    Service,

    /// <summary>
    /// User impersonation — the actor is a human agent acting as another user. The
    /// flavor (Consent vs Force) lives on the actor's <c>ImpersonationKind</c> field
    /// and originates from the <c>act.d2_kind</c> JWT claim.
    /// </summary>
    Impersonation,
}
