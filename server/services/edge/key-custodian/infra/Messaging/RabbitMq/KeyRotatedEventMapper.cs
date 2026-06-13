// -----------------------------------------------------------------------
// <copyright file="KeyRotatedEventMapper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Infra.Messaging.RabbitMq;

using D2.Shared.Auth.Events;

/// <summary>
/// Pure mapper between the domain-shaped announce arguments and the
/// <see cref="KeyRotatedEvent"/> wire DTO (messaging-wire ↔ domain — the
/// uppermost-node surface for a published message lives in the infra publisher).
/// </summary>
/// <remarks>
/// The wire <c>NewStatus</c> carries the stable enum NAME (not the integer) so
/// consumers decode it without sharing the CLR enum. The payload is public
/// identifiers only — no key material, no compromise reason.
/// </remarks>
public static class KeyRotatedEventMapper
{
    extension(KeyDomain domain)
    {
        /// <summary>
        /// Builds the <see cref="KeyRotatedEvent"/> wire DTO for an announced
        /// key-status change in this domain.
        /// </summary>
        /// <param name="kid">The kid whose status changed.</param>
        /// <param name="newStatus">The resulting lifecycle status.</param>
        /// <param name="urgent">
        /// <see langword="true"/> for a compromise announce (carries the urgent
        /// session-invalidation signal); <see langword="false"/> for a routine
        /// rotation.
        /// </param>
        /// <returns>The populated wire event.</returns>
        public KeyRotatedEvent ToKeyRotatedEvent(Kid kid, KeyStatus newStatus, bool urgent) =>
            new()
            {
                Domain = domain.Value,
                Kid = kid.Value,
                NewStatus = newStatus.ToString(),
                Urgent = urgent,
            };
    }
}
