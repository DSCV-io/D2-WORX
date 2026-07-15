// -----------------------------------------------------------------------
// <copyright file="AuthOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth;

using D2.Shared.Auth.Jwks;
using D2.Shared.Auth.Sessions;
using D2.Shared.Auth.Validation;

/// <summary>
/// Configuration for the inbound auth runtime. Bound from
/// <c>IConfiguration</c> via the standard options pattern in
/// <see cref="AuthServiceCollectionExtensions.AddD2Auth"/>.
/// </summary>
/// <remarks>
/// Sub-component options (JWKS-specific, session-liveness-specific,
/// middleware-specific) compose into this root via nested record properties,
/// so callers only configure one root section.
/// </remarks>
public sealed record AuthOptions
{
    /// <summary>
    /// Gets or sets the OIDC issuer URL — the base URL whose
    /// <c>/.well-known/openid-configuration</c> endpoint publishes the
    /// JWKS endpoint and other discovery metadata. Required at startup
    /// (validated at host build time via <c>ValidateOnStart</c>).
    /// </summary>
    /// <remarks>
    /// Must be an HTTPS URL in production (HTTP rejected at composition
    /// time). Example: <c>https://edge.internal</c>. Settable so the
    /// <c>AddD2Auth(Action&lt;AuthOptions&gt;)</c> configure lambda can
    /// populate it after the options instance is constructed by the DI
    /// container.
    /// </remarks>
    public Uri? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the audience this service expects to find in JWT
    /// <c>aud</c> claims. Use one of the
    /// <c>D2.Shared.Auth.Abstractions.Audiences</c> codegen constants.
    /// Required at startup (validated at host build time via
    /// <c>ValidateOnStart</c>).
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Gets the clock skew tolerance applied to JWT <c>exp</c> / <c>nbf</c>
    /// checks. Default 30 seconds — matches the Microsoft.IdentityModel
    /// default and accommodates typical NTP drift.
    /// </summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets JWKS-specific options. Defaults are sensible — most
    /// callers don't override. Settable so the
    /// <c>AddD2Auth(Action&lt;AuthOptions&gt;)</c> configure lambda can swap
    /// the nested record (e.g. <c>opts.Jwks = opts.Jwks with { ... }</c>)
    /// after the options instance is constructed by the DI container.
    /// </summary>
    public JwksProviderOptions Jwks { get; set; } = new();

    /// <summary>
    /// Gets or sets session-liveness-specific options. Defaults are sensible
    /// — most callers don't override. Settable for the same reason as
    /// <see cref="Jwks"/>.
    /// </summary>
    public SessionLivenessOptions Sessions { get; set; } = new();

    /// <summary>
    /// Gets or sets JWT-validator-specific options. Defaults are sensible —
    /// RS256-only, claim + expiration required. Settable for the same reason
    /// as <see cref="Jwks"/>.
    /// </summary>
    public JwtValidatorOptions Validator { get; set; } = new();
}
