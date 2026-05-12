// -----------------------------------------------------------------------
// <copyright file="AuthFailures.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Errors;

using D2.Shared.I18n;
using D2.Shared.Result;

/// <summary>
/// Pre-built <see cref="D2Result"/> failures for inbound auth runtime —
/// JWT validation rejections, session liveness outages, JWKS upstream
/// failures.
/// </summary>
/// <remarks>
/// <para>
/// Two-axis design:
/// </para>
/// <list type="bullet">
/// <item><strong>User-facing message</strong> — coarse on purpose. Two TK keys total
///   (<c>auth_errors_UNAUTHORIZED</c>, <c>auth_errors_TEMPORARILY_UNAVAILABLE</c>) so
///   we don't tell attackers which validation step failed.</item>
/// <item><strong>Machine-readable code</strong> — granular. One <see cref="AuthErrorCodes"/>
///   constant per failure mode for dashboards / SIEM rules / client-side retry. Surfaced
///   as the <c>d2_error_code</c> on RFC 7807 ProblemDetails by the auth middleware.</item>
/// </list>
/// <para>
/// Helpers return Unauthorized / ServiceUnavailable / Forbidden D2Results
/// using the semantic factories. Caller code (middleware / interceptor /
/// validator) just picks the right helper — no manual <c>Fail</c> with hand-built
/// status codes.
/// </para>
/// </remarks>
public static class AuthFailures
{
    /// <summary>Bearer token missing on a protected endpoint → 401.</summary>
    public static D2Result BearerMissing() => Unauthorized(AuthErrorCodes.AUTH_BEARER_MISSING);

    /// <summary>Bearer present but malformed (not a parseable JWT) → 401.</summary>
    public static D2Result BearerMalformed() => Unauthorized(AuthErrorCodes.AUTH_BEARER_MALFORMED);

    /// <summary>JWT signature verification failed → 401.</summary>
    public static D2Result JwtSignatureInvalid()
        => Unauthorized(AuthErrorCodes.AUTH_JWT_SIGNATURE_INVALID);

    /// <summary>JWT expired (<c>exp</c> in the past, beyond clock skew) → 401.</summary>
    public static D2Result JwtExpired() => Unauthorized(AuthErrorCodes.AUTH_JWT_EXPIRED);

    /// <summary>JWT not yet valid (<c>nbf</c> in the future, beyond clock skew) → 401.</summary>
    public static D2Result JwtNotYetValid() => Unauthorized(AuthErrorCodes.AUTH_JWT_NOT_YET_VALID);

    /// <summary>JWT <c>iss</c> mismatch → 401.</summary>
    public static D2Result JwtIssuerMismatch()
        => Unauthorized(AuthErrorCodes.AUTH_JWT_ISSUER_MISMATCH);

    /// <summary>JWT <c>aud</c> mismatch (this service is not in the audience list) → 401.</summary>
    public static D2Result JwtAudienceMismatch()
        => Unauthorized(AuthErrorCodes.AUTH_JWT_AUDIENCE_MISMATCH);

    /// <summary>JWT missing a required claim this service needs → 401.</summary>
    public static D2Result JwtClaimMissing() => Unauthorized(AuthErrorCodes.AUTH_JWT_CLAIM_MISSING);

    /// <summary>JWT <c>act</c> chain malformed (RFC 8693 §2.1 violation) → 401.</summary>
    public static D2Result JwtActChainMalformed()
        => Unauthorized(AuthErrorCodes.AUTH_JWT_ACT_CHAIN_MALFORMED);

    /// <summary>JWT signed by an unknown <c>kid</c> after reactive JWKS refresh → 401.</summary>
    public static D2Result JwtKidNotFound() => Unauthorized(AuthErrorCodes.AUTH_JWT_KID_NOT_FOUND);

    /// <summary>JWKS upstream unavailable → 503 (retry-friendly).</summary>
    public static D2Result JwksUnavailable() =>
        D2Result.ServiceUnavailable(
            messages: [TK.Auth.Errors.TEMPORARILY_UNAVAILABLE],
            errorCode: AuthErrorCodes.AUTH_JWKS_UNAVAILABLE);

    /// <summary>JWKS upstream unavailable, typed → 503 (retry-friendly).</summary>
    /// <typeparam name="T">Payload type the caller would have returned on success.</typeparam>
    public static D2Result<T> JwksUnavailable<T>() =>
        D2Result<T>.ServiceUnavailable(
            messages: [TK.Auth.Errors.TEMPORARILY_UNAVAILABLE],
            errorCode: AuthErrorCodes.AUTH_JWKS_UNAVAILABLE);

    /// <summary>Session is revoked (cache miss confirms revocation) → 401.</summary>
    public static D2Result SessionRevoked() => Unauthorized(AuthErrorCodes.AUTH_SESSION_REVOKED);

    /// <summary>
    /// Caller is authenticated but lacks any scope from the per-endpoint
    /// required set → 401 (not 403; see <see cref="AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT"/>
    /// remarks for the rationale).
    /// </summary>
    public static D2Result ScopeInsufficient() =>
        Unauthorized(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);

    /// <summary>
    /// Session liveness store unreachable (fail-closed) → 503 (retry-friendly).
    /// </summary>
    public static D2Result SessionLivenessUnavailable() =>
        D2Result.ServiceUnavailable(
            messages: [TK.Auth.Errors.TEMPORARILY_UNAVAILABLE],
            errorCode: AuthErrorCodes.AUTH_SESSION_LIVENESS_UNAVAILABLE);

    /// <summary>
    /// Session liveness store unreachable (fail-closed), typed → 503 (retry-friendly).
    /// </summary>
    /// <typeparam name="T">Payload type the caller would have returned on success.</typeparam>
    public static D2Result<T> SessionLivenessUnavailable<T>() =>
        D2Result<T>.ServiceUnavailable(
            messages: [TK.Auth.Errors.TEMPORARILY_UNAVAILABLE],
            errorCode: AuthErrorCodes.AUTH_SESSION_LIVENESS_UNAVAILABLE);

    private static D2Result Unauthorized(string errorCode) =>
        D2Result.Unauthorized(
            messages: [TK.Auth.Errors.UNAUTHORIZED],
            errorCode: errorCode);
}
