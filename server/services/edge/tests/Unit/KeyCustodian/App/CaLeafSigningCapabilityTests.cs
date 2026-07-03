// -----------------------------------------------------------------------
// <copyright file="CaLeafSigningCapabilityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Security.Cryptography.X509Certificates;
using D2.Edge.KeyCustodian.App.Application;

/// <summary>
/// The isolated <see cref="CaLeafSigningCapability"/> — the sole holder of the
/// issuance-path intermediate-CA unwrap. Covers the happy path (a seeded active
/// intermediate signs the supplied public key) plus the defensive arms the handler
/// suites do not reach directly: a malformed ACTIVE intermediate row (wrong key type
/// or NULL certificate material) is the retryable 503, and a stored issuer
/// certificate that makes the pure issuance rule fail bubbles the 500 unchanged.
/// </summary>
public sealed class CaLeafSigningCapabilityTests
{
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    [Fact]
    public async Task SignLeaf_SeededActiveIntermediate_SignsSuppliedKey()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var (intermediateKid, _) = await KcAppTestKit.SeedCaAsync(
            db, r_crypto, KcAppTestKit.SR_BaseInstant);

        var result = await Build(db).SignLeafAsync(
            LeafPublicKey(), WorkloadIdentity.FromTrusted("edge"), Duration.FromHours(1));

        result.Success.Should().BeTrue();
        result.Data!.IssuerKid.Value.Should().Be(intermediateKid);
        result.Data!.Certificate.CertificateDer.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SignLeaf_ActiveIntermediateWrongKeyType_503()
    {
        // An ACTIVE row in the intermediate domain whose KeyType is NOT
        // X509CaCertificate is corruption — surfaced as the retryable 503.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            KcAppTestKit.BuildOptions(),
            KeyDomain.MTLS_CA_INTERMEDIATE,
            KeyType.AesPayload,
            KeyStatus.Active,
            KcAppTestKit.SR_BaseInstant);

        var result = await Build(db).SignLeafAsync(
            LeafPublicKey(), WorkloadIdentity.FromTrusted("edge"), Duration.FromHours(1));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
    }

    [Fact]
    public async Task SignLeaf_ActiveIntermediateNullCertificateMaterial_503()
    {
        // A CA-typed ACTIVE intermediate carrying NULL certificate material is the
        // other malformed-active-row shape — also the retryable 503.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var record = new KeyRecord
        {
            Kid = KidMinting.Mint(),
            KeyDomain = KeyDomain.MTLS_CA_INTERMEDIATE,
            KeyType = KeyType.X509CaCertificate,
            KeyMaterialEncrypted = r_crypto.Encrypt(new byte[32]),
            PublicKeyMaterial = null,
            CaCertificate = null,
            CreatedAt = KcAppTestKit.SR_BaseInstant,
            Status = KeyStatus.Active,
            ActivatedAt = KcAppTestKit.SR_BaseInstant,
        };

        db.Keys.Add(record);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await Build(db).SignLeafAsync(
            LeafPublicKey(), WorkloadIdentity.FromTrusted("edge"), Duration.FromHours(1));

        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
    }

    [Fact]
    public async Task SignLeaf_IssuerCertificateRejectedByIssuanceRule_Bubbles500()
    {
        // The intermediate row passes the malformed-active-row guard (CA-typed,
        // non-null material) and its private key unwraps, but the stored certificate
        // lacks a Subject Key Identifier — so the pure issuance rule cannot derive the
        // Authority Key Identifier, fails with INVALID_CERTIFICATE_REQUEST (500), and
        // the capability bubbles that failure unchanged.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        SeedSkiLessIntermediate(db);
        await db.SaveChangesAsync(CancellationToken.None);

        var result = await Build(db).SignLeafAsync(
            LeafPublicKey(), WorkloadIdentity.FromTrusted("edge"), Duration.FromHours(1));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_INVALID_CERTIFICATE_REQUEST);
    }

    private static PublicKey LeafPublicKey()
    {
        var (csrDer, _) = KcAppTestKit.BuildP256Csr();
        return CsrVerification.Verify(csrDer).Data!;
    }

    private CaLeafSigningCapability Build(KeyCustodianTestDbContext db) =>
        new(db, r_crypto, new TestClock(KcAppTestKit.SR_BaseInstant));

    private void SeedSkiLessIntermediate(KeyCustodianTestDbContext db)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var request = new CertificateRequest(
            "CN=D2 Malformed Issuing CA", key, HashAlgorithmName.SHA256);

        // CA-shaped but deliberately WITHOUT a Subject Key Identifier extension, so the
        // issuance rule's Authority-Key-Identifier build throws.
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: true,
                hasPathLengthConstraint: true,
                pathLengthConstraint: 0,
                critical: true));

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));

        var now = KcAppTestKit.SR_BaseInstant.ToDateTimeOffset();

        using var cert = request.CreateSelfSigned(now, now.AddDays(365));

        var record = new KeyRecord
        {
            Kid = KidMinting.Mint(),
            KeyDomain = KeyDomain.MTLS_CA_INTERMEDIATE,
            KeyType = KeyType.X509CaCertificate,
            KeyMaterialEncrypted = r_crypto.Encrypt(key.ExportPkcs8PrivateKey()),
            PublicKeyMaterial = null,
            CaCertificate = cert.RawData,
            CreatedAt = KcAppTestKit.SR_BaseInstant,
            Status = KeyStatus.Active,
            ActivatedAt = KcAppTestKit.SR_BaseInstant,
        };

        db.Keys.Add(record);
    }
}
