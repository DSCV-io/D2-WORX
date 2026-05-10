// -----------------------------------------------------------------------
// <copyright file="AuthOutboundHttpClientNames.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound;

/// <summary>
/// Named <c>HttpClient</c> identifiers registered by
/// <c>AuthOutboundServiceCollectionExtensions.AddD2AuthOutbound</c>. Hosts
/// that want to attach extra <c>HttpMessageHandler</c>s (resilience pipeline,
/// distributed tracing) to any of these clients reference them by name via
/// <c>services.AddHttpClient(name).AddHttpMessageHandler&lt;T&gt;()</c>.
/// </summary>
public static class AuthOutboundHttpClientNames
{
    /// <summary>
    /// Named client used by
    /// <c>ConfigurationManager&lt;OpenIdConnectConfiguration&gt;</c> for OIDC
    /// discovery-doc fetches. Routing discovery through
    /// <c>IHttpClientFactory</c> (instead of the static default
    /// <c>HttpClient</c> that <c>OpenIdConnectConfigurationRetriever()</c>
    /// would otherwise construct internally) lets the host configure
    /// timeouts / TLS / connection-pool lifetime / DNS-staleness mitigation
    /// in one place.
    /// </summary>
    public const string OIDC_DISCOVERY = "d2-auth-oidc-discovery";

    /// <summary>
    /// Named client used by <c>HttpServiceIdentityClient</c> for OAuth
    /// <c>client_credentials</c> grant requests against Edge.
    /// </summary>
    public const string SERVICE_IDENTITY = "d2-auth-service-identity";

    /// <summary>
    /// Named client used by <c>HttpTokenExchangeClient</c> for OAuth
    /// <c>token-exchange</c> (RFC 8693) requests against Edge.
    /// </summary>
    public const string TOKEN_EXCHANGE = "d2-auth-token-exchange";
}
