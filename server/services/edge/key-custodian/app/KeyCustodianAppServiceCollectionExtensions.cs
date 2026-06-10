// -----------------------------------------------------------------------
// <copyright file="KeyCustodianAppServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App;

using D2.Edge.KeyCustodian.App.Implementations.CQRS.Handlers.C;
using D2.Edge.KeyCustodian.App.Implementations.CQRS.Handlers.Q;
using D2.Edge.KeyCustodian.App.Implementations.Crypto;
using D2.Edge.KeyCustodian.App.Implementations.Policy;
using D2.Edge.KeyCustodian.App.Interfaces.CQRS.Handlers.C;
using D2.Edge.KeyCustodian.App.Interfaces.CQRS.Handlers.Q;
using D2.Edge.KeyCustodian.App.Interfaces.Crypto;
using D2.Edge.KeyCustodian.App.Interfaces.Policy;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration for the KeyCustodian App layer: the 7 lifecycle handlers, the
/// 3 key generators, the smoke tester, and the options-backed rotation-policy
/// provider.
/// </summary>
/// <remarks>
/// This extension registers ONLY what the App layer owns. The seams the handlers
/// depend on but that App does NOT own — the concrete
/// <c>IKeyCustodianDbContext</c>, the keyed root <c>IPayloadCrypto</c>, the
/// <c>IRootKeyProvider</c>, and the <c>IKeyRotationAnnouncer</c> implementation —
/// are registered by the Infra layer. The options binding +
/// startup validation also live in Infra.
/// </remarks>
public static class KeyCustodianAppServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the KeyCustodian App-layer services: handlers (transient),
        /// key generators, smoke tester, and rotation-policy provider.
        /// </summary>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddKeyCustodianApp()
        {
            ArgumentNullException.ThrowIfNull(services);

            // Lifecycle command handlers.
            services.AddTransient<IGenerateKey, GenerateKey>();
            services.AddTransient<IActivateKey, ActivateKey>();
            services.AddTransient<IRotateKey, RotateKey>();
            services.AddTransient<IRetireKey, RetireKey>();
            services.AddTransient<ICompromiseKey, CompromiseKey>();

            // Query handlers.
            services.AddTransient<IGetJwks, GetJwks>();
            services.AddTransient<IGetRotationPlan, GetRotationPlan>();

            // Key generators — one per key type; handlers select by Handles.
            services.AddSingleton<IKeyGenerator, RsaSigningKeyGenerator>();
            services.AddSingleton<IKeyGenerator, AesPayloadKeyGenerator>();
            services.AddSingleton<IKeyGenerator, SecretKeyGenerator>();

            // Crypto + policy services.
            services.AddSingleton<ISmokeTester, SmokeTester>();
            services.AddSingleton<IRotationPolicyProvider, OptionsRotationPolicyProvider>();

            return services;
        }
    }
}
