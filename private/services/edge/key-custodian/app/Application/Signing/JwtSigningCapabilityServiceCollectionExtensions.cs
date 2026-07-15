// -----------------------------------------------------------------------
// <copyright file="JwtSigningCapabilityServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Signing;

using D2.Edge.KeyCustodian.Client.Signing;

/// <summary>
/// DI registration for the dedicated JWT-minter signing capability. Deliberately SEPARATE
/// from <c>AddD2KeyCustodianApp()</c> / <c>AddD2KeyCustodianClient()</c>: the capability
/// is the ONLY path to the cluster-signing root (<c>jwks-signing</c>), so it is granted
/// solely in the auth-module (JWT minter) composition. Possession of the resolved seam —
/// plus the in-process-module plane check inside the impl — IS the authority.
/// </summary>
public static class JwtSigningCapabilityServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="IJwtSigningCapability"/> → <c>JwtSigningCapability</c>
        /// (transient). Call this ONLY from the JWT minter's (auth module's) composition —
        /// never from the general KeyCustodian client registration. A provider built without
        /// this call cannot resolve the capability (the general surface cannot reach the
        /// cluster-signing root).
        /// </summary>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2JwtSigningCapability()
        {
            // Plain reference-type null-guard (IServiceCollection DI service) — no
            // present-but-falsey concept, so BCL ThrowIfNull is correct here.
            ArgumentNullException.ThrowIfNull(services);

            services.AddTransient<IJwtSigningCapability, JwtSigningCapability>();

            return services;
        }
    }
}
