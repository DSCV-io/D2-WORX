// -----------------------------------------------------------------------
// <copyright file="IpInfoPrivacy.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Interfaces.WhoIs;

/// <summary>
/// Privacy flags from IP information lookup.
/// </summary>
public record IpInfoPrivacy
{
    /// <summary>
    /// Gets a value indicating whether the IP is a VPN.
    /// </summary>
    public bool? Vpn { get; init; }

    /// <summary>
    /// Gets a value indicating whether the IP is a proxy.
    /// </summary>
    public bool? Proxy { get; init; }

    /// <summary>
    /// Gets a value indicating whether the IP is a Tor exit node.
    /// </summary>
    public bool? Tor { get; init; }

    /// <summary>
    /// Gets a value indicating whether the IP is a relay.
    /// </summary>
    public bool? Relay { get; init; }

    /// <summary>
    /// Gets a value indicating whether the IP is a hosting provider.
    /// </summary>
    public bool? Hosting { get; init; }
}
