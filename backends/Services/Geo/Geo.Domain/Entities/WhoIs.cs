using System.Security.Cryptography;
using System.Text;
using D2.Contracts.Utilities;
using Geo.Domain.Exceptions;

// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace Geo.Domain.Entities;

/// <summary>
/// Represents WHOIS, ASN and GEOIP information associated with an IP address.
/// </summary>
/// <remarks>
/// Is an aggregate root of the Geography "Geo" Domain. Relates to the <see cref="Location"/>
/// entity via a  foreign key to describe the geographic information related to this IP address.
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
    /// A content-addressable 32-byte SHA-256 hash of the IP address, year, month and browser or
    /// device fingerprint of the record.
    /// </summary>
    public required byte[] HashId { get; init; }

    #endregion

    #region Content Addressible Properties

    /// <summary>
    /// The IP address of the record.
    /// </summary>
    /// <example>
    /// 75.155.155.200
    /// </example>
    public required string IPAddress { get; init; }

    /// <summary>
    /// The year of the record's creation.
    /// </summary>
    /// <example>
    /// 2025
    /// </example>
    public required int Year { get; init; }

    /// <summary>
    /// The month of the record's creation.
    /// </summary>
    /// <example>
    /// 6
    /// </example>
    public required int Month { get; init; }

    /// <summary>
    /// A fingerprint of the browser or device associated with the IP address.
    /// </summary>
    /// <example>
    /// Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3
    /// </example>
    /// <remarks>
    /// While optional, this will be used in the content-addressable hash if provided.
    /// </remarks>
    public string? Fingerprint { get; init; }

    #endregion

    #region Properties

    /// <summary>
    /// The Autonomous System Number (ASN) associated with the IP address.
    /// </summary>
    /// <remarks>
    /// Often represented as a string "AS852" for example. This just stores the "number".
    /// </remarks>
    /// <example>
    /// 852
    /// </example>
    public int? ASN { get; init; }

    /// <summary>
    /// Name of the ASN organization.
    /// </summary>
    /// <example>
    /// TELUS Communications Inc.
    /// </example>
    public string? ASName { get; init; }

    /// <summary>
    /// Organization domain of the ASN.
    /// </summary>
    /// <example>
    /// telus.com
    /// </example>
    public string? ASDomain { get; init; }

    /// <summary>
    /// ASN Type: ISP, Hosting, Education, Government or Business
    /// </summary>
    /// <example>
    /// ISP
    /// </example>
    public string? ASType { get; init; }

    /// <summary>
    /// Name of the mobile carrier organization.
    /// </summary>
    /// <example>
    /// TELUS
    /// </example>
    public string? CarrierName { get; init; }

    /// <summary>
    /// Mobile Country Code (MCC) of the carrier.
    /// </summary>
    /// <example>
    /// 302
    /// </example>
    public string? MCC { get; init; }

    /// <summary>
    /// Mobile Network Code (MNC) of the carrier.
    /// </summary>
    /// <example>
    /// 220
    /// </example>
    public string? MNC { get; init; }

    /// <summary>
    /// Date when the IP address's ASN last changed.
    /// </summary>
    /// <example>
    /// 2024-06-01
    /// </example>
    public DateOnly? ASChanged { get; init; }

    /// <summary>
    /// Date when the IP address's geolocation last changed.
    /// </summary>
    /// <example>
    /// 2024-06-01
    /// </example>
    public DateOnly? GeoChanged { get; init; }

    /// <summary>
    /// Indicates whether the IP address is anonymous.
    /// </summary>
    /// <example>
    /// false
    /// </example>
    public bool? IsAnonymous { get; init; }

    /// <summary>
    /// Indicates whether the IP address is an anycast IP address.
    /// </summary>
    /// <example>
    /// false
    /// </example>
    public bool? IsAnycast { get; init; }

    /// <summary>
    /// Indicates whether the IP address is a hosting/cloud/data center IP address.
    /// </summary>
    /// <example>
    /// false
    /// </example>
    public bool? IsHosting { get; init; }

    /// <summary>
    /// Indicates whether the IP address belongs to a mobile network.
    /// </summary>
    /// <example>
    /// true
    /// </example>
    public bool? IsMobile { get; init; }

    /// <summary>
    /// Indicates whether the IP address is part of a satellite internet connection.
    /// </summary>
    /// <example>
    /// false
    /// </example>
    public bool? IsSatellite { get; init; }

    /// <summary>
    /// Indicates an open web proxy IP address.
    /// </summary>
    /// <example>
    /// false
    /// </example>
    public bool? IsProxy { get; init; }

    /// <summary>
    /// Indicates location preserving anonymous relay service like iCloud private relay.
    /// </summary>
    /// <example>
    /// false
    /// </example>
    public bool? IsRelay { get; init; }

    /// <summary>
    /// Indicates a TOR (The Onion Router) exit node IP address.
    /// </summary>
    /// <example>
    /// false
    /// </example>
    public bool? IsTor { get; init; }

    /// <summary>
    /// Indicates Virtual Private Network (VPN) service exit node IP address.
    /// </summary>
    /// <example>
    /// false
    /// </example>
    public bool? IsVPN { get; init; }

    /// <summary>
    /// The name of the privacy service provider (includes VPN, Proxy, or Relay service provider
    /// name).
    /// </summary>
    /// <example>
    /// NordVPN
    /// </example>
    public string? PrivacyName { get; init; }

    #endregion

    #region Foreign Keys

    /// <summary>
    /// Foreign key to the <see cref="Location"/> entity representing the geolocation of the IP
    /// address.
    /// </summary>
    /// <example>
    /// Content-addressable 32-byte SHA-256 hash of the Location.
    /// </example>
    public byte[]? LocationHashId { get; init; }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Navigation property to the <see cref="Location"/> entity representing the geolocation of
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
    /// <param name="fingerprint">
    /// The fingerprint of the browser or device associated with the IP address. Optional.
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
    /// Whether the IP is from a anonymous relay service. Optional.
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
        // Used for hash generation.
        string ipAddress,
        int? year = null,
        int? month = null,
        string? fingerprint = null,
        // Properties.
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
        // Geolocation.
        byte[]? locationHashId = null)
    {
        var yearNotNull = year ?? DateTime.UtcNow.Year;
        var monthNotNull = month ?? DateTime.UtcNow.Month;

        var (hashId, normalizedIp) = ComputeHashAndNormalizeIp(
            ipAddress,
            yearNotNull,
            monthNotNull,
            fingerprint);

        var whois = new WhoIs
        {
            HashId = hashId,
            IPAddress = normalizedIp,
            Year = yearNotNull,
            Month = monthNotNull,
            Fingerprint = fingerprint.CleanStr(),
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
            whois.Fingerprint,
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
    /// Computes the SHA-256 hash of the normalized IP address, year, month and fingerprint and
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
    /// <param name="fingerprint">
    /// The fingerprint of the browser or device associated with the IP address. Optional.
    /// </param>
    ///
    /// <returns>
    /// A tuple containing the computed SHA-256 hash as a byte array and the normalized IP address.
    /// </returns>
    ///
    /// <exception cref="GeoValidationException">
    /// Thrown if the IP address is null, empty, whitespace, or not a valid IPv4 or IPv6 address or
    /// if the month is not between 1 and 12, or if the year is not between 1 and 9999.
    /// </exception>
    ///
    /// <seealso cref="NormalizeAndValidateIPAddress"/>
    /// <seealso cref="IsValidIpAddress"/>
    public static (byte[] hash, string normalizedIp) ComputeHashAndNormalizeIp(
        string ipAddress,
        int year,
        int month,
        string? fingerprint = null)
    {
        var normalizedIp = NormalizeAndValidateIPAddress(ipAddress);

        if (month is < 1 or > 12)
            throw new GeoValidationException(
                nameof(WhoIs),
                nameof(Month),
                month,
                "must be between 1 and 12.");

        if (year is < 1 or > 9999)
            throw new GeoValidationException(
                nameof(WhoIs),
                nameof(Year),
                year,
                "must be between 1 and 9999.");

        var inputBytes = Encoding.UTF8.GetBytes($"{normalizedIp}|{year}|{month}|{fingerprint.CleanStr()}");
        var hashId = SHA256.HashData(inputBytes);

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
            throw new GeoValidationException(
                nameof(WhoIs),
                nameof(IPAddress),
                ipAddress,
                "is required.");

        ipAddress = ipAddress.Trim().ToLowerInvariant();

        if (!IsValidIpAddress(ipAddress))
            throw new GeoValidationException(
                nameof(WhoIs),
                nameof(IPAddress),
                ipAddress,
                "is not a valid IPv4 or IPv6 address.");

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
