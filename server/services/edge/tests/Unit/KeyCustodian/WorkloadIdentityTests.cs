// -----------------------------------------------------------------------
// <copyright file="WorkloadIdentityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// Adversarial unit tests for <see cref="WorkloadIdentity"/> — the issuance-side
/// <see cref="WorkloadIdentity.Create"/> validation, the peer-validation-side
/// <see cref="WorkloadIdentity.Parse"/> SPIFFE grammar (default-deny), the
/// computed <see cref="WorkloadIdentity.Uri"/> round-trip, and
/// <see cref="WorkloadIdentity.FromTrusted"/>.
/// </summary>
public sealed class WorkloadIdentityTests
{
    // -----------------------------------------------------------------------
    // Create — valid input
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("edge")]
    [InlineData("files")]
    [InlineData("a")]
    [InlineData("my-service-01")]
    public void Create_ValidServiceId_ReturnsOk(string serviceId)
    {
        var result = WorkloadIdentity.Create(serviceId);
        result.Success.Should().BeTrue();
        result.Data!.ServiceId.Should().Be(serviceId);
    }

    [Fact]
    public void Create_MaxLengthServiceId_ReturnsOk()
    {
        var serviceId = new string('a', 64);
        var result = WorkloadIdentity.Create(serviceId);
        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Create_UpperCase_NormalizesToLowercase()
    {
        var result = WorkloadIdentity.Create(" EDGE ");
        result.Success.Should().BeTrue();
        result.Data!.ServiceId.Should().Be("edge");
    }

    // -----------------------------------------------------------------------
    // Create — null / empty / whitespace / over-length
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NullEmptyWhitespace_ReturnsInvalidWorkloadIdentity(string? serviceId)
    {
        var result = WorkloadIdentity.Create(serviceId);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Create_OverMaxLength_ReturnsInvalidWorkloadIdentity()
    {
        var result = WorkloadIdentity.Create(new string('a', 65));
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY);
    }

    // -----------------------------------------------------------------------
    // Create — invalid charset (lowercase DNS-label only)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("svc with spaces")]
    [InlineData("svc/slash")]
    [InlineData("svc.dot")]
    [InlineData("svc_underscore")]
    [InlineData("svc@at")]
    [InlineData("svcé")]
    [InlineData("svc:colon")]
    public void Create_InvalidCharset_ReturnsInvalidWorkloadIdentity(string serviceId)
    {
        var result = WorkloadIdentity.Create(serviceId);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY);
    }

    [Theory]
    [InlineData("Edge", "edge")]
    [InlineData("FILES", "files")]
    public void Create_MixedOrUpperCase_NormalizesToLowercase(string input, string expected)
    {
        // Uppercase is normalized (lowercased), not rejected — the same convenience
        // KeyDomain.Create offers. The charset check runs AFTER normalization.
        var result = WorkloadIdentity.Create(input);
        result.Success.Should().BeTrue();
        result.Data!.ServiceId.Should().Be(expected);
    }

    // -----------------------------------------------------------------------
    // Uri — computed SPIFFE SAN
    // -----------------------------------------------------------------------

    [Fact]
    public void Uri_ValidIdentity_ProducesSpiffeFormat()
    {
        var identity = WorkloadIdentity.Create("edge").Data!;
        identity.Uri.Should().Be("spiffe://d2.internal/workload/edge");
    }

    [Fact]
    public void Uri_RoundTripsThroughParse()
    {
        var identity = WorkloadIdentity.Create("files").Data!;
        var parsed = WorkloadIdentity.Parse(identity.Uri);
        parsed.Success.Should().BeTrue();
        parsed.Data!.ServiceId.Should().Be("files");
    }

    // -----------------------------------------------------------------------
    // Parse — valid SPIFFE URI
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_ValidSpiffeUri_ReturnsOkWithServiceId()
    {
        var result = WorkloadIdentity.Parse("spiffe://d2.internal/workload/edge");
        result.Success.Should().BeTrue();
        result.Data!.ServiceId.Should().Be("edge");
    }

    // -----------------------------------------------------------------------
    // Parse — default-deny (the R3 fail-open guard)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-uri")]
    [InlineData("spiffe://evil.internal/workload/edge")] // wrong trust domain
    [InlineData("https://d2.internal/workload/edge")] // wrong scheme
    [InlineData("spiffe://d2.internal/svc/edge")] // missing /workload/ path
    [InlineData("spiffe://d2.internal/workload/")] // empty workload segment
    [InlineData("spiffe://d2.internal/workload/svc_underscore")] // invalid charset
    [InlineData("spiffe://d2.internal.evil.com/workload/edge")] // trust-domain suffix attack
    public void Parse_Adversarial_ReturnsInvalidWorkloadIdentity(string? uri)
    {
        var result = WorkloadIdentity.Parse(uri);
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY);
    }

    // -----------------------------------------------------------------------
    // FromTrusted
    // -----------------------------------------------------------------------

    [Fact]
    public void FromTrusted_ValidServiceId_WrapsVerbatim()
    {
        var identity = WorkloadIdentity.FromTrusted("edge");
        identity.ServiceId.Should().Be("edge");
    }

    [Fact]
    public void FromTrusted_Null_ThrowsArgumentNullException()
    {
        var act = () => WorkloadIdentity.FromTrusted(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromTrusted_Empty_ThrowsArgumentException()
    {
        var act = () => WorkloadIdentity.FromTrusted(string.Empty);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromTrusted_Whitespace_ThrowsArgumentException()
    {
        var act = () => WorkloadIdentity.FromTrusted("   ");
        act.Should().Throw<ArgumentException>();
    }

    // Gate-intact pin: FromTrusted bypasses validation, Create still rejects
    [Fact]
    public void FromTrusted_AcceptsInvalidCharset_CreateRejectsIt()
    {
        var trusted = WorkloadIdentity.FromTrusted("svc with spaces");
        trusted.ServiceId.Should().Be("svc with spaces");

        WorkloadIdentity.Create("svc with spaces").Success.Should().BeFalse();
    }

    [Fact]
    public void Constants_PinTheSpiffeWireFormat()
    {
        WorkloadIdentity.SCHEME.Should().Be("spiffe");
        WorkloadIdentity.TRUST_DOMAIN.Should().Be("d2.internal");
        WorkloadIdentity.WORKLOAD_PATH_PREFIX.Should().Be("/workload/");
    }
}
