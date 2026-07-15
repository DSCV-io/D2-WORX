// -----------------------------------------------------------------------
// <copyright file="CaRootSigningCapabilityServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.CertificateAuthority;

/// <summary>
/// DI registration for the dedicated CA-root-signing capability. Deliberately
/// SEPARATE from <c>AddD2KeyCustodianApp()</c>: the capability is the ONLY holder of
/// every stored <c>mtls-ca-root</c> private-key plaintext materialization (the mesh
/// trust anchor — a cluster-root-grade secret under rules §9.44), so it is granted
/// solely by the one composition root that holds root-signing authority (the in-host
/// System rotation worker). A provider built from the general registration alone
/// cannot resolve it — and, because all four lifecycle-mutation handlers take the
/// capability, cannot even construct them. The authority over the root is thus a
/// build-time, review-visible fact (a DI-resolution failure for the wrong caller),
/// not a runtime boolean guard.
/// </summary>
public static class CaRootSigningCapabilityServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="ICaRootSigningCapability"/> →
        /// <c>CaRootSigningCapability</c> (transient). Call this ONLY from the
        /// composition root that holds root-signing authority (the System rotation
        /// worker's host) — never from the general KeyCustodian registration. Without
        /// this call the four lifecycle-mutation handlers cannot resolve, so root
        /// signing (and every stored-root-key unwrap) is structurally absent from a
        /// host that does not opt in.
        /// </summary>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2CaRootSigningCapability()
        {
            // Plain reference-type null-guard (IServiceCollection DI service) — no
            // present-but-falsey concept, so BCL ThrowIfNull is correct here.
            ArgumentNullException.ThrowIfNull(services);

            services.AddTransient<ICaRootSigningCapability, CaRootSigningCapability>();

            return services;
        }
    }
}
