// -----------------------------------------------------------------------
// <copyright file="IpInfoResponse.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.App.Interfaces.WhoIs;

/// <summary>
/// Response from IP information lookup.
/// </summary>
/// <remarks>
/// Maps to the relevant fields from IPinfo.io API response.
/// </remarks>
public record IpInfoResponse
{
    /// <summary>
    /// Gets the IP address.
    /// </summary>
    public string? Ip { get; init; }

    /// <summary>
    /// Gets the hostname.
    /// </summary>
    public string? Hostname { get; init; }

    /// <summary>
    /// Gets the city name.
    /// </summary>
    public string? City { get; init; }

    /// <summary>
    /// Gets the region/state name.
    /// </summary>
    public string? Region { get; init; }

    /// <summary>
    /// Gets the country ISO code.
    /// </summary>
    public string? Country { get; init; }

    /// <summary>
    /// Gets the postal/ZIP code.
    /// </summary>
    public string? Postal { get; init; }

    /// <summary>
    /// Gets the latitude as a string.
    /// </summary>
    public string? Latitude { get; init; }

    /// <summary>
    /// Gets the longitude as a string.
    /// </summary>
    public string? Longitude { get; init; }

    /// <summary>
    /// Gets the organization/ASN info (format: "AS12345 Org Name").
    /// </summary>
    public string? Org { get; init; }

    /// <summary>
    /// Gets the privacy/anonymity flags.
    /// </summary>
    public IpInfoPrivacy? Privacy { get; init; }
}
