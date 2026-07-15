// -----------------------------------------------------------------------
// <copyright file="PoCCsrSigningWorkloadCertificateIssuerTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.Host;

using D2.Edge.Api.Outbound;
using D2.Edge.KeyCustodian.App.Application.Issuance;
using D2.Edge.Tests.Unit.KeyCustodian.App;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// CSR-shaped <see cref="PoCCsrSigningWorkloadCertificateIssuer"/> via the co-host
/// leaf-capability path: cert-only material, scope factory, bad CSR rejection.
/// Does not use System / <c>IIssueLeafHandler</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PoCCsrSigningWorkloadCertificateIssuerTests
{
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    [Fact]
    public async Task IssueAsync_ValidCsr_ReturnsCertOnlyMaterial_ViaLeafCap()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var (csrDer, _) = KcAppTestKit.BuildP256Csr();
        var issuer = BuildIssuer(db);

        var result = await issuer.IssueAsync(csrDer);

        result.Success.Should().BeTrue();
        result.Data!.CertificateDer.Should().NotBeEmpty();
        result.Data.IssuerCertificateDer.Should().NotBeEmpty();
        result.Data.NotAfter.Should().BeGreaterThan(KcAppTestKit.SR_BaseInstant);

        // Cert-only seam: WorkloadLeafMaterial has no private-key member — assert DER
        // is a loadable public certificate (no private key on the returned material).
        using var leaf = System.Security.Cryptography.X509Certificates.X509CertificateLoader
            .LoadCertificate(result.Data.CertificateDer);

        leaf.HasPrivateKey.Should().BeFalse();
        leaf.RawData.Should().NotBeEmpty();
    }

    [Fact]
    public async Task IssueAsync_EmptyCsr_Fails()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var issuer = BuildIssuer(db);
        var result = await issuer.IssueAsync([]);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task IssueAsync_NullCsr_Fails()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var issuer = BuildIssuer(db);
        var result = await issuer.IssueAsync(null!);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task IssueAsync_NoActiveIntermediate_503()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var (csrDer, _) = KcAppTestKit.BuildP256Csr();
        var issuer = BuildIssuer(db);

        var result = await issuer.IssueAsync(csrDer);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public void Issuer_Ctor_TakesIServiceScopeFactory_NotICaLeafSigningCapability()
    {
        // Singleton issuer must not capture scoped/transient capability —
        // only IServiceScopeFactory is injected; capability resolves per IssueAsync.
        var ctor = typeof(PoCCsrSigningWorkloadCertificateIssuer)
            .GetConstructors()
            .Single();

        ctor.GetParameters().Select(p => p.ParameterType)
            .Should().Contain(typeof(IServiceScopeFactory));

        ctor.GetParameters().Select(p => p.ParameterType)
            .Should().NotContain(typeof(ICaLeafSigningCapability));
    }

    [Fact]
    public async Task IssueAsync_Twice_ResolvesCapabilityPerCall()
    {
        // Happy path twice against a real leaf-cap + seeded CA proves each call
        // opens a fresh scope (no captive DbContext) and still signs.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var issuer = BuildIssuer(db);
        var (csr1, _) = KcAppTestKit.BuildP256Csr();
        var (csr2, _) = KcAppTestKit.BuildP256Csr();

        var first = await issuer.IssueAsync(csr1);
        var second = await issuer.IssueAsync(csr2);

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();
        first.Data!.CertificateDer.Should().NotEqual(second.Data!.CertificateDer);
    }

    [Fact]
    public async Task IssueAsync_CanceledToken_ThrowsOperationCanceled()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedCaAsync(db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var issuer = BuildIssuer(db);
        var (csrDer, _) = KcAppTestKit.BuildP256Csr();
        var canceled = new CancellationToken(canceled: true);

        var act = async () => await issuer.IssueAsync(csrDer, canceled);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Issuer_UsesEdgeHostIdentityServiceId_NotDuplicateConstant()
    {
        // Single SoT: EdgeHostIdentity.SERVICE_ID — no EDGE_SERVICE_ID duplicate.
        typeof(PoCCsrSigningWorkloadCertificateIssuer)
            .GetField("EDGE_SERVICE_ID")
            .Should().BeNull();

        var source = File.ReadAllText(
            EdgeHostTestKit.ResolveEdgeApiSourceFile(
                "Outbound", "PoCCsrSigningWorkloadCertificateIssuer.cs"));

        source.Should().Contain("EdgeHostIdentity.SERVICE_ID");
        source.Should().NotContain("EDGE_SERVICE_ID");
    }

    private PoCCsrSigningWorkloadCertificateIssuer BuildIssuer(KeyCustodianTestDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(KcAppTestKit.BuildOptions()));
        services.AddTransient<ICaLeafSigningCapability>(_ =>
            new CaLeafSigningCapability(db, r_crypto, new TestClock(KcAppTestKit.SR_BaseInstant)));

        services.AddSingleton<PoCCsrSigningWorkloadCertificateIssuer>();

        var sp = services.BuildServiceProvider();

        return sp.GetRequiredService<PoCCsrSigningWorkloadCertificateIssuer>();
    }
}
