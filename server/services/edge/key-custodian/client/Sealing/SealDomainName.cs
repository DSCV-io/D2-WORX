// -----------------------------------------------------------------------
// <copyright file="SealDomainName.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Client.Sealing;

/// <summary>
/// The <c>seal:&lt;serviceId&gt;</c> domain-name family for a per-service sealing
/// keyring — the rotation-event domain a sealer/opener subscribes and the metric
/// <c>domain</c> tag. Mirrors <c>KeyDomain.SEAL_PREFIX</c> in the KC domain layer,
/// which this client package cannot reference under the dependency law; the literal
/// is wire/spec-anchored (§5.25 exemption — it IS the KeyCustodian seal-domain wire
/// value the <c>KeyRotatedEvent.Domain</c> carries for a seal key).
/// </summary>
internal static class SealDomainName
{
    /// <summary>The seal-domain family prefix.</summary>
    public const string PREFIX = "seal:";

    /// <summary>Builds the <c>seal:&lt;serviceId&gt;</c> domain name for a service.</summary>
    /// <param name="serviceId">The recipient (public) or own (private) service id.</param>
    /// <returns>The <c>seal:&lt;serviceId&gt;</c> domain string.</returns>
    public static string For(string serviceId) => PREFIX + serviceId;
}
