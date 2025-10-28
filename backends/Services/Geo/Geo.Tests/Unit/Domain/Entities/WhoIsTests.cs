using FluentAssertions;
using Geo.Domain.Entities;
using Geo.Domain.Exceptions;
using Xunit;

namespace Geo.Tests.Unit.Domain.Entities;

/// <summary>
/// Unit tests for the <see cref="WhoIs"/> entity.
/// </summary>
public class WhoIsTests
{
    #region Valid Creation - Minimal

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
        whois.HashId.Should().HaveCount(32); // SHA-256 = 32 bytes
        whois.IPAddress.Should().Be("192.168.1.1");
        whois.Year.Should().BeGreaterThan(0); // Defaults to current year
        whois.Month.Should().BeInRange(1, 12); // Defaults to current month
        whois.Fingerprint.Should().BeNull();
        whois.ASN.Should().BeNull();
    }

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

    [Fact]
    public void Create_WithFingerprint_Success()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        const string fingerprint = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

        // Act
        var whois = WhoIs.Create(ip_address, fingerprint: fingerprint);

        // Assert
        whois.Fingerprint.Should().Be(fingerprint);
    }

    #endregion

    #region Valid Creation - With Properties

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

    [Fact]
    public void Create_WithLocationHashId_Success()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        byte[] locationHashId = new byte[32];

        // Act
        var whois = WhoIs.Create(ip_address, locationHashId: locationHashId);

        // Assert
        whois.LocationHashId.Should().BeEquivalentTo(locationHashId);
    }

    #endregion

    #region IP Address Validation

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
        whois1.HashId.Should().BeEquivalentTo(whois2.HashId);
    }

    [Fact]
    public void Create_WithSameIPYearMonthAndFingerprint_GeneratesSameHash()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        const int year = 2025;
        const int month = 6;
        const string fingerprint = "Mozilla/5.0";

        // Act
        var whois1 = WhoIs.Create(ip_address, year, month, fingerprint);
        var whois2 = WhoIs.Create(ip_address, year, month, fingerprint);

        // Assert
        whois1.HashId.Should().BeEquivalentTo(whois2.HashId);
    }

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
        whois1.HashId.Should().BeEquivalentTo(whois2.HashId);
    }

    #endregion

    #region Content-Addressable - Different Input = Different Hash

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
        whois1.HashId.Should().NotBeEquivalentTo(whois2.HashId);
    }

    [Fact]
    public void Create_WithDifferentYear_GeneratesDifferentHash()
    {
        // Arrange
        const string ip_address = "192.168.1.1";

        // Act
        var whois1 = WhoIs.Create(ip_address, 2024, 6);
        var whois2 = WhoIs.Create(ip_address, 2025, 6);

        // Assert
        whois1.HashId.Should().NotBeEquivalentTo(whois2.HashId);
    }

    [Fact]
    public void Create_WithDifferentMonth_GeneratesDifferentHash()
    {
        // Arrange
        const string ip_address = "192.168.1.1";

        // Act
        var whois1 = WhoIs.Create(ip_address, 2025, 5);
        var whois2 = WhoIs.Create(ip_address, 2025, 6);

        // Assert
        whois1.HashId.Should().NotBeEquivalentTo(whois2.HashId);
    }

    [Fact]
    public void Create_WithDifferentFingerprint_GeneratesDifferentHash()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        const string fingerprint1 = "Mozilla/5.0";
        const string fingerprint2 = "Chrome/91.0";

        // Act
        var whois1 = WhoIs.Create(ip_address, 2025, 6, fingerprint1);
        var whois2 = WhoIs.Create(ip_address, 2025, 6, fingerprint2);

        // Assert
        whois1.HashId.Should().NotBeEquivalentTo(whois2.HashId);
    }

    [Fact]
    public void Create_WithFingerprintVsNull_GeneratesDifferentHash()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        const string fingerprint = "Mozilla/5.0";

        // Act
        var whois1 = WhoIs.Create(ip_address, 2025, 6, fingerprint);
        var whois2 = WhoIs.Create(ip_address, 2025, 6);

        // Assert
        whois1.HashId.Should().NotBeEquivalentTo(whois2.HashId);
    }

    #endregion

    #region Temporal Versioning

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
        january.HashId.Should().NotBeEquivalentTo(february.HashId);
        february.HashId.Should().NotBeEquivalentTo(march.HashId);
        january.HashId.Should().NotBeEquivalentTo(march.HashId);
    }

    [Fact]
    public void Create_SameIPDifferentYears_CreatesDistinctRecords()
    {
        // Arrange
        const string ip_address = "192.168.1.1";

        // Act
        var year2024 = WhoIs.Create(ip_address, 2024, 6);
        var year2025 = WhoIs.Create(ip_address, 2025, 6);

        // Assert
        year2024.HashId.Should().NotBeEquivalentTo(year2025.HashId);
    }

    #endregion

    #region Device Differentiation

    [Fact]
    public void Create_SameIPDifferentFingerprints_CreatesDistinctRecords()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        const string device1_fingerprint = "Mozilla/5.0 (Windows NT 10.0)";
        const string device2_fingerprint = "Mozilla/5.0 (Macintosh; Intel Mac OS X)";

        // Act
        var device1 = WhoIs.Create(ip_address, 2025, 6, device1_fingerprint);
        var device2 = WhoIs.Create(ip_address, 2025, 6, device2_fingerprint);

        // Assert - Multiple devices behind same IP get separate records
        device1.HashId.Should().NotBeEquivalentTo(device2.HashId);
    }

    #endregion

    #region Create Overload Tests

    [Fact]
    public void Create_WithExistingWhoIs_CreatesNewInstanceWithSameHash()
    {
        // Arrange
        var original = WhoIs.Create(
            "192.168.1.1",
            2025,
            6,
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
            asn: 852,
            asName: "TELUS Communications Inc.",
            isVpn: true);

        // Act
        var copy = WhoIs.Create(original);

        // Assert
        copy.Should().NotBeNull();
        copy.HashId.Should().BeEquivalentTo(original.HashId);
        copy.IPAddress.Should().Be(original.IPAddress);
        copy.Year.Should().Be(original.Year);
        copy.Month.Should().Be(original.Month);
        copy.Fingerprint.Should().Be(original.Fingerprint);
        copy.ASN.Should().Be(original.ASN);
        copy.ASName.Should().Be(original.ASName);
        copy.IsVPN.Should().Be(original.IsVPN);
    }

    #endregion

    #region Hash Determinism

    [Fact]
    public void Create_CalledMultipleTimes_GeneratesConsistentHash()
    {
        // Arrange
        const string ip_address = "192.168.1.1";
        const int year = 2025;
        const int month = 6;
        const string fingerprint = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";

        // Act - Create same WhoIs 5 times
        var whoisRecords = Enumerable.Range(0, 5)
            .Select(_ => WhoIs.Create(ip_address, year, month, fingerprint))
            .ToList();

        // Assert - All should have identical hashes
        var firstHash = whoisRecords[0].HashId;
        whoisRecords.Should().AllSatisfy(whois => whois.HashId.Should().BeEquivalentTo(firstHash));
    }

    #endregion

    #region ComputeHashAndNormalizeIp Tests

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
        hash.Should().HaveCount(32);
        normalizedIp.Should().Be("192.168.1.1");
    }

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
        hash1.Should().BeEquivalentTo(hash2);
    }

    #endregion

    #region NormalizeAndValidateIPAddress Tests

    [Theory]
    [InlineData("192.168.1.1", "192.168.1.1")]
    [InlineData("  192.168.1.1  ", "192.168.1.1")]
    [InlineData("2001:0DB8::1", "2001:0db8::1")]
    public void NormalizeAndValidateIPAddress_WithValidIP_ReturnsNormalized(string input, string expected)
    {
        // Act
        var normalized = WhoIs.NormalizeAndValidateIPAddress(input);

        // Assert
        normalized.Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void NormalizeAndValidateIPAddress_WithInvalidIP_ThrowsGeoValidationException(string? ipAddress)
    {
        // Act
        var act = () => WhoIs.NormalizeAndValidateIPAddress(ipAddress!);

        // Assert
        act.Should().Throw<GeoValidationException>();
    }

    #endregion
}
