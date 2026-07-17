// -----------------------------------------------------------------------
// <copyright file="SpiffeWorkloadIdentityTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.WorkloadIdentity;

using AwesomeAssertions;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.Spiffe;
using Xunit;

/// <summary>
/// Adversarial unit tests for the shared SPIFFE grammar
/// <see cref="SpiffeWorkloadIdentity"/> — the issuance-side
/// <see cref="SpiffeWorkloadIdentity.Create"/> validation, the
/// peer-validation-side <see cref="SpiffeWorkloadIdentity.Parse"/> SPIFFE grammar
/// (default-deny), the computed <see cref="SpiffeWorkloadIdentity.Uri"/>
/// round-trip, and <see cref="SpiffeWorkloadIdentity.FromTrusted"/>. The shared VO
/// asserts the GENERIC <c>ValidationFailed</c> — domain-specific re-mapping (e.g.
/// KeyCustodian's <c>KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY</c>) is the consumer's
/// concern, covered by the KeyCustodian-side test.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SpiffeWorkloadIdentityTests
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
        var result = SpiffeWorkloadIdentity.Create(serviceId);

        result.Success.Should().BeTrue();
        result.Data!.ServiceId.Should().Be(serviceId);
    }

    [Fact]
    public void Create_MaxLengthServiceId_ReturnsOk()
    {
        var serviceId = new string('a', 64);

        var result = SpiffeWorkloadIdentity.Create(serviceId);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Create_UpperCase_NormalizesToLowercase()
    {
        var result = SpiffeWorkloadIdentity.Create(" EDGE ");

        result.Success.Should().BeTrue();
        result.Data!.ServiceId.Should().Be("edge");
    }

    [Theory]
    [InlineData("Edge", "edge")]
    [InlineData("FILES", "files")]
    public void Create_MixedOrUpperCase_NormalizesToLowercase(string input, string expected)
    {
        // Uppercase is normalized (lowercased), not rejected. The charset check
        // runs AFTER normalization.
        var result = SpiffeWorkloadIdentity.Create(input);

        result.Success.Should().BeTrue();
        result.Data!.ServiceId.Should().Be(expected);
    }

    // -----------------------------------------------------------------------
    // Create — null / empty / whitespace / over-length → generic ValidationFailed
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NullEmptyWhitespace_ReturnsGenericValidationFailed(string? serviceId)
    {
        var result = SpiffeWorkloadIdentity.Create(serviceId);

        result.Success.Should().BeFalse();
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Create_OverMaxLength_ReturnsGenericValidationFailed()
    {
        var result = SpiffeWorkloadIdentity.Create(new string('a', 65));

        result.Success.Should().BeFalse();
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
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
    public void Create_InvalidCharset_ReturnsGenericValidationFailed(string serviceId)
    {
        var result = SpiffeWorkloadIdentity.Create(serviceId);

        result.Success.Should().BeFalse();
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // Uri — computed SPIFFE SAN
    // -----------------------------------------------------------------------

    [Fact]
    public void Uri_ValidIdentity_ProducesSpiffeFormat()
    {
        var identity = SpiffeWorkloadIdentity.Create("edge").Data!;

        identity.Uri.Should().Be("spiffe://d2.internal/workload/edge");
    }

    [Fact]
    public void Uri_RoundTripsThroughParse()
    {
        var identity = SpiffeWorkloadIdentity.Create("files").Data!;

        var parsed = SpiffeWorkloadIdentity.Parse(identity.Uri);

        parsed.Success.Should().BeTrue();
        parsed.Data!.ServiceId.Should().Be("files");
    }

    // -----------------------------------------------------------------------
    // Parse — valid SPIFFE URI
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_ValidSpiffeUri_ReturnsOkWithServiceId()
    {
        var result = SpiffeWorkloadIdentity.Parse("spiffe://d2.internal/workload/edge");

        result.Success.Should().BeTrue();
        result.Data!.ServiceId.Should().Be("edge");
    }

    // -----------------------------------------------------------------------
    // Parse — default-deny (the fail-open guard)
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
    public void Parse_Adversarial_ReturnsGenericValidationFailed(string? uri)
    {
        var result = SpiffeWorkloadIdentity.Parse(uri);

        result.Success.Should().BeFalse();
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    // -----------------------------------------------------------------------
    // FromTrusted
    // -----------------------------------------------------------------------

    [Fact]
    public void FromTrusted_ValidServiceId_WrapsVerbatim()
    {
        var identity = SpiffeWorkloadIdentity.FromTrusted("edge");

        identity.ServiceId.Should().Be("edge");
    }

    [Fact]
    public void FromTrusted_Null_ThrowsArgumentNullException()
    {
        var act = () => SpiffeWorkloadIdentity.FromTrusted(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FromTrusted_Empty_ThrowsArgumentException()
    {
        var act = () => SpiffeWorkloadIdentity.FromTrusted(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromTrusted_Whitespace_ThrowsArgumentException()
    {
        var act = () => SpiffeWorkloadIdentity.FromTrusted("   ");

        act.Should().Throw<ArgumentException>();
    }

    // Gate-intact pin: FromTrusted bypasses validation, Create still rejects.
    [Fact]
    public void FromTrusted_AcceptsInvalidCharset_CreateRejectsIt()
    {
        var trusted = SpiffeWorkloadIdentity.FromTrusted("svc with spaces");
        trusted.ServiceId.Should().Be("svc with spaces");

        SpiffeWorkloadIdentity.Create("svc with spaces").Success.Should().BeFalse();
    }

    [Fact]
    public void Constants_PinTheSpiffeWireFormat()
    {
        SpiffeWorkloadIdentity.SCHEME.Should().Be("spiffe");
        SpiffeWorkloadIdentity.TRUST_DOMAIN.Should().Be("d2.internal");
        SpiffeWorkloadIdentity.WORKLOAD_PATH_PREFIX.Should().Be("/workload/");
    }
}
