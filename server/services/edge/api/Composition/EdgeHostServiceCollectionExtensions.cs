// -----------------------------------------------------------------------
// <copyright file="EdgeHostServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Api.Composition;

using D2.Edge.Api.Kestrel;
using D2.Edge.Api.Mtls;
using D2.Edge.Api.Outbound;
using D2.Edge.KeyCustodian.App.Application.CertificateAuthority;
using D2.Edge.KeyCustodian.App.Application.Issuance;
using D2.Edge.KeyCustodian.Infra.Configuration;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Grpc;
using D2.Shared.Auth.Http;
using D2.Shared.Auth.Outbound;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using D2.Shared.Caching.Distributed.Redis;
using D2.Shared.Caching.Tiered;
using D2.Shared.Messaging.RabbitMq;
using D2.Shared.ServiceDefaults;
using D2.Shared.Utilities.Configuration;
using D2.Shared.Utilities.Extensions;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Edge host DI composition — <see cref="AddD2EdgeHost"/>.
/// </summary>
public static class EdgeHostServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the full Edge host composition: service defaults (Auth + MutualTls),
        /// three-bind Kestrel roles, establishment (Edge HTTP + gRPC), Redis + tiered cache,
        /// RabbitMQ, KeyCustodian (with CA leaf/root caps, without JWT minter), and outbound
        /// dual-factor stack with the CSR PoC issuer.
        /// </summary>
        /// <param name="configuration">Host configuration root.</param>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when a required configuration key is missing, blank, or invalid.
        /// </exception>
        public IServiceCollection AddD2EdgeHost(IConfiguration configuration)
        {
            // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            // Fail-loud Issuer at registration (same class as REDIS_URL) — dual-URL honesty
            // is enforced here, not only when AuthOptions resolve.
            var issuerRaw = configuration["KEYCUSTODIAN_APP:IssuerBaseUrl"]
                ?? configuration["KEYCUSTODIAN_APP:ISSUERBASEURL"];

            if (issuerRaw.Falsey())
            {
                throw new InvalidOperationException(
                    "KEYCUSTODIAN_APP:IssuerBaseUrl (or KEYCUSTODIAN_APP__ISSUERBASEURL) "
                    + "is required — in-cluster Issuer HTTPS base, e.g. https://d2-edge:8443.");
            }

            var issuerUri = new Uri(issuerRaw!);

            if (!string.Equals(
                    issuerUri.Scheme,
                    Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase)
                || issuerUri.Port == EdgeHttpsRolePolicies.MtlsHttpsPort)
            {
                throw new InvalidOperationException(
                    "KEYCUSTODIAN_APP:IssuerBaseUrl must be https://… and must not use the "
                    + $"mTLS port ({EdgeHttpsRolePolicies.MtlsHttpsPort}). "
                    + "In-cluster SoT: https://d2-edge:8443.");
            }

            services.AddD2ServiceDefaults(configuration, opts =>
            {
                opts.AuthConfigure = auth =>
                {
                    auth.Issuer = issuerUri;
                    auth.Audience = WellKnownAudiences.D2_INTERNAL_AUDIENCE;
                };

                opts.MutualTlsConfigure = mtls =>
                {
                    mtls.Enabled = true;

                    // Edge inbound callers today: audit process. Expand as mesh grows.
                    mtls.AllowedWorkloads = ["audit"];
                    mtls.TrustAnchorsProvider =
                        LoadPublicCaAnchors.FromConfiguration(configuration);
                };
            });

            // NEVER services.AddD2MutualTls(...) again after defaults.

            // M1-B exclusive three-bind after MutualTls registration.
            services.AddSingleton<
                IConfigureOptions<KestrelServerOptions>,
                EdgeHttpsRoleKestrelConfigure>();

            // ServiceId + establishment — Edge HTTP middleware + gRPC interceptor.
            // AddD2RequestOriginGrpc AFTER Auth.Grpc from defaults (interceptor order).
            services.AddD2RequestOriginEdge(o => o.ServiceId = EdgeHostIdentity.SERVICE_ID);
            services.AddD2RequestOriginGrpc(o => o.ServiceId = EdgeHostIdentity.SERVICE_ID);

            // Honest Redis — never raw redis:// into StackExchange.
            var redisUrl = configuration["REDIS_URL"];

            if (redisUrl.Falsey())
                throw new InvalidOperationException("REDIS_URL is required.");

            services.AddD2DistributedCacheRedis(o =>
                o.ConnectionString = ConnectionStringHelper.ParseRedisUri(redisUrl!));

            services.AddD2RedisCacheInvalidationBackplane();
            services.AddD2TieredCache();

            // Real RMQ.
            var rmqUrl = configuration["RABBITMQ_URL"];

            if (rmqUrl.Falsey())
                throw new InvalidOperationException("RABBITMQ_URL is required.");

            services.AddD2MessagingRabbitMq(o => o.ConnectionUri = rmqUrl!);

            // Honest PG — never raw postgresql:// into Npgsql.
            var kcCsRaw = configuration["KEYCUSTODIAN_DATABASE_URL"];

            if (kcCsRaw.Falsey())
            {
                throw new InvalidOperationException(
                    "KEYCUSTODIAN_DATABASE_URL is required.");
            }

            var kcCs = ConnectionStringHelper.ParsePostgresUri(kcCsRaw!);

            services.AddD2KeyCustodian(configuration, kcCs);
            services.AddD2CaLeafSigningCapability();
            services.AddD2CaRootSigningCapability();

            // Do NOT call AddD2JwtSigningCapability — structural deny on general host.
            services.AddGrpc();

            // Outbound dual-factor DI. AddD2WorkloadCertificateOutbound also registers
            // WorkloadLeafRefreshHostedService (IssueAsync at host StartAsync).
            services.AddD2WorkloadCertificateOutbound();
            services.AddD2ForwardedJwtOutbound();
            services.AddSingleton<
                IWorkloadCertificateIssuer,
                PoCCsrSigningWorkloadCertificateIssuer>();

            return services;
        }
    }
}
