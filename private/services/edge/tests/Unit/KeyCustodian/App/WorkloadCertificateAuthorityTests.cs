// -----------------------------------------------------------------------
// <copyright file="WorkloadCertificateAuthorityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App;

/// <summary>
/// The pure-rule matrices for both <see cref="WorkloadCertificateAuthority"/> arms.
/// <c>AuthorizeIssuance</c>: the type-zero <c>Unestablished</c> deny is checked
/// FIRST; issuance is cross-process-only (every other established plane gets the
/// uniform 403); a cross-process hop with no authenticated peer is fail-closed;
/// an authenticated cross-process caller is allowed — with NO subject arm (self-issue
/// is structural: nothing exists to compare). <c>AuthorizeCaCertificateFetch</c>:
/// same first arm; served planes are cross-process + in-process module; identity
/// required; broad within the served planes.
/// </summary>
public sealed class WorkloadCertificateAuthorityTests
{
    // -----------------------------------------------------------------------
    // AuthorizeIssuance — the real 3-arm rule
    // -----------------------------------------------------------------------

    [Fact]
    public void AuthorizeIssuance_Unestablished_Denied_RequestOriginUnestablished_First()
    {
        // Even WITH a caller id present, the type-zero arm fires first — a context
        // no boundary established can never reach a later arm.
        var result = WorkloadCertificateAuthority.AuthorizeIssuance(
            "edge", RequestOrigin.Unestablished);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED,
            "the type-zero fail-closed deny is checked FIRST");
    }

    [Theory]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.InProcessModule)]
    [InlineData(RequestOrigin.System)]
    public void AuthorizeIssuance_NonCrossProcessPlane_DeniedIssuanceNotAuthorized(
        RequestOrigin origin)
    {
        // The in-process plane's ImmediateCaller is caller-supplied — even a present
        // caller id cannot authorize minting an identity off the cross-process plane.
        var result = WorkloadCertificateAuthority.AuthorizeIssuance("edge", origin);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_ISSUANCE_NOT_AUTHORIZED,
            "issuance is cross-process-only; the plane deny is the uniform 403");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AuthorizeIssuance_CrossProcessNoPeer_DeniedForbidden(string? caller)
    {
        var result = WorkloadCertificateAuthority.AuthorizeIssuance(
            caller, RequestOrigin.CrossProcessHop);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "a cross-process hop with no authenticated mTLS peer identity is fail-closed");
        result.ErrorCode.Should().NotBe(
            KeyCustodianErrorCodes.KEYCUSTODIAN_ISSUANCE_NOT_AUTHORIZED,
            "the identity-absent arm is the bare Forbidden, not the plane deny");
    }

    [Fact]
    public void AuthorizeIssuance_CrossProcessWithPeer_Allowed()
    {
        var result = WorkloadCertificateAuthority.AuthorizeIssuance(
            "edge", RequestOrigin.CrossProcessHop);

        result.Success.Should().BeTrue(
            "an authenticated cross-process caller may be issued its own leaf — "
            + "self-issue is structural (the handler derives the SAN from the peer), "
            + "so the rule has no subject arm");
    }

    // -----------------------------------------------------------------------
    // AuthorizeCaCertificateFetch — the plane-gated broad arm
    // -----------------------------------------------------------------------

    [Fact]
    public void AuthorizeCaCertificateFetch_Unestablished_Denied_RequestOriginUnestablished_First()
    {
        var result = WorkloadCertificateAuthority.AuthorizeCaCertificateFetch(
            "edge", RequestOrigin.Unestablished);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED,
            "the type-zero fail-closed deny is checked FIRST");
    }

    [Theory]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.System)]
    public void AuthorizeCaCertificateFetch_UnservedPlane_DeniedCaCertificateNotAuthorized(
        RequestOrigin origin)
    {
        // The internal trust anchor never rides the public plane, and System workers
        // reach the CA through the CA provider.
        var result = WorkloadCertificateAuthority.AuthorizeCaCertificateFetch("edge", origin);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_CA_CERTIFICATE_NOT_AUTHORIZED);
    }

    [Theory]
    [InlineData(RequestOrigin.CrossProcessHop, null)]
    [InlineData(RequestOrigin.CrossProcessHop, "")]
    [InlineData(RequestOrigin.CrossProcessHop, "   ")]
    [InlineData(RequestOrigin.InProcessModule, null)]
    public void AuthorizeCaCertificateFetch_ServedPlaneNoIdentity_DeniedForbidden(
        RequestOrigin origin, string? caller)
    {
        var result = WorkloadCertificateAuthority.AuthorizeCaCertificateFetch(caller, origin);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "a served plane with no caller identity is fail-closed");
    }

    [Theory]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    public void AuthorizeCaCertificateFetch_ServedPlaneWithIdentity_Allowed(
        RequestOrigin origin)
    {
        // Broad within the served planes — public trust material; there is
        // deliberately NO per-workload policy map.
        var result = WorkloadCertificateAuthority.AuthorizeCaCertificateFetch("files", origin);

        result.Success.Should().BeTrue();
    }
}
