// -----------------------------------------------------------------------
// <copyright file="KeyCustodianAppServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application;

using D2.Edge.KeyCustodian.App.Application.Facade;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.CompromiseKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueLeaf;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RotateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.SeedCertificateAuthority;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetCaCertificate;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetKeyring;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetOidcConfiguration;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetRotationPlan;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.Sign;

/// <summary>
/// DI registration for the KeyCustodian App layer: the 15 operation handlers and
/// the options-backed rotation-policy + authority providers.
/// </summary>
/// <remarks>
/// This extension registers ONLY what the App layer owns. The seams the handlers
/// depend on but that App does NOT own — the concrete
/// <c>IKeyCustodianDbContext</c>, the keyed root <c>IPayloadCrypto</c>, the
/// <c>IRootKeyProvider</c>, and the <c>IKeyRotationAnnouncer</c> implementation —
/// are registered by the Infra layer. The options binding +
/// startup validation also live in Infra. The issuance leaf-signing capability
/// (<c>ICaLeafSigningCapability</c>) is deliberately NOT registered here — it is
/// granted solely by its own dedicated extension
/// (<c>AddD2CaLeafSigningCapability()</c>) from the composition root that serves
/// the issuance surface, so a provider built from this registration alone cannot
/// sign a workload leaf via the issuance path.
/// </remarks>
public static class KeyCustodianAppServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the KeyCustodian App-layer services: handlers (transient) and
        /// the rotation-policy provider.
        /// </summary>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2KeyCustodianApp()
        {
            // §5.1a carve-out: plain reference-type null-guard (IServiceCollection DI service)
            // — no present-but-falsey concept.
            ArgumentNullException.ThrowIfNull(services);

            // Lifecycle command handlers.
            services.AddTransient<IGenerateKeyHandler, GenerateKeyHandler>();
            services.AddTransient<IActivateKeyHandler, ActivateKeyHandler>();
            services.AddTransient<IRotateKeyHandler, RotateKeyHandler>();
            services.AddTransient<IRetireKeyHandler, RetireKeyHandler>();
            services.AddTransient<ICompromiseKeyHandler, CompromiseKeyHandler>();
            services.AddTransient<IRunDueRotationsHandler, RunDueRotationsHandler>();
            services.AddTransient<
                IIssueWorkloadCertificateHandler, IssueWorkloadCertificateHandler>();
            services.AddTransient<IIssueLeafHandler, IssueLeafHandler>();
            services.AddTransient<
                ISeedCertificateAuthorityHandler, SeedCertificateAuthorityHandler>();

            // Query handlers.
            services.AddTransient<IGetJwksHandler, GetJwksHandler>();
            services.AddTransient<
                IGetOidcConfigurationHandler, GetOidcConfigurationHandler>();
            services.AddTransient<IGetRotationPlanHandler, GetRotationPlanHandler>();
            services.AddTransient<ISignHandler, SignHandler>();
            services.AddTransient<IGetKeyringHandler, GetKeyringHandler>();
            services.AddTransient<IGetCaCertificateHandler, GetCaCertificateHandler>();

            // Policy providers.
            services.AddSingleton<IRotationPolicyProvider, OptionsRotationPolicyProvider>();
            services.AddSingleton<
                ISigningDomainAuthorityPolicy, OptionsSigningDomainAuthorityPolicy>();
            services.AddSingleton<
                IKeyringDomainAuthorityPolicy, OptionsKeyringDomainAuthorityPolicy>();

            // Generated façade layer — registers IKeyCustodianApi → KeyCustodianApi (Transient).
            // The generated extension is overwritten on rebuild; this call site is the
            // stable hand-written anchor.
            services.AddD2KeyCustodianClient();

            return services;
        }
    }
}
