// -----------------------------------------------------------------------
// <copyright file="KeyringServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Client.Keyring;

using System.Linq;
using D2.Shared.Auth.Events;
using D2.Shared.Encryption;
using D2.Shared.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using KeyringClientStub = D2.Services.Protos.KeyCustodian.V2Alpha.KeyCustodianKeyring.KeyCustodianKeyringClient;

/// <summary>
/// Registration source that backs a domain's <see cref="IPayloadCrypto"/> with a
/// rotation-aware KeyCustodian keyring fetched over the CROSS-PROCESS gRPC surface.
/// The module's own App project offers the sibling IN-PROCESS source
/// (<c>AddD2EncryptionFromKeyCustodian</c>) built on the same internal hot-swap
/// machinery (<see cref="AddKeyringBackedPayloadCrypto"/>). Both mark the
/// registration's provenance <see cref="EncryptionKeyringSource.KeyCustodian"/> so
/// the deny-by-default source guard passes, and both wire the shared rotation
/// channel + refresh subscriber.
/// </summary>
public static class KeyringServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Backs the keyed <see cref="IPayloadCrypto"/> for <paramref name="domain"/> with a
        /// CROSS-PROCESS KeyCustodian keyring source (the gRPC keyring surface). Consumers
        /// resolve <c>[FromKeyedServices(domain)] IPayloadCrypto</c>. The keyring gRPC client
        /// stub is host-provided (live mTLS address at Edge composition; a TestServer channel
        /// in isolation).
        /// </summary>
        /// <param name="domain">The payload key domain to serve.</param>
        /// <returns>The same <paramref name="services"/> for chaining.</returns>
        public IServiceCollection AddD2EncryptionForViaKeyring(string domain)
        {
            ArgumentNullException.ThrowIfNull(services);
            domain.ThrowIfFalsey();

            services.AddKeyedSingleton<IKeyringClient>(
                domain,
                static (sp, _) => new GrpcKeyringClient(
                    sp.GetRequiredService<KeyringClientStub>()));

            return services.AddKeyringBackedPayloadCrypto(domain);
        }

        /// <summary>
        /// Shared keyring-backed registration used by BOTH sources: the rotation channel +
        /// refresh subscriber (once per host), the keyed hot-swap crypto over the keyed
        /// <see cref="IKeyringClient"/> the caller registered, the encryption
        /// registry/registration, and the KeyCustodian source provenance marker + the
        /// deny-by-default source check. Internal — a registration source (this class or the
        /// module App's in-process source) composes it; it is never a consumer-facing seam.
        /// </summary>
        /// <param name="domain">The payload key domain to serve.</param>
        /// <returns>The same <paramref name="services"/> for chaining.</returns>
        internal IServiceCollection AddKeyringBackedPayloadCrypto(string domain)
        {
            services.TryAddSingleton<RabbitMqRotationEventChannel>();
            services.TryAddSingleton<IRotationEventChannel>(
                static sp => sp.GetRequiredService<RabbitMqRotationEventChannel>());
            RegisterRefreshSubscriberOnce(services);

            services.AddKeyedSingleton<IPayloadCrypto>(
                domain,
                static (sp, key) => KeyringBackedPayloadCrypto.Create(
                    (string)key!,
                    sp.GetRequiredKeyedService<IKeyringClient>(key),
                    sp.GetRequiredService<IRotationEventChannel>(),
                    sp.GetRequiredService<ILogger<KeyringBackedPayloadCrypto>>()));

            services.TryAddSingleton<EncryptionRegistry>();
            services.AddSingleton(new EncryptionRegistration(domain));

            // Provenance: a KeyCustodian-sourced keyring passes the deny-by-default source
            // guard in every environment; the guard hook makes an unmarked host crash outside
            // Development.
            services.MarkD2EncryptionSource(domain, EncryptionKeyringSource.KeyCustodian);
            services.AddD2EncryptionSourceCheck();

            return services;
        }
    }

    // The rotation subscriber + its subscription are shared infrastructure — one per host,
    // regardless of how many domains are registered. Guard against a duplicate
    // ISubscriberRegistration for the single keyring-refresh queue.
    private static void RegisterRefreshSubscriberOnce(IServiceCollection services)
    {
        if (services.Any(static d => d.ServiceType == typeof(KeyringRefreshSubscriber)))
            return;

        services.AddD2Subscriber<KeyringRefreshSubscriber, KeyRotatedEvent>(
            MqSubscriptionsRegistry.ByConstant[MqSubscriptions.KeyringRefresh]);
    }
}
