// -----------------------------------------------------------------------
// <copyright file="AuthErrorCodes.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Errors;

/// <summary>
/// Machine-readable <c>d2_error_code</c> string constants surfaced on
/// <see cref="D2.Shared.Result.D2Result"/> failures + RFC 7807
/// <c>ProblemDetails.Extensions["d2_error_code"]</c>.
/// </summary>
/// <remarks>
/// <para>
/// Granular by design — every distinct failure mode gets its own code so
/// dashboards / SIEM rules / client-side retry logic can branch on it.
/// User-facing messages are intentionally COARSE (just two TK keys —
/// <c>auth_unauthorized</c> / <c>auth_session_unavailable</c>) since
/// telling an attacker which check failed is an info leak.
/// </para>
/// <para>
/// Constants stay in one file (rather than scattered across the validators
/// they originate in) so the full failure taxonomy is reviewable in one
/// glance. Adding a new code = adding a constant here + a counter tag in
/// <see cref="Telemetry.AuthTelemetry.ProblemEmitted"/>'s allowed set.
/// </para>
/// </remarks>
public static class AuthErrorCodes
{
    /// <summary>The Authorization header was missing on a protected endpoint.</summary>
    public const string AUTH_BEARER_MISSING = "AUTH_BEARER_MISSING";

    /// <summary>The Authorization header was present but not a parseable Bearer JWT.</summary>
    public const string AUTH_BEARER_MALFORMED = "AUTH_BEARER_MALFORMED";

    /// <summary>JWT signature verification failed against the JWKS.</summary>
    public const string AUTH_JWT_SIGNATURE_INVALID = "AUTH_JWT_SIGNATURE_INVALID";

    /// <summary>JWT is expired (<c>exp</c> in the past, beyond clock skew).</summary>
    public const string AUTH_JWT_EXPIRED = "AUTH_JWT_EXPIRED";

    /// <summary>JWT not yet valid (<c>nbf</c> in the future, beyond clock skew).</summary>
    public const string AUTH_JWT_NOT_YET_VALID = "AUTH_JWT_NOT_YET_VALID";

    /// <summary>JWT <c>iss</c> claim does not match the configured issuer.</summary>
    public const string AUTH_JWT_ISSUER_MISMATCH = "AUTH_JWT_ISSUER_MISMATCH";

    /// <summary>JWT <c>aud</c> claim does not match this service's configured audience.</summary>
    public const string AUTH_JWT_AUDIENCE_MISMATCH = "AUTH_JWT_AUDIENCE_MISMATCH";

    /// <summary>JWT is missing a required claim that this service depends on.</summary>
    public const string AUTH_JWT_CLAIM_MISSING = "AUTH_JWT_CLAIM_MISSING";

    /// <summary>JWT <c>act</c> chain is malformed (RFC 8693 §2.1 violation).</summary>
    public const string AUTH_JWT_ACT_CHAIN_MALFORMED = "AUTH_JWT_ACT_CHAIN_MALFORMED";

    /// <summary>
    /// JWT signed by an unknown <c>kid</c>; reactive JWKS refresh did not surface it.
    /// </summary>
    public const string AUTH_JWT_KID_NOT_FOUND = "AUTH_JWT_KID_NOT_FOUND";

    /// <summary>JWKS upstream is unavailable; no cached snapshot to fall back on.</summary>
    public const string AUTH_JWKS_UNAVAILABLE = "AUTH_JWKS_UNAVAILABLE";

    /// <summary>The bearer's <c>d2_session_id</c> is no longer alive (revoked).</summary>
    public const string AUTH_SESSION_REVOKED = "AUTH_SESSION_REVOKED";

    /// <summary>
    /// Session liveness store unreachable. Caller fails closed: receives a
    /// 503-equivalent and may retry; the attempt is NOT treated as authenticated.
    /// Treating an unknown liveness state as alive would let revoked sessions
    /// ride through outages.
    /// </summary>
    public const string AUTH_SESSION_LIVENESS_UNAVAILABLE = "AUTH_SESSION_LIVENESS_UNAVAILABLE";

    /// <summary>
    /// Caller is authenticated but the per-endpoint scope requirement is not
    /// satisfied. Surfaces as 401 (not 403) — the auth boundary keeps a
    /// uniform shape regardless of whether the JWT was bad or scopes were
    /// insufficient, so attackers can't deduce which check failed.
    /// </summary>
    public const string AUTH_SCOPE_INSUFFICIENT = "AUTH_SCOPE_INSUFFICIENT";
}
