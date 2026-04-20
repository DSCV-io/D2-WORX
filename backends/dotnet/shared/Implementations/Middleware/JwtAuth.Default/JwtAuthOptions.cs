// -----------------------------------------------------------------------
// <copyright file="JwtAuthOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.JwtAuth.Default;

/// <summary>
/// Configuration options for JWT authentication against the Auth Service.
/// </summary>
public class JwtAuthOptions
{
    /// <summary>
    /// Gets or sets the base URL of the Auth Service (e.g., "https://auth.example.com").
    /// Used to construct the JWKS endpoint for key retrieval.
    /// </summary>
    public string AuthServiceBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expected JWT issuer claim.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expected JWT audience claim.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the clock skew tolerance for token lifetime validation.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets how often the gateway proactively re-fetches JWKS keys.
    /// Defaults to 8 hours. Keys rotate every 30 days, so this is conservative.
    /// </summary>
    public TimeSpan JwksAutoRefreshInterval { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Gets or sets the minimum cooldown between forced JWKS refreshes
    /// (triggered when an unknown signing key is encountered during validation).
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan JwksRefreshInterval { get; set; } = TimeSpan.FromMinutes(5);
}
