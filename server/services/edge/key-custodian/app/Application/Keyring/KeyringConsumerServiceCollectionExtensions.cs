// -----------------------------------------------------------------------
// <copyright file="KeyringConsumerServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Keyring;

using D2.Edge.KeyCustodian.Client.Keyring;
using D2.Shared.Encryption;
using D2.Shared.Time;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The module's IN-PROCESS keyring registration source: backs a domain's keyed
/// <see cref="IPayloadCrypto"/> with the co-hosted KeyCustodian leaf (no network hop,
/// still rotation-aware) by composing the client package's internal hot-swap machinery
/// over an <see cref="InProcessKeyringClient"/>. Lives in App — the in-process source
/// references the leaf <c>IKeyCustodianApi</c>, which the client package cannot reach
/// under the dependency law; the cross-process sibling
/// (<c>AddD2EncryptionForViaKeyring</c>) lives in the client package.
/// </summary>
public static class KeyringConsumerServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Backs the keyed <see cref="IPayloadCrypto"/> for <paramref name="domain"/> with the
        /// IN-PROCESS KeyCustodian keyring source. This is the safe replacement for a
        /// hand-rolled static-key registration in production use; the deny-by-default
        /// encryption-source guard passes because the registration marks its provenance
        /// KeyCustodian.
        /// </summary>
        /// <param name="domain">The payload key domain to serve.</param>
        /// <param name="callingModuleId">
        /// The id of the module/host making the in-process call (the host names itself —
        /// fail-closed, no ambient guessing). Flows to the leaf as the established
        /// <c>ImmediateCaller</c> the keyring authority policy gates on.
        /// </param>
        /// <returns>The same <paramref name="services"/> for chaining.</returns>
        public IServiceCollection AddD2EncryptionFromKeyCustodian(
            string domain, string callingModuleId)
        {
            ArgumentNullException.ThrowIfNull(services);
            domain.ThrowIfFalsey();
            callingModuleId.ThrowIfFalsey();

            services.AddKeyedSingleton<IKeyringClient>(
                domain,
                (sp, _) => new InProcessKeyringClient(
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<IClock>(),
                    callingModuleId));

            return services.AddKeyringBackedPayloadCrypto(domain);
        }
    }
}
