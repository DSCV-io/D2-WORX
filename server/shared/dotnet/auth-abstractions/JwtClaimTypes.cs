// -----------------------------------------------------------------------
// <copyright file="JwtClaimTypes.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Abstractions;

/// <summary>
/// JWT claim name string constants used across the platform. Standard OAuth /
/// OIDC claims keep their canonical names (<c>sub</c>, <c>aud</c>, <c>scope</c>,
/// <c>act</c>, etc.); D²-specific claims are namespaced with the <c>d2_</c>
/// prefix to avoid collisions with future spec additions.
/// </summary>
/// <remarks>
/// Impersonation metadata lives <b>inside</b> the <c>act</c> chain (e.g.
/// <c>act["d2_kind"] = "consent"</c>); the chain's shape determines token kind.
/// Mirror this list when adding the equivalent constants for cross-language consumption
/// (Node SvelteKit BFF) — share a generated source where possible, or lock the
/// .NET ↔ TS mirroring via a parity test.
/// </remarks>
public static class JwtClaimTypes
{
    // ---- Standard OAuth / OIDC claims ----

    /// <summary>
    /// Subject — the user's identifier (or service client_id for pure service-identity tokens).
    /// </summary>
    public const string SUB = "sub";

    /// <summary>Audience — the intended recipient service identifier.</summary>
    public const string AUD = "aud";

    /// <summary>Issued-at — Unix timestamp seconds when the token was minted.</summary>
    public const string IAT = "iat";

    /// <summary>Expiry — Unix timestamp seconds after which the token is invalid.</summary>
    public const string EXP = "exp";

    /// <summary>
    /// Authorized party (RFC 7519 §4.1.7) — the OAuth client the token was issued for.
    /// </summary>
    public const string AZP = "azp";

    /// <summary>
    /// Scope — space-separated string of granted permissions per RFC 6749 §3.3.
    /// Some authorization servers send this as a JSON array; the runtime parser
    /// accepts both shapes.
    /// </summary>
    public const string SCOPE = "scope";

    /// <summary>
    /// Actor chain — RFC 8693 §2.1 nested-object structure identifying the chain
    /// of services / impersonators that minted the token. Empty / absent when the
    /// token is an end-user-direct token.
    /// </summary>
    public const string ACT = "act";

    /// <summary>
    /// OAuth client identifier of the client that requested THIS specific token
    /// from the authorization server (RFC 8693 §4.3 / RFC 9068 §2.2). Updates
    /// on every token exchange — for a multi-hop chain, this is the immediate
    /// requesting client, not the originating one. Useful for audit ("this
    /// token was minted at the request of X").
    /// </summary>
    public const string CLIENT_ID = "client_id";

    // ---- D²-specific top-level claims (d2_ prefix) ----

    /// <summary>
    /// User session identifier — links the token to a session record in auth_db.
    /// </summary>
    public const string SESSION_ID = "d2_session_id";

    /// <summary>Login handle — unique, lowercase username.</summary>
    public const string USERNAME = "d2_username";

    /// <summary>
    /// JWT fingerprint — SHA-256 of canonical request signal (User-Agent + Accept).
    /// </summary>
    public const string FINGERPRINT = "d2_fp";

    // ---- D²-specific organization claims (single org context — no agent / target split) ----

    /// <summary>
    /// Organization identifier. During impersonation: the impersonated user's org.
    /// </summary>
    public const string ORG_ID = "d2_org_id";

    /// <summary>Organization display name.</summary>
    public const string ORG_NAME = "d2_org_name";

    /// <summary>Organization type — string form of <see cref="OrgType"/>.</summary>
    public const string ORG_TYPE = "d2_org_type";

    /// <summary>User's role in the organization — string form of <see cref="Role"/>.</summary>
    public const string ORG_ROLE = "d2_org_role";

    // ---- Inside-act claim names (RFC 8693 §2.1 — these live INSIDE the act object) ----

    /// <summary>
    /// Inside-<c>act</c> claim — flavor of impersonation when the actor's kind is
    /// impersonation. Values are lowercase strings matching <see cref="ImpersonationKind"/>
    /// (<c>"consent"</c> or <c>"force"</c>).
    /// </summary>
    /// <remarks>
    /// Lookup path: <c>act.d2_kind</c>. There is NO top-level <c>d2_kind</c> claim — token
    /// kind is derived from the act chain's shape (see <see cref="ActorKind"/> remarks).
    /// </remarks>
    public const string ACT_KIND = "d2_kind";

    /// <summary>
    /// Inside-<c>act</c> claim — impersonation session identifier when the actor's kind is
    /// impersonation. Lookup path: <c>act.d2_session_id</c>.
    /// </summary>
    public const string ACT_SESSION_ID = "d2_session_id";
}
