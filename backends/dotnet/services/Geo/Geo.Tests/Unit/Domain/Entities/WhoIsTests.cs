// -----------------------------------------------------------------------
// <copyright file="WhoIsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Tests.Unit.Domain.Entities;

using D2.Geo.Domain.Entities;
using D2.Geo.Domain.Exceptions;
using FluentAssertions;
using Xunit;

/// <summary>
/// Unit tests for the <see cref="WhoIs"/> entity.
/// </summary>
public class WhoIsTests
{
    #region Valid Creation - Minimal

    /// <summary>
    /// Tests creating a WhoIs instance with only an IP address.
    /// </summary>
    [Fact]
    public void Create_WithIPAddressOnly_Success()
    {
        // Arrange
        const string ip_address = "192.168.1.1";

        // Act
        var whois = WhoIs.Create(ip_address);

        // Assert
        whois.Should().NotBeNull();
        whois.HashId.Should().NotBeNull();
        whois.HashId.Should().HaveLength(64); // SHA-256 = 32 bytes = 64 hex chars
        whois.IPAddress.Should().Be("192.168.1.1");
        whois.Year.Should().BeGreaterThan(0); // Defaults to current year
        whois.Month.Should().BeInRange(1, 12); // Defaults to current month
        whois.ASN.Should().BeNull();
    }

    /// <summary>
    /// Tests creating a WhoIs instance with an IP address, year, and month.
    /// </summary>
    [Fact]
    public void Create_WithYearAndMonth_Success()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        const int year = 2025;
        const int month = 6;

        // Act
        var whois = WhoIs.Create(ip_address, year, month);

        // Assert
        whois.IPAddress.Should().Be("192.168.1.1");
        whois.Year.Should().Be(2025);
        whois.Month.Should().Be(6);
    }

    #endregion

    #region Valid Creation - With Properties

    /// <summary>
    /// Tests creating a WhoIs instance with ASN-related properties.
    /// </summary>
    [Fact]
    public void Create_WithASNProperties_Success()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        const int asn = 852;
        const string as_name = "TELUS Communications Inc.";
        const string as_domain = "telus.com";
        const string as_type = "ISP";

        // Act
        var whois = WhoIs.Create(
            ip_address,
            asn: asn,
            asName: as_name,
            asDomain: as_domain,
            asType: as_type);

        // Assert
        whois.ASN.Should().Be(852);
        whois.ASName.Should().Be("TELUS Communications Inc.");
        whois.ASDomain.Should().Be("telus.com");
        whois.ASType.Should().Be("ISP");
    }

    /// <summary>
    /// Tests creating a WhoIs instance with network flags.
    /// </summary>
    [Fact]
    public void Create_WithNetworkFlags_Success()
    {
        // Arrange
        const string ip_address = "192.168.1.1";

        // Act
        var whois = WhoIs.Create(
            ip_address,
            isAnonymous: true,
            isVpn: true,
            isTor: false,
            isProxy: false);

        // Assert
        whois.IsAnonymous.Should().BeTrue();
        whois.IsVPN.Should().BeTrue();
        whois.IsTor.Should().BeFalse();
        whois.IsProxy.Should().BeFalse();
    }

    /// <summary>
    /// Tests creating a WhoIs instance with a location hash ID.
    /// </summary>
    [Fact]
    public void Create_WithLocationHashId_Success()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        const string location_hash_id = "A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2";

        // Act
        var whois = WhoIs.Create(ip_address, locationHashId: location_hash_id);

        // Assert
        whois.LocationHashId.Should().Be(location_hash_id);
    }

    #endregion

    #region IP Address Validation

    /// <summary>
    /// Tests creating a WhoIs instance with valid IPv4 addresses.
    /// </summary>
    ///
    /// <param name="ipAddress">
    /// The valid IPv4 address to test.
    /// </param>
    [Theory]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("0.0.0.0")]
    public void Create_WithValidIPv4_Success(string ipAddress)
    {
        // Act
        var whois = WhoIs.Create(ipAddress);

        // Assert
        whois.IPAddress.Should().Be(ipAddress);
    }

    /// <summary>
    /// Tests creating a WhoIs instance with valid IPv6 addresses.
    /// </summary>
    ///
    /// <param name="input">
    /// The valid IPv6 address to test.
    /// </param>
    /// <param name="expected">
    /// The expected normalized IPv6 address.
    /// </param>
    [Theory]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", "2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    [InlineData("::1", "::1")]
    [InlineData("fe80::1", "fe80::1")]
    public void Create_WithValidIPv6_Success(string input, string expected)
    {
        // Act
        var whois = WhoIs.Create(input);

        // Assert
        whois.IPAddress.Should().Be(expected);
    }

    /// <summary>
    /// Tests creating a WhoIs instance with null or empty IP addresses.
    /// </summary>
    ///
    /// <param name="ipAddress">
    /// The null or empty IP address to test.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Create_WithNullOrEmptyIPAddress_ThrowsGeoValidationException(string? ipAddress)
    {
        // Act
        var act = () => WhoIs.Create(ipAddress!);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    /// <summary>
    /// Tests creating a WhoIs instance with invalid IP addresses.
    /// </summary>
    ///
    /// <param name="ipAddress">
    /// The invalid IP address to test.
    /// </param>
    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("999.999.999.999")]
    [InlineData("192.168.1.1.1")]
    [InlineData("gggg::1")]
    public void Create_WithInvalidIPAddress_ThrowsGeoValidationException(string ipAddress)
    {
        // Act
        var act = () => WhoIs.Create(ipAddress);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion

    #region IP Address Normalization

    /// <summary>
    /// Tests creating a WhoIs instance with a clean IPv4 address.
    /// </summary>
    [Fact]
    public void Create_WithCleanIPv4_NoChanges()
    {
        // Arrange
        const string clean_ip = "192.168.1.1";

        // Act
        var whois = WhoIs.Create(clean_ip);

        // Assert
        whois.IPAddress.Should().Be(clean_ip);
    }

    /// <summary>
    /// Tests creating a WhoIs instance with a dirty IPv4 address.
    /// </summary>
    [Fact]
    public void Create_WithDirtyIPv4_CleansAndLowercases()
    {
        // Arrange
        const string dirty_ip = "  192.168.1.1  ";

        // Act
        var whois = WhoIs.Create(dirty_ip);

        // Assert
        whois.IPAddress.Should().Be("192.168.1.1");
    }

    /// <summary>
    /// Tests creating a WhoIs instance with an uppercase IPv6 address.
    /// </summary>
    [Fact]
    public void Create_WithUppercaseIPv6_Lowercases()
    {
        // Arrange
        const string uppercase_ip = "2001:0DB8:85A3::8A2E:0370:7334";

        // Act
        var whois = WhoIs.Create(uppercase_ip);

        // Assert
        whois.IPAddress.Should().Be("2001:0db8:85a3::8a2e:0370:7334");
    }

    #endregion

    #region Year Validation

    /// <summary>
    /// Tests creating a WhoIs instance with valid year values.
    /// </summary>
    ///
    /// <param name="year">
    /// The valid year to test.
    /// </param>
    [Theory]
    [InlineData(1)]
    [InlineData(2000)]
    [InlineData(2025)]
    [InlineData(9999)]
    public void Create_WithValidYear_Success(int year)
    {
        // Arrange
        const string ip_address = "192.168.1.1";

        // Act
        var whois = WhoIs.Create(ip_address, year);

        // Assert
        whois.Year.Should().Be(year);
    }

    /// <summary>
    /// Tests creating a WhoIs instance with invalid year values.
    /// </summary>
    ///
    /// <param name="year">
    /// The invalid year to test.
    /// </param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000)]
    public void Create_WithInvalidYear_ThrowsGeoValidationException(int year)
    {
        // Arrange
        const string ip_address = "192.168.1.1";

        // Act
        var act = () => WhoIs.Create(ip_address, year);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion

    #region Month Validation

    /// <summary>
    /// Tests creating a WhoIs instance with valid month values.
    /// </summary>
    ///
    /// <param name="month">
    /// The valid month to test.
    /// </param>
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public void Create_WithValidMonth_Success(int month)
    {
        // Arrange
        const string ip_address = "192.168.1.1";

        // Act
        var whois = WhoIs.Create(ip_address, month: month);

        // Assert
        whois.Month.Should().Be(month);
    }

    /// <summary>
    /// Tests creating a WhoIs instance with invalid month values.
    /// </summary>
    ///
    /// <param name="month">
    /// The invalid month to test.
    /// </param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(100)]
    public void Create_WithInvalidMonth_ThrowsGeoValidationException(int month)
    {
        // Arrange
        const string ip_address = "192.168.1.1";

        // Act
        var act = () => WhoIs.Create(ip_address, month: month);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion

    #region String Cleanup

    /// <summary>
    /// Tests creating a WhoIs instance with dirty ASName and ASDomain values.
    /// </summary>
    [Fact]
    public void Create_WithDirtyASName_CleansWhitespace()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        const string dirty_as_name = "  TELUS   Communications   Inc.  ";

        // Act
        var whois = WhoIs.Create(ip_address, asName: dirty_as_name);

        // Assert
        whois.ASName.Should().Be("TELUS Communications Inc.");
    }

    /// <summary>
    /// Tests creating a WhoIs instance with dirty ASDomain value.
    /// </summary>
    [Fact]
    public void Create_WithDirtyASDomain_CleansWhitespace()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        const string dirty_as_domain = "  telus.com  ";

        // Act
        var whois = WhoIs.Create(ip_address, asDomain: dirty_as_domain);

        // Assert
        whois.ASDomain.Should().Be("telus.com");
    }

    /// <summary>
    /// Tests creating a WhoIs instance with whitespace-only ASName.
    /// </summary>
    ///
    /// <param name="asName">
    /// The ASName value containing only whitespace to test.
    /// </param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Create_WithWhitespaceOnlyASName_SetsToNull(string asName)
    {
        // Arrange
        const string ip_address = "192.168.1.1";

        // Act
        var whois = WhoIs.Create(ip_address, asName: asName);

        // Assert
        whois.ASName.Should().BeNull();
    }

    #endregion

    #region Content-Addressable - Same Input = Same Hash

    /// <summary>
    /// Tests that creating WhoIs instances with the same IP, year, and month generates the same
    /// hash.
    /// </summary>
    [Fact]
    public void Create_WithSameIPYearMonth_GeneratesSameHash()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        const int year = 2025;
        const int month = 6;

        // Act
        var whois1 = WhoIs.Create(ip_address, year, month);
        var whois2 = WhoIs.Create(ip_address, year, month);

        // Assert
        whois1.HashId.Should().Be(whois2.HashId);
    }

    /// <summary>
    /// Tests that creating WhoIs instances with the same IP but different surrounding whitespace
    /// generates the same hash.
    /// </summary>
    [Fact]
    public void Create_WithSameIPDifferentWhitespace_GeneratesSameHash()
    {
        // Arrange
        const string ip1 = "192.168.1.1";
        const string ip2 = "  192.168.1.1  ";

        // Act
        var whois1 = WhoIs.Create(ip1, 2025, 6);
        var whois2 = WhoIs.Create(ip2, 2025, 6);

        // Assert
        whois1.HashId.Should().Be(whois2.HashId);
    }

    #endregion

    #region Content-Addressable - Different Input = Different Hash

    /// <summary>
    /// Tests that creating WhoIs instances with different IPs generates different hashes.
    /// </summary>
    [Fact]
    public void Create_WithDifferentIP_GeneratesDifferentHash()
    {
        // Arrange
        const string ip1 = "192.168.1.1";
        const string ip2 = "192.168.1.2";

        // Act
        var whois1 = WhoIs.Create(ip1, 2025, 6);
        var whois2 = WhoIs.Create(ip2, 2025, 6);

        // Assert
        whois1.HashId.Should().NotBe(whois2.HashId);
    }

    /// <summary>
    /// Tests that creating WhoIs instances with different years generates different hashes.
    /// </summary>
    [Fact]
    public void Create_WithDifferentYear_GeneratesDifferentHash()
    {
        // Arrange
        const string ip_address = "192.168.1.1";

        // Act
        var whois1 = WhoIs.Create(ip_address, 2024, 6);
        var whois2 = WhoIs.Create(ip_address, 2025, 6);

        // Assert
        whois1.HashId.Should().NotBe(whois2.HashId);
    }

    /// <summary>
    /// Tests that creating WhoIs instances with different months generates different hashes.
    /// </summary>
    [Fact]
    public void Create_WithDifferentMonth_GeneratesDifferentHash()
    {
        // Arrange
        const string ip_address = "192.168.1.1";

        // Act
        var whois1 = WhoIs.Create(ip_address, 2025, 5);
        var whois2 = WhoIs.Create(ip_address, 2025, 6);

        // Assert
        whois1.HashId.Should().NotBe(whois2.HashId);
    }

    #endregion

    #region Temporal Versioning

    /// <summary>
    /// Tests that creating WhoIs instances with the same IP but different months creates distinct
    /// records.
    /// </summary>
    [Fact]
    public void Create_SameIPDifferentMonths_CreatesDistinctRecords()
    {
        // Arrange
        const string ip_address = "192.168.1.1";

        // Act
        var january = WhoIs.Create(ip_address, 2025, 1);
        var february = WhoIs.Create(ip_address, 2025, 2);
        var march = WhoIs.Create(ip_address, 2025, 3);

        // Assert - Each month creates a new record
        january.HashId.Should().NotBe(february.HashId);
        february.HashId.Should().NotBe(march.HashId);
        january.HashId.Should().NotBe(march.HashId);
    }

    /// <summary>
    /// Tests that creating WhoIs instances with the same IP but different years creates distinct
    /// records.
    /// </summary>
    [Fact]
    public void Create_SameIPDifferentYears_CreatesDistinctRecords()
    {
        // Arrange
        const string ip_address = "192.168.1.1";

        // Act
        var year2024 = WhoIs.Create(ip_address, 2024, 6);
        var year2025 = WhoIs.Create(ip_address, 2025, 6);

        // Assert
        year2024.HashId.Should().NotBe(year2025.HashId);
    }

    #endregion

    #region Create Overload Tests

    /// <summary>
    /// Tests creating a WhoIs instance from an existing WhoIs instance.
    /// </summary>
    [Fact]
    public void Create_WithExistingWhoIs_CreatesNewInstanceWithSameHash()
    {
        // Arrange
        var original = WhoIs.Create(
            "192.168.1.1",
            2025,
            6,
            asn: 852,
            asName: "TELUS Communications Inc.",
            isVpn: true);

        // Act
        var copy = WhoIs.Create(original);

        // Assert
        copy.Should().NotBeNull();
        copy.HashId.Should().Be(original.HashId);
        copy.IPAddress.Should().Be(original.IPAddress);
        copy.Year.Should().Be(original.Year);
        copy.Month.Should().Be(original.Month);
        copy.ASN.Should().Be(original.ASN);
        copy.ASName.Should().Be(original.ASName);
        copy.IsVPN.Should().Be(original.IsVPN);
    }

    #endregion

    #region Hash Determinism

    /// <summary>
    /// Tests that multiple calls to Create with the same inputs produce consistent hashes.
    /// </summary>
    [Fact]
    public void Create_CalledMultipleTimes_GeneratesConsistentHash()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        const int year = 2025;
        const int month = 6;

        // Act - Create same WhoIs 5 times
        var whoisRecords = Enumerable.Range(0, 5)
            .Select(_ => WhoIs.Create(ip_address, year, month))
            .ToList();

        // Assert - All should have identical hashes
        var firstHash = whoisRecords[0].HashId;
        whoisRecords.Should().AllSatisfy(whois => whois.HashId.Should().Be(firstHash));
    }

    #endregion

    #region ComputeHashAndNormalizeIp Tests

    /// <summary>
    /// Tests ComputeHashAndNormalizeIp with valid inputs.
    /// </summary>
    [Fact]
    public void ComputeHashAndNormalizeIp_WithValidInputs_ReturnsHashAndNormalizedIP()
    {
        // Arrange
        const string ip_address = "  192.168.1.1  ";
        const int year = 2025;
        const int month = 6;

        // Act
        var (hash, normalizedIp) = WhoIs.ComputeHashAndNormalizeIp(ip_address, year, month);

        // Assert
        hash.Should().HaveLength(64); // 32 bytes = 64 hex chars
        normalizedIp.Should().Be("192.168.1.1");
    }

    /// <summary>
    /// Tests ComputeHashAndNormalizeIp with the same inputs returns the same hash.
    /// </summary>
    [Fact]
    public void ComputeHashAndNormalizeIp_WithSameInputs_ReturnsSameHash()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        const int year = 2025;
        const int month = 6;

        // Act
        var (hash1, _) = WhoIs.ComputeHashAndNormalizeIp(ip_address, year, month);
        var (hash2, _) = WhoIs.ComputeHashAndNormalizeIp(ip_address, year, month);

        // Assert
        hash1.Should().Be(hash2);
    }

    #endregion

    #region NormalizeAndValidateIPAddress Tests

    /// <summary>
    /// Tests NormalizeAndValidateIPAddress with valid IP addresses.
    /// </summary>
    ///
    /// <param name="input">
    /// The input IP address to test.
    /// </param>
    /// <param name="expected">
    /// The expected normalized IP address.
    /// </param>
    [Theory]
    [InlineData("192.168.1.1", "192.168.1.1")]
    [InlineData("  192.168.1.1  ", "192.168.1.1")]
    [InlineData("2001:0DB8::1", "2001:0db8::1")]
    public void NormalizeAndValidateIPAddress_WithValidIP_ReturnsNormalized(
        string input,
        string expected)
    {
        // Act
        var normalized = WhoIs.NormalizeAndValidateIPAddress(input);

        // Assert
        normalized.Should().Be(expected);
    }

    /// <summary>
    /// Tests NormalizeAndValidateIPAddress with invalid IP addresses.
    /// </summary>
    ///
    /// <param name="ipAddress">
    /// The invalid IP address to test.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void NormalizeAndValidateIPAddress_WithInvalidIP_ThrowsGeoValidationException(
        string? ipAddress)
    {
        // Act
        var act = () => WhoIs.NormalizeAndValidateIPAddress(ipAddress!);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion
}
