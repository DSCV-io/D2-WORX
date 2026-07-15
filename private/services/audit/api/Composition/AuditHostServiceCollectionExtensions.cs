// -----------------------------------------------------------------------
// <copyright file="AuditHostServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Audit.Api.Composition;

using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Auth.Grpc;
using DcsvIo.D2.Caching.Distributed.Redis;
using DcsvIo.D2.Caching.Tiered;
using DcsvIo.D2.Private.Audit.Api.Kestrel;
using DcsvIo.D2.Private.Audit.Api.Mtls;
using DcsvIo.D2.Private.Audit.App.Application;
using DcsvIo.D2.ServiceDefaults;
using DcsvIo.D2.Utilities.Configuration;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Audit host DI composition — <see cref="AddD2AuditHost"/>.
/// </summary>
public static class AuditHostServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the full Audit host composition: service defaults (Auth + MutualTls),
        /// dual-bind Kestrel, gRPC establishment, Redis + tiered cache, and the NIE
        /// App layer. No JWT minter. No Edge HTTP establishment.
        /// </summary>
        /// <param name="configuration">Host configuration root.</param>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a required configuration key is missing, blank, or invalid.
        /// </exception>
        public IServiceCollection AddD2AuditHost(IConfiguration configuration)
        {
            // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            var issuerRaw = configuration["KEYCUSTODIAN_APP:IssuerBaseUrl"]
                ?? configuration["KEYCUSTODIAN_APP:ISSUERBASEURL"];

            if (issuerRaw.Falsey())
            {
                throw new InvalidOperationException(
                    "KEYCUSTODIAN_APP:IssuerBaseUrl (or KEYCUSTODIAN_APP__ISSUERBASEURL) "
                    + "is required — in-cluster Issuer HTTPS base, e.g. https://d2-edge:8443.");
            }

            var issuerUri = new Uri(issuerRaw!);

            // Same dual-URL honesty as Edge: https only; never mTLS Edge port 9443.
            if (!string.Equals(
                    issuerUri.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase)
                || issuerUri.Port == 9443)
            {
                throw new InvalidOperationException(
                    "KEYCUSTODIAN_APP:IssuerBaseUrl must be https://… and must not use the "
                    + "Edge mTLS port (9443). In-cluster SoT: https://d2-edge:8443.");
            }

            // OIDC/JWKS HttpClient must trust the same public CA root as mTLS
            // TrustAnchors (Issuer listen cert is intermediate-signed under the
            // D2 Internal Root CA — not in the OS store).
            var auditTrustAnchorPath = configuration[LoadPublicCaAnchors.TRUST_ANCHOR_PATH_KEY]
                ?? configuration["AUDIT_MTLS:TRUST_ANCHOR_PATH"];

            services.AddD2ServiceDefaults(configuration, opts =>
            {
                opts.AuthConfigure = auth =>
                {
                    auth.Issuer = issuerUri;
                    auth.Audience = WellKnownAudiences.D2_INTERNAL_AUDIENCE;

                    if (auditTrustAnchorPath.Truthy())
                    {
                        auth.Jwks = auth.Jwks with
                        {
                            TrustedRootCertificatePath = auditTrustAnchorPath,
                        };
                    }
                };

                opts.MutualTlsConfigure = mtls =>
                {
                    mtls.Enabled = true;

                    // Audit inbound callers today: edge process only.
                    mtls.AllowedWorkloads = ["edge"];
                    mtls.TrustAnchorsProvider =
                        LoadPublicCaAnchors.FromConfiguration(configuration);
                };
            });

            // NEVER services.AddD2MutualTls(...) again after defaults.

            // Dual-bind Kestrel AFTER MutualTls registration.
            services.AddSingleton<
                IConfigureOptions<KestrelServerOptions>,
                AuditHttpsRoleKestrelConfigure>();

            // ServiceId + establishment — gRPC interceptor only (no Edge HTTP on Audit).
            services.AddD2RequestOriginGrpc(o => o.ServiceId = AuditHostIdentity.SERVICE_ID);

            // Honest Redis — never raw redis:// into StackExchange.
            var redisUrl = configuration["REDIS_URL"];

            if (redisUrl.Falsey())
                throw new InvalidOperationException("REDIS_URL is required.");

            services.AddD2DistributedCacheRedis(o =>
                o.ConnectionString = ConnectionStringHelper.ParseRedisUri(redisUrl!));

            services.AddD2RedisCacheInvalidationBackplane();
            services.AddD2TieredCache();

            services.AddD2AuditApp();
            services.AddGrpc();

            return services;
        }
    }
}
