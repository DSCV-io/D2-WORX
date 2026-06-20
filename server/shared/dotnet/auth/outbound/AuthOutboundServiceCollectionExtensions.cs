// -----------------------------------------------------------------------
// <copyright file="AuthOutboundServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound;

using D2.Shared.Auth.Outbound.ServiceIdentity;
using D2.Shared.Auth.Outbound.TokenExchange;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using D2.Shared.Utilities.Extensions;
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
        /// <param name="configure">
        /// Configuration delegate for <see cref="AuthOutboundOptions"/>.
        /// </param>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2AuthOutbound(Action<AuthOutboundOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var optionsBuilder = services.AddOptions<AuthOutboundOptions>();
            if (configure is not null)
                optionsBuilder.Configure(configure);

            // Required-field validation runs at host startup so misconfiguration
            // surfaces during composition, not on the first failed token fetch.
            optionsBuilder
                .Validate(
                    o => o.Issuer.Truthy(),
                    "AuthOutboundOptions.Issuer is required.")
                .Validate(
                    o => o.ClientId.Truthy(),
                    "AuthOutboundOptions.ClientId is required.")
                .Validate(
                    o => o.ClientSecret.Truthy(),
                    "AuthOutboundOptions.ClientSecret is required.")
                .ValidateOnStart();

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
                var metadataAddress =
                    $"{opts.Issuer.TrimEnd('/')}/.well-known/openid-configuration";
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

        /// <summary>
        /// Registers the outbound workload-certificate (mutual-TLS leaf)
        /// presentation stack — the single per-process live-leaf cache, the
        /// refresh-ahead leaf client (<see cref="IWorkloadLeafSource"/>), and the
        /// proactive reissue hosted service. Composable with — and independent of —
        /// <see cref="AddD2AuthOutbound"/>: a host that presents a workload leaf but
        /// wants no outbound tokens (or vice-versa) wires only what it needs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>The host supplies the issuer.</b> The reissue mechanism is the
        /// host-supplied <see cref="IWorkloadCertificateIssuer"/> port — register it
        /// (an in-process adapter calling KeyCustodian's issuance handler in dev; a
        /// local issuance in the end-to-end harness) BEFORE building the host. The
        /// shared lib never references a service's domain.
        /// </para>
        /// <para>
        /// Per-channel attachment is opt-in via
        /// <c>GrpcClientBuilderExtensions.AddD2WorkloadCertificate()</c> on the gRPC
        /// client builder — a channel that does not call it presents no client cert.
        /// </para>
        /// </remarks>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2WorkloadCertificateOutbound()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddOptions<AuthOutboundOptions>();
            services.TryAddSingleton(TimeProvider.System);

            services.TryAddSingleton<WorkloadLeafCache>();
            services.TryAddSingleton<WorkloadLeafClient>();
            services.TryAddSingleton<IWorkloadLeafSource>(sp =>
                sp.GetRequiredService<WorkloadLeafClient>());

            services.AddHostedService(sp => new WorkloadLeafRefreshHostedService(
                sp.GetRequiredService<WorkloadLeafClient>(),
                sp.GetRequiredService<WorkloadLeafCache>(),
                sp.GetRequiredService<IOptions<AuthOutboundOptions>>(),
                sp.GetRequiredService<
                    Microsoft.Extensions.Logging.ILogger<WorkloadLeafRefreshHostedService>>(),
                sp.GetRequiredService<TimeProvider>()));

            return services;
        }

        /// <summary>
        /// The host's residual config registration for the forwarded-transaction-token
        /// outbound factor — the forwarded-JWT analogue of
        /// <see cref="AddD2WorkloadCertificateOutbound"/>. Pair the two on any host
        /// that makes internal gRPC calls under the forward-unchanged model; the
        /// generated gRPC-client DI extension auto-chains
        /// <c>.AddD2ForwardedJwt().AddD2WorkloadCertificate()</c> on every internal
        /// client, and these two registrations supply what the emitter cannot invent.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>What the forwarding credential needs — and where it comes from.</b> The
        /// per-channel <c>.AddD2ForwardedJwt()</c> credential resolves, on each
        /// outbound RPC, the current request's request-scoped
        /// <c>IForwardedJwtAccessor</c> through the framework-free
        /// <c>IAmbientRequestScopeAccessor</c> port. Both of those services are
        /// registered by the INBOUND auth transport (<c>AddD2AuthHttp()</c> registers
        /// the holder AND the <c>IHttpContextAccessor</c>-backed ambient adapter; the
        /// holder is also registered by <c>AddD2AuthGrpc()</c>). A forwarding host is
        /// by definition an inbound host — it received an inbound request to have a
        /// token to forward — so those registrations are already present. This call
        /// therefore deliberately registers NEITHER the holder NOR the ambient adapter
        /// (the inbound transport owns them) and keeps the outbound lib free of any
        /// AspNetCore framework reference.
        /// </para>
        /// <para>
        /// The credential reads no configuration (no TTL, no endpoint — the token is
        /// ambient), so this adds no <see cref="AuthOutboundOptions"/> fields. It
        /// touches <see cref="AuthOutboundOptions"/> only to keep the options pipeline
        /// present and the surface symmetric with
        /// <see cref="AddD2WorkloadCertificateOutbound"/>; it is idempotent.
        /// </para>
        /// </remarks>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2ForwardedJwtOutbound()
        {
            ArgumentNullException.ThrowIfNull(services);

            // The forwarding credential reads no config; this keeps the options
            // pipeline present and the surface symmetric with the mTLS sibling.
            // Holder + ambient adapter come from the inbound transport (see remarks).
            services.AddOptions<AuthOutboundOptions>();

            return services;
        }
    }
}
