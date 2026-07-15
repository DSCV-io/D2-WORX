// -----------------------------------------------------------------------
// <copyright file="CaSuccessorFactoryTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App.CertificateAuthority;

using System.Security.Cryptography.X509Certificates;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.CertificateAuthority;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Observability;

/// <summary>
/// Tests for <see cref="CaSuccessorFactory"/> — the shared CA-key builder used by
/// the generate-successor and compromise-replacement paths. An intermediate is
/// signed by the active root (and chains to it); a root is self-signed; a missing
/// active root yields 503; the new private key is root-wrapped (never plaintext on
/// the pending row).
/// </summary>
public sealed class CaSuccessorFactoryTests
{
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();

    [Fact]
    public async Task Build_Intermediate_WithActiveRoot_ChainsToRoot()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var (_, rootCertDer) = await KcAppTestKit.SeedCaRootAsync(db, r_crypto, created);
        var clock = new TestClock(created + Duration.FromHours(1));

        var result = await CaSuccessorFactory.BuildAsync(
            db,
            r_crypto,
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options),
            r_options,
            clock,
            KeyDomain.MtlsCaIntermediate,
            KeyCustodianMetrics.CaRootKeyUses.Operation.GENERATE_SUCCESSOR,
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.KeyType.Should().Be(KeyType.X509CaCertificate);
        result.Data!.KeyDomain.Value.Should().Be(KeyDomain.MTLS_CA_INTERMEDIATE);
        result.Data!.CaCertificateMaterial.Should().NotBeNull();

        // The generated intermediate must chain to the seeded root.
        CaTestAssertions.AssertChainsToRoot(
            result.Data!.CaCertificateMaterial!.Bytes.Span, rootCertDer);
    }

    [Fact]
    public async Task Build_Intermediate_NoActiveRoot_ReturnsServiceUnavailable()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);

        var result = await CaSuccessorFactory.BuildAsync(
            db,
            r_crypto,
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options),
            r_options,
            clock,
            KeyDomain.MtlsCaIntermediate,
            KeyCustodianMetrics.CaRootKeyUses.Operation.GENERATE_SUCCESSOR,
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
    }

    [Fact]
    public async Task Build_Intermediate_RootRetiredNotActive_ReturnsServiceUnavailable()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        await KcAppTestKit.SeedCaRootAsync(db, r_crypto, created, KeyStatus.Retired);
        var clock = new TestClock(created + Duration.FromHours(1));

        var result = await CaSuccessorFactory.BuildAsync(
            db,
            r_crypto,
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options),
            r_options,
            clock,
            KeyDomain.MtlsCaIntermediate,
            KeyCustodianMetrics.CaRootKeyUses.Operation.GENERATE_SUCCESSOR,
            CancellationToken.None);

        result.Success.Should().BeFalse(because: "only an ACTIVE root may sign an intermediate");
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
    }

    [Fact]
    public async Task Build_Root_SelfSigned_NoIssuerNeeded()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);

        var result = await CaSuccessorFactory.BuildAsync(
            db,
            r_crypto,
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options),
            r_options,
            clock,
            KeyDomain.MtlsCaRoot,
            KeyCustodianMetrics.CaRootKeyUses.Operation.GENERATE_SUCCESSOR,
            CancellationToken.None);

        result.Success.Should().BeTrue(because: "a root successor is self-signed; no issuer needed");
        result.Data!.KeyDomain.Value.Should().Be(KeyDomain.MTLS_CA_ROOT);

        using var cert = X509CertificateLoader.LoadCertificate(
            result.Data!.CaCertificateMaterial!.Bytes.Span);
        cert.SubjectName.Name.Should().Be(cert.IssuerName.Name, because: "a root CA is self-signed");
    }

    [Fact]
    public async Task Build_Intermediate_WrapsPrivateKey_NeverPlaintext()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        await KcAppTestKit.SeedCaRootAsync(db, r_crypto, created);
        var clock = new TestClock(created + Duration.FromHours(1));

        var result = await CaSuccessorFactory.BuildAsync(
            db,
            r_crypto,
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options),
            r_options,
            clock,
            KeyDomain.MtlsCaIntermediate,
            KeyCustodianMetrics.CaRootKeyUses.Operation.GENERATE_SUCCESSOR,
            CancellationToken.None);

        // The wrapped material must unwrap back to a usable ECDSA key — proving it
        // was root-wrapped (not stored plaintext) and the wrap round-trips.
        var unwrapped = r_crypto.Decrypt(result.Data!.KeyMaterialEncrypted.Bytes.Span);

        try
        {
            ImportPkcs8(unwrapped).Should().BeTrue(
                because: "the wrapped material is a valid PKCS#8 ECDSA key");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(unwrapped);
        }
    }

    [Fact]
    public async Task Build_NonCaDomain_ReturnsPreconditionViolated()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);

        var result = await CaSuccessorFactory.BuildAsync(
            db,
            r_crypto,
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options),
            r_options,
            clock,
            KeyDomain.JwksSigning,
            KeyCustodianMetrics.CaRootKeyUses.Operation.GENERATE_SUCCESSOR,
            CancellationToken.None);

        result.Success.Should().BeFalse(because: "the factory only builds CA keys");
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_PRECONDITION_VIOLATED);
    }

    private static bool ImportPkcs8(byte[] pkcs8)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(pkcs8, out _);
        return true;
    }
}
