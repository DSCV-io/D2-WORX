// -----------------------------------------------------------------------
// <copyright file="AuthServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth;

using DcsvIo.D2.Auth.Abstractions.Jwks;
using DcsvIo.D2.Auth.Abstractions.Sessions;
using DcsvIo.D2.Auth.Jwks;
using DcsvIo.D2.Auth.Sessions;
using DcsvIo.D2.Auth.Validation;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

/// <summary>
/// DI registration entry point for the inbound auth runtime — JWKS provider,
/// session liveness tracker, JWT validator, ASP.NET Core middleware, gRPC
/// interceptor.
/// </summary>
public static class AuthServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds the inbound auth runtime to the service collection. Wires options,
        /// the named OIDC discovery <see cref="IHttpClientFactory"/> client, the
        /// shared <see cref="IConfigurationManager{T}"/>, the JWKS provider + its
        /// rotation backplane subscriber, and the session liveness tracker + its
        /// revoke-event observer.
        /// </summary>
        /// <remarks>
        /// If <paramref name="configure"/> throws, the exception bubbles to the
        /// first <c>IOptions&lt;AuthOptions&gt;.Value</c> resolution (typically at
        /// host startup composition) — fail-fast, never silent.
        /// </remarks>
        /// <param name="configure">
        /// Configures <see cref="AuthOptions"/>. Must populate the required
        /// <see cref="AuthOptions.Issuer"/> + <see cref="AuthOptions.Audience"/>
        /// fields; defaults apply for the rest.
        /// </param>
        /// <returns>The same <c>IServiceCollection</c> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <c>services</c> or <paramref name="configure"/> is null.
        /// </exception>
        public IServiceCollection AddD2Auth(
            Action<AuthOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.AddOptions<AuthOptions>()
                .Configure(configure)
                .Validate(
                    o => o.Issuer is not null && o.Audience.Truthy(),
                    "AuthOptions.Issuer and AuthOptions.Audience are required.")
                .Validate(
                    o => o.Issuer is null || o.Issuer.Scheme == Uri.UriSchemeHttps,
                    "AuthOptions.Issuer must be HTTPS.")
                .Validate(
                    o => o.Jwks.BackplaneChannelKey.Truthy(),
                    "AuthOptions.Jwks.BackplaneChannelKey must not be empty / whitespace.")
                .Validate(
                    o => o.Validator.ValidAlgorithms.Count > 0,
                    "AuthOptions.Validator.ValidAlgorithms must contain at least one entry.")
                .Validate(
                    o => o.Validator.ValidAlgorithms.All(a => a.Truthy()),
                    "AuthOptions.Validator.ValidAlgorithms entries must not be empty / whitespace.")
                .Validate(
                    o => o.Jwks.TrustedRootCertificatePath.Falsey()
                        || File.Exists(o.Jwks.TrustedRootCertificatePath),
                    "AuthOptions.Jwks.TrustedRootCertificatePath must point to an existing "
                    + "PUBLIC CA certificate file when set (or leave empty for system trust).")
                .ValidateOnStart();

            services.TryAddSingleton(TimeProvider.System);

            // Named HttpClient used by ConfigurationManager for OIDC discovery
            // doc + JWKS fetches. Independent from auth-outbound's same-purpose
            // client (different naming so per-client policies can diverge).
            // Per-request timeout sourced from JwksProviderOptions; default 5s
            // — without this override the BCL default of 100s applies.
            // Optional TrustedRootCertificatePath pins a private-PKI public CA
            // (CustomRootTrust + hostname still validated; never accept-any).
            services.AddHttpClient(OIDC_DISCOVERY_HTTP_CLIENT_NAME, (sp, client) =>
                {
                    var opts = sp.GetRequiredService<IOptions<AuthOptions>>().Value;
                    client.Timeout = opts.Jwks.HttpRequestTimeout;
                })
                .ConfigurePrimaryHttpMessageHandler(sp =>
                {
                    var opts = sp.GetRequiredService<IOptions<AuthOptions>>().Value;

                    return OidcDiscoveryHttpMessageHandlerFactory.Create(
                        opts.Jwks.TrustedRootCertificatePath);
                });

            // OIDC discovery: one ConfigurationManager per process. TryAdd so
            // we're compat with auth-outbound's same registration when both
            // libs are loaded in the same composition root (whoever registers
            // first wins; same Edge issuer either way).
            services.TryAddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<AuthOptions>>().Value;

                // Issuer is non-null at this point: ValidateOnStart() rejected
                // null/missing Issuer at host build time, so any resolution past
                // that point is guaranteed populated.
                var metadataAddress = new Uri(
                    opts.Issuer!,
                    "/.well-known/openid-configuration").ToString();
                var httpClient = sp.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(OIDC_DISCOVERY_HTTP_CLIENT_NAME);
                return new ConfigurationManager<OpenIdConnectConfiguration>(
                    metadataAddress,
                    new OpenIdConnectConfigurationRetriever(),
                    httpClient);
            });

            // JWKS provider — wraps the shared ConfigurationManager + adds
            // Singleflight + cooldown on RefreshAsync.
            services.TryAddSingleton<HttpJwksProvider>();
            services.TryAddSingleton<IJwksProvider>(sp =>
                sp.GetRequiredService<HttpJwksProvider>());

            // Backplane subscriber for cluster-wide JWKS rotation events.
            // Always registered; no-ops at runtime when ICacheInvalidationBackplane
            // is absent (single-instance / test deploys).
            services.AddHostedService<JwksBackplaneSubscriber>();

            // Session liveness — sentinel-only ITieredCache check.
            // Cache invalidation on session-revoke is handled automatically by
            // DefaultTieredCache's built-in backplane subscription; the
            // SessionRevokedBackplaneSubscriber exists for telemetry observation only.
            services.TryAddSingleton<TieredCacheSessionLivenessTracker>();
            services.TryAddSingleton<ISessionLivenessTracker>(sp =>
                sp.GetRequiredService<TieredCacheSessionLivenessTracker>());

            services.AddHostedService<SessionRevokedBackplaneSubscriber>();

            // JWT validation pipeline — stateless singletons. ClaimsToContextMapper
            // is a thin facade over the codegen FromClaims factory; JwtValidator
            // wraps JsonWebTokenHandler + the JWKS provider for the
            // signature + standard-claims validation pipeline.
            services.TryAddSingleton<ClaimsToContextMapper>();
            services.TryAddSingleton<JwtValidator>();

            return services;
        }
    }

    /// <summary>
    /// Named <c>HttpClient</c> identifier for OIDC discovery + JWKS fetches.
    /// Independent from auth-outbound's same-purpose client (different
    /// timeouts / policies may apply).
    /// </summary>
    public const string OIDC_DISCOVERY_HTTP_CLIENT_NAME = "d2-auth-oidc-discovery";
}
