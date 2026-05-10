// -----------------------------------------------------------------------
// <copyright file="AuthOutboundServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound;

using D2.Shared.Auth.Outbound.ServiceIdentity;
using D2.Shared.Auth.Outbound.TokenExchange;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

/// <summary>
/// DI registration entry-point for the outbound auth runtime —
/// <c>IServiceIdentityClient</c> + <c>ITokenExchangeClient</c> plus the
/// supporting refresh hosted service, named <c>HttpClient</c>s, and shared
/// caches. Composition root only; per-channel gRPC opt-in lives in the
/// <c>GrpcClientBuilderExtensions.AddD2ServiceIdentity()</c> extension.
/// </summary>
public static class AuthOutboundServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the full outbound-auth stack. Designed as a single-call
        /// composition root; calling repeatedly is harmless for the
        /// <c>TryAddSingleton</c>-guarded registrations but appends extra
        /// configurators to the named <c>HttpClient</c>s and registers
        /// duplicate <c>IHostedService</c> instances — call once per host.
        /// </summary>
        /// <param name="configure">Configuration delegate for <see cref="AuthOutboundOptions"/>.</param>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2AuthOutbound(Action<AuthOutboundOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            if (configure is not null)
                services.Configure(configure);
            else
                services.AddOptions<AuthOutboundOptions>();

            services.TryAddSingleton(TimeProvider.System);

            // Named HttpClient used by ConfigurationManager for OIDC discovery
            // doc fetches. Same Timeout knob as the token-fetch clients.
            services.AddHttpClient(AuthOutboundHttpClientNames.OIDC_DISCOVERY, (sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<AuthOutboundOptions>>().Value;
                client.Timeout = opts.HttpRequestTimeout;
            });

            // OIDC discovery: one ConfigurationManager per process; auto-refreshes
            // the discovery doc + JWKS on its own schedule. Routes discovery
            // requests through our IHttpClientFactory-managed client (3-arg
            // ctor) so HttpRequestTimeout / TLS / connection-pool config
            // surface here, not via the default static HttpClient.
            services.TryAddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<AuthOutboundOptions>>().Value;
                var metadataAddress = $"{opts.Issuer.TrimEnd('/')}/.well-known/openid-configuration";
                var httpClient = sp.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(AuthOutboundHttpClientNames.OIDC_DISCOVERY);
                return new ConfigurationManager<OpenIdConnectConfiguration>(
                    metadataAddress,
                    new OpenIdConnectConfigurationRetriever(),
                    httpClient);
            });

            // ServiceIdentity stack — single per-process token cache, HTTP client,
            // proactive refresh hosted service.
            services.TryAddSingleton<ServiceIdentityCache>();
            services.TryAddSingleton<HttpServiceIdentityClient>();
            services.TryAddSingleton<IServiceIdentityClient>(sp =>
                sp.GetRequiredService<HttpServiceIdentityClient>());

            services.AddHttpClient(HttpServiceIdentityClient.HTTP_CLIENT_NAME, (sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<AuthOutboundOptions>>().Value;
                client.Timeout = opts.HttpRequestTimeout;
            });

            services.AddHostedService<ServiceIdentityRefreshHostedService>();

            // TokenExchange stack — ILocalCache-backed cache + reverse-index +
            // backplane subscription (when ICacheInvalidationBackplane is
            // registered) + HTTP client.
            services.TryAddSingleton<TokenExchangeCache>();
            services.TryAddSingleton<HttpTokenExchangeClient>();
            services.TryAddSingleton<ITokenExchangeClient>(sp =>
                sp.GetRequiredService<HttpTokenExchangeClient>());

            services.AddHttpClient(HttpTokenExchangeClient.HTTP_CLIENT_NAME, (sp, client) =>
            {
                var opts = sp.GetRequiredService<IOptions<AuthOutboundOptions>>().Value;
                client.Timeout = opts.HttpRequestTimeout;
            });

            return services;
        }
    }
}
