// -----------------------------------------------------------------------
// <copyright file="SealedEncryptionServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Encryption;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registration helpers for the sealed-encryption startup self-check
/// machinery. Deliberately <c>internal</c>: no consumer exists until the
/// sealed registration-by-service sources land, and keeping the surface
/// internal lets that step reshape/populate it additively without a
/// public-API break. Tests reach it via <c>InternalsVisibleTo</c>.
/// </summary>
internal static class SealedEncryptionServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Records a sealed recipient for startup verification and ensures
        /// the hosted <see cref="SealedEncryptionStartupCheck"/> is
        /// registered. The caller separately registers the keyed
        /// <see cref="IPayloadSealer"/> and/or <see cref="IPayloadOpener"/>
        /// under <paramref name="recipientServiceId"/> — the check resolves
        /// whichever sides exist.
        /// </summary>
        /// <param name="recipientServiceId">
        /// The recipient service id the sealer/opener registrations are
        /// keyed by (lowercase <c>[a-z0-9-]</c>, at most 64 characters).
        /// </param>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2SealedEncryptionRecipient(string recipientServiceId)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton(new SealedEncryptionRegistration(recipientServiceId));
            services.AddD2SealedEncryptionStartupCheck();

            return services;
        }

        /// <summary>
        /// Registers <see cref="SealedEncryptionStartupCheck"/>, the hosted
        /// service that verifies every registered sealed recipient at host
        /// startup. Idempotent — the hosted service and the registry are
        /// each registered exactly once.
        /// </summary>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2SealedEncryptionStartupCheck()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<SealedEncryptionRegistry>();
            services.AddHostedService<SealedEncryptionStartupCheck>();

            return services;
        }
    }
}
