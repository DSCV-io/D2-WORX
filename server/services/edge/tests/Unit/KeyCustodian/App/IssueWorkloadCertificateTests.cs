// -----------------------------------------------------------------------
// <copyright file="IssueWorkloadCertificateTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;

/// <summary>
/// Pins the interim fail-closed DENY-ALL issuance gate on
/// <see cref="IssueWorkloadCertificateHandler"/>: until the real caller↔subject
/// binding rule lands with the cross-process issuance transport wiring, EVERY origin
/// is denied THROUGH the real handler — no leaf is returned, no issuance-audit row is
/// written, and the managed-key store is untouched, even with a fully seeded active
/// CA and a valid workload identity. The pure-rule matrix for
/// <see cref="WorkloadCertificateIssuance"/> (the leaf-building crypto) and for the
/// deny-all <see cref="WorkloadCertificateAuthority"/> skeleton live in their own
/// suites; this suite proves the gate at the handler seam. The wiring step that
/// replaces the deny-all arm replaces these pins with the real allow/deny matrix.
/// </summary>
public sealed class IssueWorkloadCertificateTests
{
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    // -----------------------------------------------------------------------
    // Deny-all through the REAL handler — every established origin Forbidden,
    // even with an active CA seeded and a valid workload requested.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    [InlineData(RequestOrigin.System)]
    public async Task Issue_EveryEstablishedOrigin_DeniedForbidden_NoLeaf_NoAudit(
        RequestOrigin origin)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);
        var keysBefore = db.Keys.Count();

        var result = await Build(db, origin)
            .HandleAsync(new IssueWorkloadCertificateInput("edge"));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "no caller↔subject binding authority exists yet — every established origin "
            + "is denied until the real issuance rule lands with the transport wiring");
        result.Data.Should().BeNull(because: "no leaf may be minted through the deny-all gate");
        db.LeafIssuanceAudit.Should().BeEmpty(because: "a denied issuance writes no audit row");
        db.Keys.Count().Should().Be(keysBefore, because: "the managed-key store is untouched");
    }

    [Fact]
    public async Task Issue_UnestablishedOrigin_DeniedRequestOriginUnestablished_First()
    {
        // The type-zero fail-closed arm runs FIRST: a context no boundary established
        // surfaces the specific origin-unestablished code, not the generic Forbidden.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var result = await Build(db, RequestOrigin.Unestablished)
            .HandleAsync(new IssueWorkloadCertificateInput("edge"));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED);
        db.LeafIssuanceAudit.Should().BeEmpty();
    }

    [Fact]
    public async Task Issue_DeniedBeforeInputValidation_InvalidWorkloadStillForbidden()
    {
        // Authority precedes work: even a garbage workload id surfaces the authority
        // deny, not INVALID_WORKLOAD_IDENTITY — the gate runs before any validation.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await Build(db, RequestOrigin.CrossProcessHop)
            .HandleAsync(new IssueWorkloadCertificateInput("NOT A VALID ID"));

        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.ErrorCode.Should().NotBe(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_WORKLOAD_IDENTITY,
            "the deny-all authority gate fires before input validation");
    }

    [Fact]
    public async Task Issue_DeniedBeforeCaLoad_NoActiveCaStillForbidden_Not503()
    {
        // With NO CA seeded, a 503 NO_ACTIVE_ISSUING_CA would prove the handler
        // reached the CA load — the deny-all gate must fire before it.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var result = await Build(db, RequestOrigin.CrossProcessHop)
            .HandleAsync(new IssueWorkloadCertificateInput("edge"));

        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the authority gate denies before the CA dependency is even consulted");
    }

    private IssueWorkloadCertificateHandler Build(
        KeyCustodianTestDbContext db, RequestOrigin origin) =>
        new(
            KcAppTestKit.ContextWithOrigin<IssueWorkloadCertificateHandler>(origin),
            KcAppTestKit.NullClassifier(),
            db,
            Options.Create(r_options),
            r_crypto,
            new TestClock(KcAppTestKit.SR_BaseInstant));
}
