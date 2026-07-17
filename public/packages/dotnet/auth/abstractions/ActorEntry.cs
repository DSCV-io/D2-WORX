// -----------------------------------------------------------------------
// <copyright file="ActorEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// One link in the RFC 8693 actor chain. Identifies a service or user that
/// participated in producing the current token via token exchange / impersonation.
/// </summary>
/// <param name="Kind">
/// Whether this actor is a service identity or a user impersonation step. The
/// <see cref="ImpersonationKind"/>, <see cref="SessionId"/>, and the four
/// <c>Org*</c> fields below are only meaningful when <paramref name="Kind"/>
/// is <see cref="ActorKind.Impersonation"/>.
/// </param>
/// <param name="Subject">
/// The actor's identifier (the <c>act.sub</c> claim). For <see cref="ActorKind.Service"/>
/// this is the service's OAuth <c>client_id</c> per RFC 6749 §4.4. For
/// <see cref="ActorKind.Impersonation"/> this is the agent (impersonator) user id.
/// </param>
/// <param name="ClientId">
/// Optional service client identifier when <paramref name="Kind"/> is
/// <see cref="ActorKind.Service"/>. Often equals <paramref name="Subject"/> for service
/// actors but may differ if the deployed service identifier differs from the registered
/// OAuth client identifier.
/// </param>
/// <param name="ImpersonationKind">
/// When <paramref name="Kind"/> is <see cref="ActorKind.Impersonation"/>: which flavor
/// of impersonation — <see cref="Auth.Abstractions.ImpersonationKind.Consent"/>
/// (OTP-authorized) or <see cref="Auth.Abstractions.ImpersonationKind.Force"/> (silent,
/// admin-only). Sourced from the actor's <c>act.d2_kind</c> claim. Null for
/// <see cref="ActorKind.Service"/> entries.
/// </param>
/// <param name="SessionId">
/// When <paramref name="Kind"/> is <see cref="ActorKind.Impersonation"/>: the impersonation
/// session identifier (<c>act.d2_session_id</c> claim). Distinct from the user's own
/// session id on <c>IAuthContext.SessionId</c>. Null for <see cref="ActorKind.Service"/>
/// entries.
/// </param>
/// <param name="OrgId">
/// When <paramref name="Kind"/> is <see cref="ActorKind.Impersonation"/>: the agent's
/// (impersonator's) own organization id (<c>act.d2_org_id</c> claim). Useful for audit
/// — "Alice from <i>Customer Support</i> impersonated Bob" — and for additional authz
/// rules that key on the agent's home org. Null for <see cref="ActorKind.Service"/>
/// entries.
/// </param>
/// <param name="OrgName">
/// When <paramref name="Kind"/> is <see cref="ActorKind.Impersonation"/>: the agent's own
/// organization display name (<c>act.d2_org_name</c> claim). Null for
/// <see cref="ActorKind.Service"/> entries.
/// </param>
/// <param name="OrgType">
/// When <paramref name="Kind"/> is <see cref="ActorKind.Impersonation"/>: the agent's own
/// organization type (<c>act.d2_org_type</c> claim). Null for
/// <see cref="ActorKind.Service"/> entries.
/// </param>
/// <param name="OrgRole">
/// When <paramref name="Kind"/> is <see cref="ActorKind.Impersonation"/>: the agent's
/// role in their own organization (<c>act.d2_org_role</c> claim). Null for
/// <see cref="ActorKind.Service"/> entries.
/// </param>
/// <param name="Act">
/// Optional nested actor entry — RFC 8693 §2.1 allows <c>act</c> claims to nest
/// (delegation-of-delegation chains). When present, this represents the actor that
/// minted the token <i>this</i> entry's actor consumed before re-delegating onward.
/// </param>
/// <remarks>
/// On the wire (JWT and AMQP headers) the <c>act</c> chain is the RFC 8693 nested-object
/// shape: <c>{ "sub": "...", "act": { "sub": "...", "act": { ... } } }</c>. In code, the
/// outermost chain is exposed on <c>IAuthContext.ActorChain</c> as
/// <c>IReadOnlyList&lt;ActorEntry&gt;</c> for ergonomic enumeration; each entry's
/// <see cref="Act"/> field walks deeper if needed.
/// </remarks>
public sealed record ActorEntry(
    ActorKind Kind,
    string Subject,
    string? ClientId = null,
    ImpersonationKind? ImpersonationKind = null,
    Guid? SessionId = null,
    Guid? OrgId = null,
    string? OrgName = null,
    OrgType? OrgType = null,
    Role? OrgRole = null,
    ActorEntry? Act = null);
