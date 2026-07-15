// -----------------------------------------------------------------------
// <copyright file="JwtValidatorOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Validation;

/// <summary>
/// JWT-validator-specific knobs on <see cref="AuthOptions"/>. Composed under
/// <c>AuthOptions.Validator</c>; not configured directly by callers.
/// </summary>
/// <remarks>
/// <para>
/// Use the parameterless ctor for all defaults; use the parameterized ctor
/// (positional or named args) to override one or more values without an
/// object initializer. The <c>with</c>-expression also works for record-style
/// selective overrides.
/// </para>
/// <para>
/// Top-level <see cref="AuthOptions.Issuer"/> / <see cref="AuthOptions.Audience"/> /
/// <see cref="AuthOptions.ClockSkew"/> are NOT shadowed here — they live on the
/// parent options because they're shared with the JWKS provider and because
/// per-validator overrides would create a footgun (e.g. silently lowering
/// audience-validation rigor on one path).
/// </para>
/// </remarks>
public sealed record JwtValidatorOptions
{
    /// <summary>
    /// Default <see cref="ValidAlgorithms"/> — RS256 only. Internal so consumers
    /// don't reference it directly.
    /// </summary>
    /// <remarks>
    /// Pinning the allowed algorithms list defends against
    /// <c>alg=none</c> and HMAC-with-public-key confusion attacks. Do NOT widen
    /// to include HS256 / HS384 / HS512 unless you have a specific symmetric-key
    /// flow that owns its own key material — never share an RSA verify key
    /// between asymmetric and symmetric validation paths.
    /// </remarks>
    internal static readonly IReadOnlyList<string> SR_DefaultValidAlgorithms = ["RS256"];

    /// <summary>
    /// Initializes a new <see cref="JwtValidatorOptions"/>. Each parameter is
    /// nullable; passing <c>null</c> (or omitting the argument) yields the
    /// documented default for that property. Use <c>new()</c> for all defaults.
    /// </summary>
    /// <param name="requireSessionIdClaim">
    /// Override for <see cref="RequireSessionIdClaim"/>; <c>null</c> = default
    /// <see langword="true"/>.
    /// </param>
    /// <param name="requireExpirationTime">
    /// Override for <see cref="RequireExpirationTime"/>; <c>null</c> = default
    /// <see langword="true"/>.
    /// </param>
    /// <param name="validAlgorithms">
    /// Override for <see cref="ValidAlgorithms"/>; <c>null</c> = default
    /// <c>["RS256"]</c>. Empty / whitespace-only entries are rejected at host
    /// build time via <c>ValidateOnStart</c> (see <c>AddD2Auth</c>).
    /// </param>
    public JwtValidatorOptions(
        bool? requireSessionIdClaim = null,
        bool? requireExpirationTime = null,
        IReadOnlyList<string>? validAlgorithms = null)
    {
        RequireSessionIdClaim = requireSessionIdClaim ?? true;
        RequireExpirationTime = requireExpirationTime ?? true;
        ValidAlgorithms = validAlgorithms ?? SR_DefaultValidAlgorithms;
    }

    /// <summary>
    /// Gets a value indicating whether JWTs missing the <c>d2_session_id</c>
    /// claim are rejected. Default <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Defense-in-depth: the session liveness check (transport-layer middleware /
    /// interceptor) needs the claim to perform its lookup. Surfacing the
    /// failure here gives operators the granular <c>claim_missing</c> outcome
    /// tag instead of a downstream parse error. Set to <see langword="false"/>
    /// only for service-identity-only flows (RFC 6749 §4.4 client_credentials)
    /// that don't carry a user session.
    /// </remarks>
    public bool RequireSessionIdClaim { get; init; }

    /// <summary>
    /// Gets a value indicating whether JWTs missing the standard <c>exp</c>
    /// claim are rejected. Default <see langword="true"/>. Mirrors the
    /// Microsoft.IdentityModel default — declared explicitly so the contract
    /// is doc-complete and survives library default changes.
    /// </summary>
    public bool RequireExpirationTime { get; init; }

    /// <summary>
    /// Gets the allowlist of accepted JWS <c>alg</c> header values. Default
    /// <c>["RS256"]</c>.
    /// </summary>
    /// <remarks>
    /// Pinning the algorithm list defends against two well-known confusion
    /// attacks: (a) <c>alg=none</c> — a token with no signature whatsoever
    /// that some validators accept by default; (b) HMAC-with-public-key —
    /// an HS256-signed token where the attacker uses the issuer's
    /// known-public RSA key as the HMAC secret. Both fail when the validator
    /// rejects any algorithm not explicitly listed. RS256 is the platform
    /// canonical signing algorithm; widen this list ONLY for service-specific
    /// asymmetric flows (e.g. RS384 / ES256). Empty / whitespace-only entries
    /// are rejected at host build time via <c>ValidateOnStart</c>.
    /// </remarks>
    public IReadOnlyList<string> ValidAlgorithms { get; init; }
}
