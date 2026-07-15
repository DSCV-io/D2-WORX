// -----------------------------------------------------------------------
// <copyright file="IssueLeafHandlerTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueLeaf;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;
using D2.Edge.KeyCustodian.App.Application.Issuance;
using D2.Private.Auth;
using D2.Shared.Auth.Abstractions;

/// <summary>
/// The generated-op shell <see cref="IssueLeafHandler"/>: the wire DTO's CSR bytes
/// arrive at the inner handler INTACT, the inner output maps field-by-field to the
/// wire DTO (leaf DER, issuer DER, Instant â†’ DateTimeOffset), and an inner denial
/// bubbles UNCHANGED (code + status preserved â€” the shell adds no gate and no
/// telemetry of its own; the inner handler is the single chokepoint).
/// </summary>
public sealed class IssueLeafHandlerTests
{
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    [Fact]
    public async Task IssueLeaf_HappyPath_MapsInnerOutputFieldByField()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);
        var (csr, csrSpki) = KcAppTestKit.BuildP256Csr();

        // Drive the inner handler directly for the expected shape, then the shell.
        var inner = BuildInner(db, RequestOrigin.CrossProcessHop, "edge");
        var shell = new IssueLeafHandler(
            KcAppTestKit.ContextWithOriginAndCaller<IssueLeafHandler>(
                RequestOrigin.CrossProcessHop, "edge", IssueScopes()),
            inner);

        var result = await shell.HandleAsync(
            new D2.Edge.KeyCustodian.Client.Issuance.IssueLeafInput(csr));

        result.Success.Should().BeTrue();
        var output = result.Data!;

        // The CSR bytes arrived intact: the minted leaf certifies the CSR's key.
        using var leaf = System.Security.Cryptography.X509Certificates
            .X509CertificateLoader.LoadCertificate(output.CertificateDer);
        leaf.PublicKey.ExportSubjectPublicKeyInfo().Should().Equal(csrSpki);

        // Field-by-field mapping: validity window Instant â†’ DateTimeOffset. notBefore
        // is front-backdated by the fixed clock-skew allowance (5 min); notAfter =
        // now + validity is unchanged (forward validity never shortened).
        output.NotBefore.Should().Be(
            (KcAppTestKit.SR_BaseInstant - Duration.FromMinutes(5)).ToDateTimeOffset());
        output.NotAfter.Should().Be(
            (KcAppTestKit.SR_BaseInstant
                + Duration.FromTimeSpan(r_options.LeafValidity)).ToDateTimeOffset());
        output.IssuerCertificateDer.Should().NotBeEmpty();
    }

    [Fact]
    public async Task IssueLeaf_InnerAuthorityDeny_BubblesUnchanged()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var (csr, _) = KcAppTestKit.BuildP256Csr();

        // The INNER handler's context is EdgeInbound â†’ the plane deny; the shell
        // must surface the same code + status verbatim.
        var inner = BuildInner(db, RequestOrigin.EdgeInbound, "edge");
        var shell = new IssueLeafHandler(
            KcAppTestKit.ContextWithOriginAndCaller<IssueLeafHandler>(
                RequestOrigin.EdgeInbound, "edge", IssueScopes()),
            inner);

        var result = await shell.HandleAsync(
            new D2.Edge.KeyCustodian.Client.Issuance.IssueLeafInput(csr));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_ISSUANCE_NOT_AUTHORIZED,
            "the inner 403 bubbles through the shell unchanged");
    }

    [Fact]
    public async Task IssueLeaf_InnerCsrReject_Bubbles400Unchanged()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var inner = BuildInner(db, RequestOrigin.CrossProcessHop, "edge");
        var shell = new IssueLeafHandler(
            KcAppTestKit.ContextWithOriginAndCaller<IssueLeafHandler>(
                RequestOrigin.CrossProcessHop, "edge", IssueScopes()),
            inner);

        var result = await shell.HandleAsync(
            new D2.Edge.KeyCustodian.Client.Issuance.IssueLeafInput(
                RandomNumberGenerator.GetBytes(64)));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CSR,
            "the inner 400 bubbles through the shell unchanged");
    }

    private static IReadOnlySet<string> IssueScopes() =>
        new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Issue };

    private IssueWorkloadCertificateHandler BuildInner(
        KeyCustodianTestDbContext db, RequestOrigin origin, string? caller)
    {
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);

        return new IssueWorkloadCertificateHandler(
            KcAppTestKit.ContextWithOriginAndCaller<IssueWorkloadCertificateHandler>(
                origin, caller, IssueScopes()),
            KcAppTestKit.NullClassifier(),
            db,
            Options.Create(r_options),
            new CaLeafSigningCapability(db, r_crypto, clock),
            clock);
    }
}
