// -----------------------------------------------------------------------
// <copyright file="InProcessJwksProviderServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Jwks;

using D2.Shared.Auth.Abstractions.Jwks;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration that replaces the consumer-side HTTP
/// <see cref="IJwksProvider"/> with the issuer-host in-process KeyCustodian
/// store reader on Edge only.
/// </summary>
/// <remarks>
/// Call <strong>after</strong> <c>AddD2Auth</c> (which registers
/// <c>HttpJwksProvider</c> as the default <see cref="IJwksProvider"/>) and
/// after KeyCustodian infra has registered <c>IKeyCustodianDbContext</c>.
/// Only the <see cref="IJwksProvider"/> interface registration is replaced —
/// the concrete <c>HttpJwksProvider</c> remains registered for any direct
/// resolve; remote hosts must not call this extension.
/// </remarks>
public static class InProcessJwksProviderServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Replaces <see cref="IJwksProvider"/> with
        /// <see cref="InProcessJwksProvider"/> so the issuer host validates
        /// JWTs from the KeyCustodian DB (Active + Retiring jwks-signing keys)
        /// without HTTP self-fetch to its own well-known endpoints.
        /// </summary>
        /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="services"/> is null.
        /// </exception>
        public IServiceCollection AddD2InProcessJwksProvider()
        {
            // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
            ArgumentNullException.ThrowIfNull(services);

            // Replace interface only — leave HttpJwksProvider concrete registration.
            services.RemoveAll<IJwksProvider>();
            services.TryAddSingleton<InProcessJwksProvider>();
            services.AddSingleton<IJwksProvider>(sp =>
                sp.GetRequiredService<InProcessJwksProvider>());

            return services;
        }
    }
}
