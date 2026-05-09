// -----------------------------------------------------------------------
// <copyright file="EncryptionDomains.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Encryption;

/// <summary>
/// Canonical encryption-domain identifiers shared across the codebase.
/// Each domain corresponds to an independently-rotated keyring registered via
/// <c>services.AddD2EncryptionFor(domain, factory)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Domains are deliberately a closed set — adding a new one is an architectural
/// decision that requires KeyCustodian provisioning, key-rotation runbook
/// updates, and operator coordination. Use these constants instead of raw
/// strings so a typo can't silently route a message to a non-existent
/// keyring.
/// </para>
/// <para>
/// Consumed by <c>D2.Shared.Messaging</c>'s <c>[Encrypted]</c> attribute on
/// proto-typed message classes, by <c>AddD2EncryptionFor</c> registrations in
/// service composition roots, and by ops tooling (the <c>d2 keys ...</c> CLI).
/// </para>
/// </remarks>
public static class EncryptionDomains
{
    /// <summary>
    /// Audit events. All services publish; D2.Audit consumes. Carries actor
    /// + actee identities, action descriptors, IPs, and fingerprints — fully
    /// PII-bearing on every event.
    /// </summary>
    public const string Audit = "audit";

    /// <summary>
    /// Notification requests (the input shape D2.Notifications consumes).
    /// Carries recipient identity + subject / body markdown, often with names,
    /// addresses, financial figures, and verification codes.
    /// </summary>
    public const string Notifications = "notifications";

    /// <summary>
    /// Courier delivery records (the materialized email / SMS payloads
    /// D2.Notifications hands to D2.Courier). Carries fully-rendered message
    /// bodies and recipient addresses.
    /// </summary>
    public const string Courier = "courier";
}
