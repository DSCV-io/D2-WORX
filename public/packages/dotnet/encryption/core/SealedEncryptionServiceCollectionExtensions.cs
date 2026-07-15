// -----------------------------------------------------------------------
// <copyright file="SealedEncryptionServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Encryption;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Registration helpers for the sealed-encryption startup self-check
/// machinery — the sealed sibling of
/// <see cref="EncryptionSourceServiceCollectionExtensions"/>. This is a
/// building-block seam a registration SOURCE composes (the KeyCustodian-backed
/// <c>AddD2SealedEncryptionViaKeyCustodian</c> single call in the KC client
/// package, or the in-process twin), NOT the surface a consumer remembers: the
/// caller separately registers the keyed <see cref="IPayloadSealer"/> and/or
/// <see cref="IPayloadOpener"/> under the same recipient service id, and this
/// method records the recipient so the hosted self-check verifies whichever
/// sides exist. Public because those cross-assembly sources call it; a service
/// composes ONE spec-driven registration call rather than this directly.
/// </summary>
public static class SealedEncryptionServiceCollectionExtensions
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

        /// <summary>
        /// Registers <see cref="SealedEncryptionSourceStartupCheck"/>, the hosted service that
        /// enforces sealed-keyring provenance deny-by-default at host startup (a static /
        /// unmarked sealed recipient is rejected outside a Development host). Called by the
        /// KeyCustodian-backed sealing source, which also marks each registration's provenance
        /// KeyCustodian. Idempotent — the hosted service is registered exactly once.
        /// </summary>
        /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
        public IServiceCollection AddD2SealedEncryptionSourceCheck()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<SealedEncryptionRegistry>();
            services.AddHostedService<SealedEncryptionSourceStartupCheck>();

            return services;
        }
    }
}
