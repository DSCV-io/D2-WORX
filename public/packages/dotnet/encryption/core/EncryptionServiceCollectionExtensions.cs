// -----------------------------------------------------------------------
// <copyright file="EncryptionServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Registration helpers for <see cref="IPayloadCrypto"/>. One <see cref="IPayloadCrypto"/>
/// is registered per encryption purpose (typically per domain) using
/// .NET keyed services so a service holding multiple keyrings can pick
/// the right one with <c>[FromKeyedServices(...)]</c>.
/// </summary>
public static class EncryptionServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers a keyed <see cref="PayloadCryptoKeyring"/> and a matching
        /// keyed <see cref="IPayloadCrypto"/> under <paramref name="serviceKey"/>.
        /// The keyring factory runs once (singleton).
        /// </summary>
        /// <param name="serviceKey">
        /// Discriminator for keyed resolution — typically the encryption
        /// purpose name (e.g. <c>"audit"</c>, <c>"root"</c>). Consumers inject
        /// with <c>[FromKeyedServices(serviceKey)] IPayloadCrypto crypto</c>.
        /// </param>
        /// <param name="keyringFactory">
        /// Builds the keyring from the service provider — typically resolves a
        /// <c>KeyringClient</c> and asks for the active keyring for this purpose.
        /// </param>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2EncryptionFor(
            string serviceKey,
            Func<IServiceProvider, PayloadCryptoKeyring> keyringFactory)
        {
            ArgumentNullException.ThrowIfNull(services);
            serviceKey.ThrowIfFalsey();
            ArgumentNullException.ThrowIfNull(keyringFactory);

            services.AddKeyedSingleton<PayloadCryptoKeyring>(
                serviceKey, (sp, _) => keyringFactory(sp));
            services.AddKeyedSingleton<IPayloadCrypto>(serviceKey, (sp, key) =>
                new PayloadCrypto(sp.GetRequiredKeyedService<PayloadCryptoKeyring>(key)));

            // Track the registered keys on a single registry so the startup
            // self-test (if added) can iterate without inspecting the container.
            services.TryAddSingleton<EncryptionRegistry>();
            services.AddSingleton(new EncryptionRegistration(serviceKey));

            // Deny-by-default provenance guard: the raw seam registers NO source
            // marker, so EncryptionSourceStartupCheck classifies this registration
            // as a static factory and — outside a Development host — crashes the
            // host. A KeyCustodian-sourced registration marks itself KeyCustodian
            // and passes. Hooked here so no encryption host can dodge the check.
            services.AddD2EncryptionSourceCheck();

            return services;
        }

        /// <summary>
        /// Registers <see cref="EncryptionStartupCheck"/>, an
        /// <see cref="IHostedService"/> that runs an encrypt → decrypt round-trip
        /// against every keyed <see cref="IPayloadCrypto"/> at host startup.
        /// Failures throw and prevent the host from accepting traffic.
        /// </summary>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2EncryptionStartupCheck()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.AddHostedService<EncryptionStartupCheck>();
            return services;
        }
    }
}
