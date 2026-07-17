// -----------------------------------------------------------------------
// <copyright file="EncryptionSourceServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration helpers for the encryption-source provenance guard: the keyed
/// marker that records where a domain's keyring comes from, and the hosted
/// <see cref="EncryptionSourceStartupCheck"/> that enforces it deny-by-default.
/// </summary>
public static class EncryptionSourceServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Records the provenance of the encryption registration under
        /// <paramref name="serviceKey"/> as a keyed <see cref="EncryptionSourceMarker"/>.
        /// A KeyCustodian source passes <see cref="EncryptionSourceStartupCheck"/>
        /// in every environment; a static source (or an unmarked registration)
        /// is rejected outside a Development host.
        /// </summary>
        /// <param name="serviceKey">
        /// The keyed-services discriminator (typically the domain) whose source
        /// is being marked — must match the key passed to <c>AddD2EncryptionFor</c>.
        /// </param>
        /// <param name="source">The provenance of that registration.</param>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection MarkD2EncryptionSource(
            string serviceKey,
            EncryptionKeyringSource source)
        {
            ArgumentNullException.ThrowIfNull(services);
            serviceKey.ThrowIfFalsey();

            services.AddKeyedSingleton(
                serviceKey, new EncryptionSourceMarker(serviceKey, source));

            return services;
        }

        /// <summary>
        /// Registers <see cref="EncryptionSourceStartupCheck"/>, the
        /// <see cref="Microsoft.Extensions.Hosting.IHostedService"/> that
        /// enforces keyring provenance at host startup. Idempotent — safe to
        /// call once per <c>AddD2EncryptionFor</c>; the hosted service is
        /// registered exactly once.
        /// </summary>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2EncryptionSourceCheck()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.AddHostedService<EncryptionSourceStartupCheck>();

            return services;
        }
    }
}
