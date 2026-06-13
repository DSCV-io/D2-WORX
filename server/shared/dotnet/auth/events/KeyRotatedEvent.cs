// -----------------------------------------------------------------------
// <copyright file="KeyRotatedEvent.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Events;

using D2.Shared.Messaging;

/// <summary>
/// Message published by KeyCustodian whenever a key changes state during the
/// rotation lifecycle. Consumers (e.g. auth middleware, JWKS refresh jobs) subscribe
/// to refresh their in-process key rings on receiving this event.
/// </summary>
/// <remarks>
/// <para>
/// The exchange is <c>d2.security.key-rotated</c> (fanout), meaning all queue
/// bindings receive a copy. Encryption is plaintext by design — the event delivers
/// only the (domain, kid, new status) tuple consumers need to trigger a ring refresh;
/// no key material is transmitted. Encrypting with the rotating key would create an
/// unresolvable chicken-and-egg: subscribers cannot decrypt the notification without
/// first completing the rotation that the notification triggers.
/// </para>
/// <para>
/// <see cref="MqPubAttribute"/> is applied directly on this single sealed record so
/// the publisher's runtime resolver finds it on the exact CLR type that gets
/// instantiated and published. The type lives in this dedicated leaf assembly (not
/// the auth vocabulary slice) because <see cref="MqPubAttribute"/> forces a reference
/// to <c>D2.Shared.Messaging.Abstractions</c>, which the auth vocabulary slice cannot
/// take without closing a dependency cycle.
/// </para>
/// </remarks>
[MqPub(MqMessages.AuthKeyRotated)]
public sealed record KeyRotatedEvent
{
    /// <summary>
    /// Gets the key domain identifier (e.g. <c>"jwks-signing"</c>, <c>"audit"</c>).
    /// </summary>
    public required string Domain { get; init; }

    /// <summary>
    /// Gets the key identifier (KID) of the key whose status changed.
    /// </summary>
    public required string Kid { get; init; }

    /// <summary>
    /// Gets the key's new lifecycle status after the rotation step
    /// (e.g. <c>"Active"</c>, <c>"Retiring"</c>, <c>"Retired"</c>).
    /// </summary>
    public required string NewStatus { get; init; }

    /// <summary>
    /// Gets a value indicating whether consumers should refresh their key rings
    /// urgently rather than waiting for the next scheduled refresh tick.
    /// </summary>
    public bool Urgent { get; init; }
}
