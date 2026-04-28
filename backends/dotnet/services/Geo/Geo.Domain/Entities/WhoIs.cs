// -----------------------------------------------------------------------
// <copyright file="WhoIs.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable MemberCanBePrivate.Global
namespace D2.Geo.Domain.Entities;

using System.Security.Cryptography;
using System.Text;
using D2.Geo.Domain.Exceptions;
using D2.Shared.Utilities.Attributes;
using D2.Shared.Utilities.Enums;
using D2.Shared.Utilities.Extensions;

/// <summary>
/// Represents WHOIS, ASN and GEOIP information associated with an IP address.
/// </summary>
/// <remarks>
/// Is an aggregate root of the Geography "Geo" Domain. Relates to the <see cref="Location"/>
/// entity via a foreign key to describe the geographic information related to this IP address.
///
/// Its primary key is a content-addressable SHA-256 hash of the IP address, year and month of
/// the record.
///
/// This allows for efficient storage and retrieval of historical WHOIS data.
///
/// <see cref="WhoIs"/> records should be immutable once created to maintain historical accuracy.
/// </remarks>
public record WhoIs
{
    #region Identity

    /// <summary>
    /// Gets a content-addressable SHA-256 hash (hex string) of the IP address, year and month of
    /// the record.
    /// </summary>
    /// <example>
    /// A1B2C3D4E5F6...
    /// </example>
    public required string HashId { get; init; }

    #endregion

    #region Content Addressable Properties

    /// <summary>
    /// Gets the IP address of the record.
    /// </summary>
    /// <example>
    /// 75.155.155.200.
    /// </example>
    [RedactData(Reason = RedactReason.PersonalInformation)]
    public required string IPAddress { get; init; }

    /// <summary>
    /// Gets the year of the record's creation.
    /// </summary>
    /// <example>
    /// 2025.
    /// </example>
    public required int Year { get; init; }

    /// <summary>
    /// Gets the month of the record's creation.
    /// </summary>
    /// <example>
    /// 6.
    /// </example>
    public required int Month { get; init; }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the Autonomous System Number (ASN) associated with the IP address.
    /// </summary>
    /// <remarks>
    /// Often represented as a string "AS852" for example. This just stores the "number".
    /// </remarks>
    /// <example>
    /// 852.
    /// </example>
    public int? ASN { get; init; }

    /// <summary>
    /// Gets name of the ASN organization.
    /// </summary>
    /// <example>
    /// TELUS Communications Inc.
    /// </example>
    public string? ASName { get; init; }

    /// <summary>
    /// Gets organization domain of the ASN.
    /// </summary>
    /// <example>
    /// telus.com.
    /// </example>
    public string? ASDomain { get; init; }

    /// <summary>
    /// Gets aSN Type: ISP, Hosting, Education, Government or Business.
    /// </summary>
    /// <example>
    /// ISP.
    /// </example>
    public string? ASType { get; init; }

    /// <summary>
    /// Gets name of the mobile carrier organization.
    /// </summary>
    /// <example>
    /// TELUS.
    /// </example>
    public string? CarrierName { get; init; }

    /// <summary>
    /// Gets mobile Country Code (MCC) of the carrier.
    /// </summary>
    /// <example>
    /// 302.
    /// </example>
    public string? MCC { get; init; }

    /// <summary>
    /// Gets mobile Network Code (MNC) of the carrier.
    /// </summary>
    /// <example>
    /// 220.
    /// </example>
    public string? MNC { get; init; }

    /// <summary>
    /// Gets date when the IP address's ASN last changed.
    /// </summary>
    /// <example>
    /// 2024-06-01.
    /// </example>
    public DateOnly? ASChanged { get; init; }

    /// <summary>
    /// Gets date when the IP address's geolocation last changed.
    /// </summary>
    /// <example>
    /// 2024-06-01.
    /// </example>
    public DateOnly? GeoChanged { get; init; }

    /// <summary>
    /// Gets indicates whether the IP address is anonymous.
    /// </summary>
    /// <example>
    /// false.
    /// </example>
    public bool? IsAnonymous { get; init; }

    /// <summary>
    /// Gets indicates whether the IP address is an anycast IP address.
    /// </summary>
    /// <example>
    /// false.
    /// </example>
    public bool? IsAnycast { get; init; }

    /// <summary>
    /// Gets indicates whether the IP address is a hosting/cloud/data center IP address.
    /// </summary>
    /// <example>
    /// false.
    /// </example>
    public bool? IsHosting { get; init; }

    /// <summary>
    /// Gets indicates whether the IP address belongs to a mobile network.
    /// </summary>
    /// <example>
    /// true.
    /// </example>
    public bool? IsMobile { get; init; }

    /// <summary>
    /// Gets indicates whether the IP address is part of a satellite internet connection.
    /// </summary>
    /// <example>
    /// false.
    /// </example>
    public bool? IsSatellite { get; init; }

    /// <summary>
    /// Gets indicates an open web proxy IP address.
    /// </summary>
    /// <example>
    /// false.
    /// </example>
    public bool? IsProxy { get; init; }

    /// <summary>
    /// Gets indicates location preserving anonymous relay service like iCloud private relay.
    /// </summary>
    /// <example>
    /// false.
    /// </example>
    public bool? IsRelay { get; init; }

    /// <summary>
    /// Gets indicates a TOR (The Onion Router) exit node IP address.
    /// </summary>
    /// <example>
    /// false.
    /// </example>
    public bool? IsTor { get; init; }

    /// <summary>
    /// Gets indicates Virtual Private Network (VPN) service exit node IP address.
    /// </summary>
    /// <example>
    /// false.
    /// </example>
    public bool? IsVPN { get; init; }

    /// <summary>
    /// Gets the name of the privacy service provider (includes VPN, Proxy, or Relay service provider
    /// name).
    /// </summary>
    /// <example>
    /// NordVPN.
    /// </example>
    public string? PrivacyName { get; init; }

    #endregion

    #region Foreign Keys

    /// <summary>
    /// Gets foreign key to the <see cref="Location"/> entity representing the geolocation of the
    /// IP address.
    /// </summary>
    /// <example>
    /// A1B2C3D4E5F6...
    /// </example>
    public string? LocationHashId { get; init; }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets navigation property to the <see cref="Location"/> entity representing the geolocation of
    /// the IP address.
    /// </summary>
    public Location? Location { get; init; }

    #endregion

    #region Functionality

    /// <summary>
    /// Factory method to create a new <see cref="WhoIs"/> record with a computed
    /// <see cref="HashId"/> and normalized <see cref="IPAddress"/>.
    /// address.
    /// </summary>
    ///
    /// <param name="ipAddress">
    /// The IP address for the record. Required.
    /// </param>
    /// <param name="year">
    /// The year for the record. Defaults to current year.
    /// </param>
    /// <param name="month">
    /// The month for the record. Defaults to current month.
    /// </param>
    /// <param name="asn">
    /// The ASN associated with the IP address. Optional.
    /// </param>
    /// <param name="asName">
    /// The name of the ASN organization. Optional.
    /// </param>
    /// <param name="asDomain">
    /// The domain of the ASN organization. Optional.
    /// </param>
    /// <param name="asType">
    /// The ASN organization type. Optional.
    /// </param>
    /// <param name="carrierName">
    /// The mobile carrier name. Optional.
    /// </param>
    /// <param name="mcc">
    /// The MCC. Optional.
    /// </param>
    /// <param name="mnc">
    /// The MNC. Optional.
    /// </param>
    /// <param name="asChanged">
    /// When the AS last changed. Optional.
    /// </param>
    /// <param name="geoChanged">
    /// When the associated geographic location data last changed. Optional.
    /// </param>
    /// <param name="isAnonymous">
    /// Whether the IP is anonymous. Optional.
    /// </param>
    /// <param name="isAnycast">
    /// Whether the IP is anycast. Optional.
    /// </param>
    /// <param name="isHosting">
    /// Whether the IP is used for hosting. Optional.
    /// </param>
    /// <param name="isMobile">
    /// Whether the IP is registered by a mobile carrier. Optional.
    /// </param>
    /// <param name="isSatellite">
    /// Whether the IP is associated to a satellite connection. Optional.
    /// </param>
    /// <param name="isProxy">
    /// Whether the IP is a web proxy. Optional.
    /// </param>
    /// <param name="isRelay">
    /// Whether the IP is from an anonymous relay service. Optional.
    /// </param>
    /// <param name="isTor">
    /// Whether the IP is associated with a TOR exit node. Optional.
    /// </param>
    /// <param name="isVpn">
    /// Whether the IP is associated with a VPN service. Optional.
    /// </param>
    /// <param name="privacyName">
    /// The name of the VPN service. Optional.
    /// </param>
    /// <param name="locationHashId">
    /// The hash identifier of the associated location. Optional.
    /// </param>
    ///
    /// <returns>
    /// A new <see cref="WhoIs"/> record with a computed <see cref="HashId"/> and normalized
    /// <see cref="IPAddress"/>.
    /// </returns>
    ///
    /// <exception cref="GeoValidationException">
    /// Throws if the IP address is null, empty, whitespace, or not a valid IPv4 or IPv6 address or
    /// if the month is not between 1 and 12, or if the year is not between 1 and 9999.
    /// </exception>
    ///
    /// <seealso cref="ComputeHashAndNormalizeIp"/>
    public static WhoIs Create(
        string ipAddress,
        int? year = null,
        int? month = null,
        int? asn = null,
        string? asName = null,
        string? asDomain = null,
        string? asType = null,
        string? carrierName = null,
        string? mcc = null,
        string? mnc = null,
        DateOnly? asChanged = null,
        DateOnly? geoChanged = null,
        bool? isAnonymous = null,
        bool? isAnycast = null,
        bool? isHosting = null,
        bool? isMobile = null,
        bool? isSatellite = null,
        bool? isProxy = null,
        bool? isRelay = null,
        bool? isTor = null,
        bool? isVpn = null,
        string? privacyName = null,
        string? locationHashId = null)
    {
        var yearNotNull = year ?? DateTime.UtcNow.Year;
        var monthNotNull = month ?? DateTime.UtcNow.Month;

        var (hashId, normalizedIp) = ComputeHashAndNormalizeIp(
            ipAddress,
            yearNotNull,
            monthNotNull);

        var whois = new WhoIs
        {
            HashId = hashId,
            IPAddress = normalizedIp,
            Year = yearNotNull,
            Month = monthNotNull,
            ASN = asn,
            ASName = asName.CleanStr(),
            ASDomain = asDomain.CleanStr(),
            ASType = asType.CleanStr(),
            CarrierName = carrierName.CleanStr(),
            MCC = mcc,
            MNC = mnc,
            ASChanged = asChanged,
            GeoChanged = geoChanged,
            IsAnonymous = isAnonymous,
            IsAnycast = isAnycast,
            IsHosting = isHosting,
            IsMobile = isMobile,
            IsSatellite = isSatellite,
            IsProxy = isProxy,
            IsRelay = isRelay,
            IsTor = isTor,
            IsVPN = isVpn,
            PrivacyName = privacyName.CleanStr(),
            LocationHashId = locationHashId,
        };

        return whois;
    }

    /// <summary>
    /// Factory method to create a new <see cref="WhoIs"/> record with a computed
    /// <see cref="HashId"/> and normalized <see cref="IPAddress"/>.
    /// address.
    /// </summary>
    ///
    /// <param name="whois">
    /// The WhoIs record to validate and create a new instance from.
    /// </param>
    ///
    /// <returns>
    /// A new <see cref="WhoIs"/> record with a computed <see cref="HashId"/> and normalized
    /// <see cref="IPAddress"/>.
    /// </returns>
    ///
    /// <exception cref="GeoValidationException">
    /// Throws if the IP address is null, empty, whitespace, or not a valid IPv4 or IPv6 address or
    /// if the month is not between 1 and 12, or if the year is not between 1 and 9999.
    /// </exception>
    ///
    /// <seealso cref="ComputeHashAndNormalizeIp"/>
    public static WhoIs Create(WhoIs whois)
        => Create(
            whois.IPAddress,
            whois.Year,
            whois.Month,
            whois.ASN,
            whois.ASName,
            whois.ASDomain,
            whois.ASType,
            whois.CarrierName,
            whois.MCC,
            whois.MNC,
            whois.ASChanged,
            whois.GeoChanged,
            whois.IsAnonymous,
            whois.IsAnycast,
            whois.IsHosting,
            whois.IsMobile,
            whois.IsSatellite,
            whois.IsProxy,
            whois.IsRelay,
            whois.IsTor,
            whois.IsVPN,
            whois.PrivacyName,
            whois.LocationHashId);

    /// <summary>
    /// Computes the SHA-256 hash of the normalized IP address, year and month, and
    /// returns the normalized and validated IP address.
    /// </summary>
    ///
    /// <param name="ipAddress">
    /// The IP address.
    /// </param>
    /// <param name="year">
    /// The year.
    /// </param>
    /// <param name="month">
    /// The month.
    /// </param>
    ///
    /// <returns>
    /// A tuple containing the computed hash (hex string) and the normalized IP address.
    /// </returns>
    ///
    /// <exception cref="GeoValidationException">
    /// Thrown if the IP address is null, empty, whitespace, or not a valid IPv4 or IPv6 address or
    /// if the month is not between 1 and 12, or if the year is not between 1 and 9999.
    /// </exception>
    ///
    /// <seealso cref="NormalizeAndValidateIPAddress"/>
    /// <seealso cref="IsValidIpAddress"/>
    public static (string Hash, string NormalizedIp) ComputeHashAndNormalizeIp(
        string ipAddress,
        int year,
        int month)
    {
        var normalizedIp = NormalizeAndValidateIPAddress(ipAddress);

        if (month is < 1 or > 12)
        {
            throw new GeoValidationException(
                nameof(WhoIs),
                nameof(Month),
                month,
                "must be between 1 and 12.");
        }

        if (year is < 1 or > 9999)
        {
            throw new GeoValidationException(
                nameof(WhoIs),
                nameof(Year),
                year,
                "must be between 1 and 9999.");
        }

        var inputBytes = Encoding.UTF8.GetBytes($"{normalizedIp}|{year}|{month}");
        var hashId = Convert.ToHexString(SHA256.HashData(inputBytes));

        return (hashId, normalizedIp);
    }

    /// <summary>
    /// Normalizes and validates the provided IP address string.
    /// </summary>
    ///
    /// <param name="ipAddress">
    /// The IP address.
    /// </param>
    ///
    /// <returns>
    /// The normalized, validated IP address.
    /// </returns>
    ///
    /// <exception cref="GeoValidationException">
    /// Thrown if the IP address is null, empty, whitespace, or not a valid IPv4 or IPv6 address.
    /// </exception>
    public static string NormalizeAndValidateIPAddress(string ipAddress)
    {
        if (ipAddress.Falsey())
        {
            throw new GeoValidationException(
                nameof(WhoIs),
                nameof(IPAddress),
                ipAddress,
                "is required.");
        }

        ipAddress = ipAddress.Trim().ToLowerInvariant();

        if (!IsValidIpAddress(ipAddress))
        {
            throw new GeoValidationException(
                nameof(WhoIs),
                nameof(IPAddress),
                ipAddress,
                "is not a valid IPv4 or IPv6 address.");
        }

        return ipAddress;
    }

    /// <summary>
    /// Returns true if the provided string is a valid IPv4 or IPv6 address.
    /// </summary>
    ///
    /// <param name="ipAddress">
    /// The IP address.
    /// </param>
    ///
    /// <returns>
    /// Whether the string is a valid IPv4 or IPv6 address.
    /// </returns>
    private static bool IsValidIpAddress(string ipAddress)
        => System.Net.IPAddress.TryParse(ipAddress, out _);

    #endregion
}
