// -----------------------------------------------------------------------
// <copyright file="AuthOutboundOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound;

/// <summary>
/// Configuration for <c>D2.Shared.Auth.Outbound</c>'s service-identity +
/// token-exchange clients. The single <c>D2_AUTH_ISSUER</c> URL drives OIDC
/// discovery; <c>token_endpoint</c> is read from
/// <c>{Issuer}/.well-known/openid-configuration</c> at startup.
/// </summary>
public sealed class AuthOutboundOptions
{
    /// <summary>
    /// Gets or sets the OIDC issuer URL (e.g. <c>https://edge.internal</c>).
    /// The lib fetches <c>{Issuer}/.well-known/openid-configuration</c> via
    /// <c>ConfigurationManager&lt;OpenIdConnectConfiguration&gt;</c> and
    /// reads <c>token_endpoint</c> from there for both
    /// <c>client_credentials</c> (service identity) and
    /// <c>token-exchange</c> (RFC 8693) requests. Maps to env var
    /// <c>D2_AUTH_ISSUER</c>.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets this service's OAuth client identifier registered at
    /// Edge. Used as the username in HTTP Basic auth for the
    /// <c>client_credentials</c> grant. Maps to env var
    /// <c>D2_AUTH_CLIENT_ID</c>.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets this service's OAuth client secret. Used as the password
    /// in HTTP Basic auth for the <c>client_credentials</c> grant. Mounted
    /// via Docker secret in production; env var in dev. Maps to env var
    /// <c>D2_AUTH_CLIENT_SECRET</c>. NEVER logged.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how far in advance of token expiry the
    /// <c>ServiceIdentityRefreshHostedService</c> proactively refreshes the
    /// cached service-identity token. Default 60 s. Set lower for
    /// short-TTL test scenarios; higher to reduce refresh churn at the
    /// cost of a wider not-yet-expired-but-stale window.
    /// </summary>
    public TimeSpan ServiceIdentityRefreshLeadTime { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Gets or sets the per-request timeout applied to outbound HTTP calls
    /// to Edge's <c>token_endpoint</c>. Default 5 s. Auth requests are
    /// blocking on the request hot path, so generous timeouts mask Edge
    /// degradation rather than surfacing it.
    /// </summary>
    public TimeSpan HttpRequestTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the cache-key prefix the
    /// <c>TokenExchangeCache</c> applies when writing through the shared
    /// <c>ILocalCache</c> singleton. Default <c>"tokenexchange:"</c>.
    /// Override only if a cache-key collision arises in a host that
    /// already uses the same prefix.
    /// </summary>
    public string TokenExchangeCacheKeyPrefix { get; set; } = "tokenexchange:";

    /// <summary>
    /// Gets or sets the TTL applied to cached token-exchange entries when
    /// the response's own <c>expires_in</c> is missing or unparseable.
    /// Default 5 min — matches the short-lived re-mint guarantee for
    /// derived service-issued tokens. The response's actual
    /// <c>expires_in</c> wins when present.
    /// </summary>
    public TimeSpan TokenExchangeCacheFallbackTtl { get; set; } = TimeSpan.FromMinutes(5);
}
