// -----------------------------------------------------------------------
// <copyright file="SealingServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Client.Sealing;

using System.Linq;
using D2.Edge.KeyCustodian.Client.Keyring;
using D2.Private.Encryption;
using D2.Shared.Auth.Events;
using D2.Shared.Encryption;
using D2.Shared.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OwnSealPrivateKeyStub =
    D2.Services.Protos.KeyCustodian.V2Alpha.KeyCustodianOwnSealPrivateKey.KeyCustodianOwnSealPrivateKeyClient;
using SealPublicKeyStub =
    D2.Services.Protos.KeyCustodian.V2Alpha.KeyCustodianSealPublicKey.KeyCustodianSealPublicKeyClient;

/// <summary>
/// The single spec-driven registration surface for the sealed (asymmetric) payload-encryption
/// mode backed by KeyCustodian over the CROSS-PROCESS gRPC seal surfaces. ONE call per service
/// wires sealing support for every sealed domain generically -- the fine-grained keyed
/// sealer/opener registrations are the INTERNAL building blocks it composes (the
/// <c>AddKeyringBackedPayloadCrypto</c> precedent), so a consumer remembers exactly one call.
/// </summary>
/// <remarks>
/// <para>
/// The name diverges DELIBERATELY from the symmetric <c>AddD2EncryptionForViaKeyring</c>: the
/// symmetric name binds to its single backing SURFACE (the keyring gRPC service), but sealing
/// is backed by TWO seal gRPC services (public + own-private), so no single surface noun
/// exists and the sealed name binds to the source SYSTEM (<c>ViaKeyCustodian</c>) -- which also
/// keeps the sealed pair internally consistent with the in-process <c>FromKeyCustodian</c>
/// twin. No <c>For</c> infix because the call is per-SERVICE whole-surface (spec-driven over
/// ALL sealed domains), not per-domain.
/// </para>
/// <para>
/// Hard enforcement of who may OPEN a sealed frame remains KeyCustodian-side (the mTLS-peer
/// key selection of the own-private-key op); the DI shape here is hygiene / least-privilege --
/// a producer registers only sealers, and the private-key opener is registered ONLY when the
/// generated catalog names the registering service as some sealed domain's consumer.
/// </para>
/// </remarks>
public static class SealingServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Wires sealed-encryption support for <paramref name="ownServiceId"/> over the
        /// CROSS-PROCESS KeyCustodian seal surfaces: a keyed <see cref="IPayloadSealer"/> for
        /// every distinct sealed-domain consumer service (lazy public-key fetch), plus a keyed
        /// <see cref="IPayloadOpener"/> under <paramref name="ownServiceId"/> ONLY when the
        /// generated catalog names this service as some sealed domain's consumer. The two seal
        /// gRPC client stubs are host-provided (live mTLS address at Edge composition; a
        /// TestServer channel in isolation).
        /// </summary>
        /// <param name="ownServiceId">This service's own id.</param>
        /// <returns>The same <paramref name="services"/> for chaining.</returns>
        public IServiceCollection AddD2SealedEncryptionViaKeyCustodian(string ownServiceId)
        {
            // §5.1a carve-out: plain reference-type null-guard -- no present-but-falsey.
            ArgumentNullException.ThrowIfNull(services);
            ValidateServiceIdGrammar(ownServiceId, nameof(ownServiceId));

            services.TryAddSingleton<ISealingClient>(static sp => new GrpcSealingClient(
                sp.GetRequiredService<SealPublicKeyStub>(),
                sp.GetRequiredService<OwnSealPrivateKeyStub>()));

            return services.AddSealedEncryptionOverSealingClient(ownServiceId, wireOpener: true);
        }

        /// <summary>
        /// Shared spec-driven wiring used by BOTH the cross-process call and the module App's
        /// in-process twin: registers the rotation infrastructure (once), a keyed lazy sealer
        /// for every distinct generated consumer service, and -- when
        /// <paramref name="wireOpener"/> is <see langword="true"/> AND this service is a sealed
        /// consumer -- the self-only private-key opener. Internal: a registration SOURCE (this
        /// class or the module App's in-process source) composes it over a registered
        /// <see cref="ISealingClient"/>; it is never a consumer-facing seam.
        /// </summary>
        /// <param name="ownServiceId">This service's own id.</param>
        /// <param name="wireOpener">
        /// Whether to wire the self private-key opener when this service is a sealed consumer.
        /// The in-process twin passes <see langword="false"/> -- no in-process opener source
        /// exists anywhere (decrypt is CrossProcessHop-only).
        /// </param>
        /// <returns>The same <paramref name="services"/> for chaining.</returns>
        internal IServiceCollection AddSealedEncryptionOverSealingClient(
            string ownServiceId, bool wireOpener)
        {
            // Product sealed domains live in private Extensions; messaging
            // (MqMessageDescriptor.IsSealed / SealedConsumerStartupCheck) resolves via the
            // public EncryptionDomainModeCatalog overlay. BOTH Via and From entrypoints compose
            // this shared method -- bootstrap here so no sealed DI path can skip the overlay.
            ProductEncryptionDomainBootstrap.EnsureRegistered();

            RegisterRotationInfraOnce(services);

            // Sealer for every DISTINCT consumer service across all sealed domains (two sealed
            // domains that share a consumer share ONE keyed sealer). Lazy public-key fetch.
            foreach (var consumerService in DistinctConsumerServices())
                services.AddSealerViaKeyCustodian(consumerService);

            // Opener, self-only + structurally gated: registered ONLY when the generated
            // ConsumerServiceByDomain lookup names ownServiceId as some sealed domain's
            // consumer. A non-consumer host gets NO opener registration at all.
            if (wireOpener && IsSealedConsumer(ownServiceId))
                services.AddOpenerViaKeyCustodian(ownServiceId);

            return services;
        }

        /// <summary>
        /// Building block: registers the keyed <see cref="IPayloadSealer"/> (key =
        /// <paramref name="recipientServiceId"/>) backed by a lazily-fetched public seal
        /// keyring, marks its provenance KeyCustodian, and records the recipient for the sealed
        /// startup self-check. Internal -- the single call composes it.
        /// </summary>
        /// <param name="recipientServiceId">The recipient service to seal payloads to.</param>
        /// <returns>The same <paramref name="services"/> for chaining.</returns>
        internal IServiceCollection AddSealerViaKeyCustodian(string recipientServiceId)
        {
            services.AddKeyedSingleton<IPayloadSealer>(
                recipientServiceId,
                static (sp, key) => KeyringBackedPayloadSealer.Create(
                    (string)key!,
                    sp.GetRequiredService<ISealingClient>(),
                    sp.GetRequiredService<IRotationEventChannel>(),
                    sp.GetRequiredService<ILogger<KeyringBackedPayloadSealer>>()));

            services.MarkD2EncryptionSource(
                recipientServiceId, EncryptionKeyringSource.KeyCustodian);
            services.AddD2SealedEncryptionRecipient(recipientServiceId);
            services.AddD2SealedEncryptionSourceCheck();

            return services;
        }

        /// <summary>
        /// Building block: registers the keyed <see cref="IPayloadOpener"/> (key =
        /// <paramref name="ownServiceId"/>) backed by this service's own private seal keyring
        /// (fail-loud boot fetch), marks its provenance KeyCustodian, and records the recipient
        /// for the sealed startup self-check. Internal -- the single call composes it.
        /// </summary>
        /// <param name="ownServiceId">This service's own id (the sealed recipient identity).</param>
        /// <returns>The same <paramref name="services"/> for chaining.</returns>
        internal IServiceCollection AddOpenerViaKeyCustodian(string ownServiceId)
        {
            services.AddKeyedSingleton<IPayloadOpener>(
                ownServiceId,
                static (sp, key) => KeyringBackedPayloadOpener.Create(
                    (string)key!,
                    sp.GetRequiredService<ISealingClient>(),
                    sp.GetRequiredService<IRotationEventChannel>(),
                    sp.GetRequiredService<ILogger<KeyringBackedPayloadOpener>>()));

            services.MarkD2EncryptionSource(ownServiceId, EncryptionKeyringSource.KeyCustodian);
            services.AddD2SealedEncryptionRecipient(ownServiceId);
            services.AddD2SealedEncryptionSourceCheck();

            return services;
        }
    }

    /// <summary>
    /// The distinct sealed-domain consumer services from the generated catalog (two sealed
    /// domains sharing a consumer collapse to one).
    /// </summary>
    /// <returns>The distinct consumer service ids.</returns>
    internal static IEnumerable<string> DistinctConsumerServices()
        => ProductEncryptionDomainModes.ConsumerServiceByDomain.Values
            .Distinct(StringComparer.Ordinal);

    /// <summary>Whether <paramref name="serviceId"/> is some sealed domain's consumer.</summary>
    /// <param name="serviceId">The service id to test.</param>
    /// <returns><see langword="true"/> when the service consumes a sealed domain.</returns>
    internal static bool IsSealedConsumer(string serviceId)
        => ProductEncryptionDomainModes.ConsumerServiceByDomain.Values.Contains(
            serviceId, StringComparer.Ordinal);

    // Validate the bare lowercase [a-z0-9-]{1,64} workload-service-id grammar fail-loud at
    // registration (the same grammar SealedKeyringValidation enforces at keyring construction --
    // duplicated inline because that validator is internal to D2.Shared.Encryption and the
    // client package cannot reach it; the keyring ctor re-checks at fetch time).
    private static void ValidateServiceIdGrammar(string serviceId, string paramName)
    {
        serviceId.ThrowIfFalsey();

        if (serviceId.Length > 64 || !serviceId.All(static c =>
                c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
        {
            throw new ArgumentException(
                $"'{serviceId}' must be a bare lowercase [a-z0-9-] identifier, at most 64 "
                + "characters (the workload service-id grammar).",
                paramName);
        }
    }

    // The rotation subscriber + channel are shared infrastructure -- one per host, regardless of
    // how many seal domains are registered (and shared with the symmetric keyring source when
    // both are wired). Guard against a duplicate ISubscriberRegistration for the single
    // keyring-refresh queue (the seal rotation events ride the same KeyRotatedEvent fanout).
    private static void RegisterRotationInfraOnce(IServiceCollection services)
    {
        services.TryAddSingleton<RabbitMqRotationEventChannel>();
        services.TryAddSingleton<IRotationEventChannel>(
            static sp => sp.GetRequiredService<RabbitMqRotationEventChannel>());

        if (services.Any(static d => d.ServiceType == typeof(KeyringRefreshSubscriber)))
            return;

        services.AddD2Subscriber<KeyringRefreshSubscriber, KeyRotatedEvent>(
            MqSubscriptionsRegistry.ByConstant[MqSubscriptions.KeyringRefresh]);
    }
}
