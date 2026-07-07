// -----------------------------------------------------------------------
// <copyright file="SealingConsumerServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Sealing;

using D2.Edge.KeyCustodian.Client.Sealing;
using D2.Shared.Encryption;
using D2.Shared.Time;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// The module's IN-PROCESS sealed-encryption registration source — the sealed sibling of
/// <c>KeyringConsumerServiceCollectionExtensions.AddD2EncryptionFromKeyCustodian</c>. Backs the
/// keyed sealer(s) with the co-hosted KeyCustodian leaf (no network hop, still rotation-aware)
/// by composing the client package's internal spec-driven wiring over an
/// <see cref="InProcessSealingClient"/>. Lives in App — the in-process source references the
/// leaf <c>IKeyCustodianApi</c>, which the client package cannot reach under the dependency
/// law; the cross-process sibling (<c>AddD2SealedEncryptionViaKeyCustodian</c>) lives in the
/// client package.
/// </summary>
public static class SealingConsumerServiceCollectionExtensions
{
    /// <param name="services">The DI container.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Wires SEALER-ONLY sealed-encryption support for <paramref name="ownServiceId"/> over
        /// the IN-PROCESS KeyCustodian leaf: a keyed <see cref="IPayloadSealer"/> for every
        /// distinct sealed-domain consumer service (lazy public-key fetch). NO opener is wired —
        /// sealed decrypt is CrossProcessHop-only; a co-hosted module that must open sealed
        /// frames takes the cross-process <c>AddD2SealedEncryptionViaKeyCustodian</c> opener arm.
        /// </summary>
        /// <param name="ownServiceId">This service's own id.</param>
        /// <param name="callingModuleId">
        /// The id of the module/host making the in-process call (the host names itself —
        /// fail-closed, no ambient guessing). Flows to the leaf as the established
        /// <c>ImmediateCaller</c> the seal authority policy gates on.
        /// </param>
        /// <returns>The same <paramref name="services"/> for chaining.</returns>
        public IServiceCollection AddD2SealedEncryptionFromKeyCustodian(
            string ownServiceId, string callingModuleId)
        {
            ArgumentNullException.ThrowIfNull(services);
            ownServiceId.ThrowIfFalsey();
            callingModuleId.ThrowIfFalsey();

            services.AddSingleton<ISealingClient>(
                sp => new InProcessSealingClient(
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<IClock>(),
                    callingModuleId));

            // Sealer arms only (wireOpener: false) — the in-process source never registers an
            // opener; the private-key op is CrossProcessHop-only.
            return services.AddSealedEncryptionOverSealingClient(ownServiceId, wireOpener: false);
        }
    }
}
