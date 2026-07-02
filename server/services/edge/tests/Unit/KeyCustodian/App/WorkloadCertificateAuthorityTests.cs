// -----------------------------------------------------------------------
// <copyright file="WorkloadCertificateAuthorityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

/// <summary>
/// Pins the interim fail-closed DENY-ALL <see cref="WorkloadCertificateAuthority"/>
/// issuance skeleton across the full origin matrix: the type-zero
/// <c>Unestablished</c> deny is checked FIRST (the specific origin-unestablished
/// code), and EVERY established origin is <c>Forbidden</c> — there is no allow arm
/// until the real caller↔subject binding rule lands with the cross-process issuance
/// transport wiring, which replaces these pins with the real allow/deny matrix.
/// </summary>
public sealed class WorkloadCertificateAuthorityTests
{
    [Fact]
    public void AuthorizeIssuance_Unestablished_Denied_RequestOriginUnestablished()
    {
        var result = WorkloadCertificateAuthority.AuthorizeIssuance(
            RequestOrigin.Unestablished);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED,
            "the type-zero fail-closed deny is checked FIRST");
    }

    [Theory]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    [InlineData(RequestOrigin.System)]
    public void AuthorizeIssuance_EveryEstablishedOrigin_DeniedForbidden(RequestOrigin origin)
    {
        var result = WorkloadCertificateAuthority.AuthorizeIssuance(origin);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the committed skeleton is DENY-ALL — no caller↔subject binding authority "
            + "exists yet, so no origin may be issued a workload leaf certificate");
    }
}
