// -----------------------------------------------------------------------
// <copyright file="CaLeafSigningCapabilityServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Issuance;

/// <summary>
/// DI registration for the dedicated issuance leaf-signing capability.
/// Deliberately SEPARATE from <c>AddD2KeyCustodianApp()</c>: the capability is the
/// ONLY holder of the issuance-path intermediate-CA unwrap (signing a
/// caller-supplied CSR public key mints a trusted workload identity), so it is
/// granted solely by the composition root that serves the issuance surface. A
/// provider built from the general registration alone cannot resolve it — and
/// therefore cannot sign a workload leaf via the issuance path.
/// </summary>
public static class CaLeafSigningCapabilityServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="ICaLeafSigningCapability"/> →
        /// <c>CaLeafSigningCapability</c> (transient). Call this ONLY from the
        /// composition root that serves the issuance surface — never from the
        /// general KeyCustodian registration. Without this call the issuance
        /// handler cannot resolve, so the issuance path is structurally absent
        /// from a host that does not opt in.
        /// </summary>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2CaLeafSigningCapability()
        {
            // Plain reference-type null-guard (IServiceCollection DI service) — no
            // present-but-falsey concept, so BCL ThrowIfNull is correct here.
            ArgumentNullException.ThrowIfNull(services);

            services.AddTransient<ICaLeafSigningCapability, CaLeafSigningCapability>();

            return services;
        }
    }
}
