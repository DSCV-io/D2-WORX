// -----------------------------------------------------------------------
// <copyright file="CaRootSigningCapabilityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App.CertificateAuthority;

using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.CertificateAuthority;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Observability;

/// <summary>
/// Behavior matrix for <see cref="ICaRootSigningCapability"/> — the dedicated §9.44
/// seam that holds every stored CA-root private-key plaintext use. The SIGN op mints
/// an intermediate that chains to the active root (503 when none active); the
/// SMOKE-VERIFY op unwraps a pending root and smoke-tests it — succeeding on valid
/// material, failing loud (the same <c>KEYCUSTODIAN_SMOKE_TEST_FAILED</c> the inline
/// generic path yields) on decryptable-but-invalid material, and PROPAGATING the
/// decrypt throw (no new swallow) on undecryptable material. Both ops zero the
/// unwrapped bytes on every path. The DI-isolation half of §9.44 is pinned in
/// <c>KeyCustodianAppServiceCollectionExtensionsTests</c>; the chokepoint-telemetry
/// half in <c>CaRootSigningInstrumentationTests</c>.
/// </summary>
public sealed class CaRootSigningCapabilityTests
{
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();

    // -----------------------------------------------------------------------
    // Sign op — root → intermediate
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sign_WithActiveRoot_MintsIntermediateChainingToRoot()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var (_, rootCertDer) = await KcAppTestKit.SeedCaRootAsync(db, r_crypto, created);
        var clock = new TestClock(created + Duration.FromHours(1));
        var capability = KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options);

        var result = await capability.SignSuccessorIntermediateAsync(
            Kid.FromTrusted(KidMinting.Mint()),
            KeyCustodianMetrics.CaRootKeyUses.Operation.GENERATE_SUCCESSOR);

        result.Success.Should().BeTrue();
        CaTestAssertions.AssertChainsToRoot(result.Data!.CertificateDer, rootCertDer);
    }

    [Fact]
    public async Task Sign_NoActiveRoot_ReturnsServiceUnavailable()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var capability = KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options);

        var result = await capability.SignSuccessorIntermediateAsync(
            Kid.FromTrusted(KidMinting.Mint()),
            KeyCustodianMetrics.CaRootKeyUses.Operation.GENERATE_SUCCESSOR);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
    }

    [Fact]
    public async Task Sign_RootRetiredNotActive_ReturnsServiceUnavailable()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        await KcAppTestKit.SeedCaRootAsync(db, r_crypto, created, KeyStatus.Retired);
        var clock = new TestClock(created + Duration.FromHours(1));
        var capability = KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options);

        var result = await capability.SignSuccessorIntermediateAsync(
            Kid.FromTrusted(KidMinting.Mint()),
            KeyCustodianMetrics.CaRootKeyUses.Operation.COMPROMISE_REPLACEMENT);

        result.Success.Should().BeFalse(because: "only an ACTIVE root may sign an intermediate");
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
    }

    [Fact]
    public async Task Sign_NullSuccessorKid_Throws()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var capability = KcAppTestKit.BuildRootSigningCapability(
            db, r_crypto, new TestClock(KcAppTestKit.SR_BaseInstant), r_options);

        var act = async () => await capability.SignSuccessorIntermediateAsync(
            null!, KeyCustodianMetrics.CaRootKeyUses.Operation.GENERATE_SUCCESSOR);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // -----------------------------------------------------------------------
    // Smoke-verify op — pending root material
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Verify_ValidPendingRoot_ReturnsOk()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var (kid, _) = await KcAppTestKit.SeedCaRootAsync(
            db, r_crypto, created, KeyStatus.Pending);
        var capability = KcAppTestKit.BuildRootSigningCapability(
            db, r_crypto, new TestClock(created), r_options);
        var pending = (PendingKey)db.Keys.Single(k => k.Kid == kid).ToDomain();

        var result = await capability.SmokeTestRootKeyMaterialAsync(
            pending, KeyCustodianMetrics.CaRootKeyUses.Operation.ACTIVATE_SMOKE_TEST);

        result.Success.Should().BeTrue(because: "a valid root smoke-passes via the capability");
    }

    [Fact]
    public async Task Verify_DecryptableButInvalidMaterial_ReturnsSmokeTestFailed()
    {
        // The corrupt-material adversarial (decryptable-but-INVALID): the wrapped blob
        // unwraps cleanly but is not a valid PKCS#8 ECDSA key, so the CA smoke probe
        // throws internally and the envelope returns SMOKE_TEST_FAILED — no throw out.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyWithCorruptMaterialAsync(
            db,
            r_crypto,
            KeyDomain.MTLS_CA_ROOT,
            KeyType.X509CaCertificate,
            KeyStatus.Pending,
            created,
            corruptPlaintext: RandomNumberGenerator.GetBytes(64));

        var corruptRow = db.Keys.Single(k => k.Kid == kid);
        corruptRow.CaCertificate = RandomNumberGenerator.GetBytes(32);
        await db.SaveChangesAsync();

        var capability = KcAppTestKit.BuildRootSigningCapability(
            db, r_crypto, new TestClock(created), r_options);
        var pending = (PendingKey)db.Keys.Single(k => k.Kid == kid).ToDomain();

        var result = await capability.SmokeTestRootKeyMaterialAsync(
            pending, KeyCustodianMetrics.CaRootKeyUses.Operation.ROTATE_SMOKE_TEST);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_SMOKE_TEST_FAILED);
    }

    [Fact]
    public async Task Verify_UndecryptableMaterial_PropagatesDecryptThrow()
    {
        // The corrupt-material adversarial (UNDECRYPTABLE): the stored blob was never
        // wrapped by the root crypto, so the decrypt throws — and the capability
        // propagates it unchanged (no new swallow), exactly like the inline generic path.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = KidMinting.Mint();
        db.Keys.Add(new KeyRecord
        {
            Kid = kid,
            KeyDomain = KeyDomain.MTLS_CA_ROOT,
            KeyType = KeyType.X509CaCertificate,
            KeyMaterialEncrypted = RandomNumberGenerator.GetBytes(64),
            CaCertificate = RandomNumberGenerator.GetBytes(32),
            CreatedAt = created,
            Status = KeyStatus.Pending,
        });
        await db.SaveChangesAsync();

        var capability = KcAppTestKit.BuildRootSigningCapability(
            db, r_crypto, new TestClock(created), r_options);
        var pending = (PendingKey)db.Keys.Single(k => k.Kid == kid).ToDomain();

        var act = async () => await capability.SmokeTestRootKeyMaterialAsync(
            pending, KeyCustodianMetrics.CaRootKeyUses.Operation.ACTIVATE_SMOKE_TEST);

        await act.Should().ThrowAsync<Exception>(
            because: "an undecryptable wrapped root blob must fail loud, not be swallowed");
    }

    [Fact]
    public async Task Verify_NullPendingRoot_Throws()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var capability = KcAppTestKit.BuildRootSigningCapability(
            db, r_crypto, new TestClock(KcAppTestKit.SR_BaseInstant), r_options);

        var act = async () => await capability.SmokeTestRootKeyMaterialAsync(
            null!, KeyCustodianMetrics.CaRootKeyUses.Operation.ACTIVATE_SMOKE_TEST);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
