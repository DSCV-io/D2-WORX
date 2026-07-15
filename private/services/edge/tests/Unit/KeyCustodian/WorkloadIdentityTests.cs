// -----------------------------------------------------------------------
// <copyright file="WorkloadIdentityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian;

/// <summary>
/// Tests for KeyCustodian's <see cref="WorkloadIdentity"/> domain wrapper. The
/// exhaustive SPIFFE-grammar matrix now lives in the shared
/// <c>SpiffeWorkloadIdentityTests</c> (the grammar moved to
/// <c>D2.Shared.WorkloadIdentity</c>). This suite is the
/// <b>delegation regression-pin</b>: KeyCustodian re-maps the shared grammar's
/// generic <c>ValidationFailed</c> to its own
/// <c>KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY</c> code on the issuance side, and the
/// re-exported constants, <see cref="WorkloadIdentity.Uri"/>, valid-path success,
/// and <see cref="WorkloadIdentity.FromTrusted"/> all behave identically post-refactor.
/// </summary>
public sealed class WorkloadIdentityTests
{
    // -----------------------------------------------------------------------
    // Create / Parse — valid input still succeeds through the delegation
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("edge")]
    [InlineData("files")]
    [InlineData("my-service-01")]
    public void Create_ValidServiceId_ReturnsOk(string serviceId)
    {
        var result = WorkloadIdentity.Create(serviceId);

        result.Success.Should().BeTrue();
        result.Data!.ServiceId.Should().Be(serviceId);
    }

    [Fact]
    public void Create_UpperCase_NormalizesToLowercase()
    {
        var result = WorkloadIdentity.Create(" EDGE ");

        result.Success.Should().BeTrue();
        result.Data!.ServiceId.Should().Be("edge");
    }

    [Fact]
    public void Parse_ValidSpiffeUri_ReturnsOkWithServiceId()
    {
        var result = WorkloadIdentity.Parse("spiffe://d2.internal/workload/edge");

        result.Success.Should().BeTrue();
        result.Data!.ServiceId.Should().Be("edge");
    }

    [Fact]
    public void Uri_RoundTripsThroughParse()
    {
        var identity = WorkloadIdentity.Create("files").Data!;
        identity.Uri.Should().Be("spiffe://d2.internal/workload/files");

        var parsed = WorkloadIdentity.Parse(identity.Uri);
        parsed.Success.Should().BeTrue();
        parsed.Data!.ServiceId.Should().Be("files");
    }

    // -----------------------------------------------------------------------
    // Delegation regression-pin: the shared generic failure is re-mapped to the
    // KeyCustodian code on BOTH Create (issuance) AND Parse (peer-validation).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("svc with spaces")]
    [InlineData("svc/slash")]
    [InlineData("svc_underscore")]
    public void Create_Invalid_ReturnsKeyCustodianInvalidWorkloadIdentity(string? serviceId)
    {
        var result = WorkloadIdentity.Create(serviceId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY);
        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void Create_OverMaxLength_ReturnsKeyCustodianInvalidWorkloadIdentity()
    {
        var result = WorkloadIdentity.Create(new string('a', 65));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY);
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("spiffe://evil.internal/workload/edge")] // wrong trust domain
    [InlineData("https://d2.internal/workload/edge")] // wrong scheme
    [InlineData("spiffe://d2.internal/svc/edge")] // missing /workload/ path
    [InlineData("spiffe://d2.internal/workload/")] // empty workload segment
    [InlineData("spiffe://d2.internal.evil.com/workload/edge")] // trust-domain suffix attack
    public void Parse_Adversarial_ReturnsKeyCustodianInvalidWorkloadIdentity(string? uri)
    {
        var result = WorkloadIdentity.Parse(uri);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY);
    }

    // -----------------------------------------------------------------------
    // FromTrusted (KeyCustodian-owned rehydration)
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
    public void FromTrusted_Whitespace_ThrowsArgumentException()
    {
        var act = () => WorkloadIdentity.FromTrusted("   ");

        act.Should().Throw<ArgumentException>();
    }

    // Gate-intact pin: FromTrusted bypasses validation, Create still rejects.
    [Fact]
    public void FromTrusted_AcceptsInvalidCharset_CreateRejectsIt()
    {
        var trusted = WorkloadIdentity.FromTrusted("svc with spaces");
        trusted.ServiceId.Should().Be("svc with spaces");

        WorkloadIdentity.Create("svc with spaces").Success.Should().BeFalse();
    }

    [Fact]
    public void Constants_ReExportTheSharedSpiffeWireFormat()
    {
        WorkloadIdentity.SCHEME.Should().Be("spiffe");
        WorkloadIdentity.TRUST_DOMAIN.Should().Be("d2.internal");
        WorkloadIdentity.WORKLOAD_PATH_PREFIX.Should().Be("/workload/");
    }
}
