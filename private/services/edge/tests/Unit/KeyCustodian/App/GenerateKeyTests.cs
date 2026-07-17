// -----------------------------------------------------------------------
// <copyright file="GenerateKeyTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App;

/// <summary>
/// Tests for <see cref="GenerateKeyHandler"/> — happy path, adversarial domain inputs,
/// the duplicate-pending conflict, and the persisted audit + zero-material
/// guarantees.
/// </summary>
public sealed class GenerateKeyTests
{
    public static TheoryData<string, KeyType> WrongTypeForDomainCases()
    {
        // Fully adversarial: every catalog domain × every key type EXCEPT its
        // canonical binding (a new domain or key type automatically joins the matrix).
        var data = new TheoryData<string, KeyType>();

        foreach (var domain in KeyDomain.All)
        {
            foreach (var keyType in Enum.GetValues<KeyType>())
            {
                if (keyType != domain.KeyType)
                    data.Add(domain.Value, keyType);
            }
        }

        return data;
    }

    [Fact]
    public async Task Generate_Secret_Cookie_CreatesPendingRowAndAudit()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var handler = Build(db, clock);

        var result = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.COOKIE, KeyType.Secret));

        result.Success.Should().BeTrue();
        result.IsCreated.Should().BeTrue(because: "generating a new key returns HTTP 201");
        result.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Data!.Status.Should().Be(KeyStatus.Pending);
        result.Data!.Domain.Should().Be(KeyDomain.COOKIE);

        db.Keys.Should().ContainSingle();
        var row = db.Keys.Single();
        row.Status.Should().Be(KeyStatus.Pending);
        row.KeyMaterialEncrypted.Should().NotBeEmpty();

        db.Audit.Should().ContainSingle();
        db.Audit.Single().Action.Should().Be(KeyAuditAction.Generated);
    }

    [Fact]
    public async Task Generate_RsaSigning_PersistsPublicMaterial()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.SR_BaseInstant));

        var result = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.JWKS_SIGNING, KeyType.RsaSigning));

        result.Success.Should().BeTrue();
        db.Keys.Single().PublicKeyMaterial.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Generate_AesPayload_CreatesPendingRowWithNoPublicMaterial()
    {
        // AES-256-GCM is symmetric — no public key component should be persisted. audit is
        // now a SEALED domain (removed from the payload catalog), so exercise the preserved
        // symmetric machinery on a registered fixture payload domain.
        using var fixtureSeam = FixturePayloadDomains.Register();
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.SR_BaseInstant));

        var result = await handler.HandleAsync(
            new GenerateKeyInput(FixturePayloadDomains.PAYLOAD_A, KeyType.AesPayload));

        result.Success.Should().BeTrue();
        result.IsCreated.Should().BeTrue(because: "a new AES key returns HTTP 201");
        var row = db.Keys.Single();
        row.Status.Should().Be(KeyStatus.Pending);
        row.KeyMaterialEncrypted.Should().NotBeEmpty();
        row.PublicKeyMaterial.Should().BeNull(because: "symmetric keys carry no public material");
    }

    [Fact]
    public async Task Generate_MintedKid_PassesKidCreate()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.SR_BaseInstant));

        var result = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.COOKIE, KeyType.Secret));

        Kid.Create(result.Data!.Kid).Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Adversarial domain inputs
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-real-domain")]
    public async Task Generate_BadDomain_ReturnsUnknownKeyDomain_PersistsNothing(string? domain)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.SR_BaseInstant));

        var result = await handler.HandleAsync(new GenerateKeyInput(domain, KeyType.Secret));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN");
        db.Keys.Should().BeEmpty();
        db.Audit.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Domain→key-type binding — every wrong (domain, type) pair is a sharp 400
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(WrongTypeForDomainCases))]
    public async Task Generate_WrongTypeForDomain_Returns400Mismatch_PersistsNothing(
        string domain, KeyType wrongType)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.SR_BaseInstant));

        var result = await handler.HandleAsync(new GenerateKeyInput(domain, wrongType));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH,
            "a (domain, type) pair that disagrees with the canonical binding is a "
            + "permanent client error");
        db.Keys.Should().BeEmpty(because: "a mismatched pair never reaches the store");
        db.Audit.Should().BeEmpty(because: "a mismatched pair writes no audit entry");
    }

    [Fact]
    public async Task Generate_CaseVariantDomain_WrongType_StillRejectedByBinding()
    {
        // Normalization happens BEFORE the binding check: a case/whitespace variant of
        // a catalog domain still resolves its binding and rejects the wrong type. Uses a
        // registered fixture payload domain (audit is sealed and left the symmetric catalog).
        using var fixtureSeam = FixturePayloadDomains.Register();
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.SR_BaseInstant));

        var result = await handler.HandleAsync(
            new GenerateKeyInput(" PAYLOAD-FIXTURE-A ", KeyType.RsaSigning));

        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH);
        db.Keys.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Seal domain binding — seal:<service> binds EcdhSealing; any other type is a 400.
    // (The WrongTypeForDomainCases matrix only covers the closed All catalog; the seal
    // family is pattern-based and NOT in that catalog, so it is exercised explicitly.)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(KeyType.AesPayload)]
    [InlineData(KeyType.RsaSigning)]
    [InlineData(KeyType.Secret)]
    [InlineData(KeyType.X509CaCertificate)]
    public async Task Generate_SealDomain_WrongType_Returns400Mismatch_PersistsNothing(
        KeyType wrongType)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.SR_BaseInstant));

        var result = await handler.HandleAsync(new GenerateKeyInput("seal:audit", wrongType));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH,
            "a seal:<service> domain binds EcdhSealing — any other type is a sharp 400");
        db.Keys.Should().BeEmpty(because: "a mismatched pair never reaches the store");
        db.Audit.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // Duplicate pending conflict
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Generate_SecondPendingForSameDomain_ReturnsPendingKeyAlreadyExists()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var handler = Build(db, clock);

        var first = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.COOKIE, KeyType.Secret));
        first.Success.Should().BeTrue();

        var second = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.COOKIE, KeyType.Secret));

        second.Success.Should().BeFalse();
        second.ErrorCode.Should().Be("KEYCUSTODIAN_PENDING_KEY_ALREADY_EXISTS");
        db.Keys.Should().ContainSingle(because: "the second pending key was rejected");
    }

    [Fact]
    public async Task Generate_PendingInDifferentDomain_DoesNotBlock()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.SR_BaseInstant));

        await handler.HandleAsync(new GenerateKeyInput(KeyDomain.COOKIE, KeyType.Secret));
        var other = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.JWKS_SIGNING, KeyType.RsaSigning));

        other.Success.Should().BeTrue();
        db.Keys.Should().HaveCount(2);
    }

    // -----------------------------------------------------------------------
    // CA-certificate generation (the dedicated CA branch)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Generate_CaIntermediate_WithActiveRoot_CreatesPendingCaChainingToRoot()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var crypto = KcAppTestKit.BuildTestRootCrypto();
        var (_, rootCertDer) = await KcAppTestKit.SeedCaRootAsync(db, crypto, created);
        var handler = BuildWithCrypto(db, new TestClock(created + Duration.FromHours(1)), crypto);

        var result = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.MTLS_CA_INTERMEDIATE, KeyType.X509CaCertificate));

        result.Success.Should().BeTrue();
        result.IsCreated.Should().BeTrue(because: "a new pending CA row is created");

        var pending = db.Keys.Single(k =>
            k.KeyDomain == KeyDomain.MTLS_CA_INTERMEDIATE && k.Status == KeyStatus.Pending);
        pending.KeyType.Should().Be(KeyType.X509CaCertificate);
        pending.CaCertificate.Should().NotBeNullOrEmpty(because: "a CA carries its certificate");
        pending.PublicKeyMaterial.Should().BeNull();

        CaTestAssertions.AssertChainsToRoot(pending.CaCertificate!, rootCertDer);
        db.Audit.Should().Contain(a =>
            a.Kid == pending.Kid && a.Action == KeyAuditAction.Generated);
    }

    [Fact]
    public async Task Generate_CaIntermediate_NoActiveRoot_ReturnsServiceUnavailable_NoRow()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.SR_BaseInstant));

        var result = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.MTLS_CA_INTERMEDIATE, KeyType.X509CaCertificate));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA);
        db.Keys.Should().BeEmpty(because: "no pending CA may be created without an active root");
        db.Audit.Should().BeEmpty();
    }

    [Fact]
    public async Task Generate_CaRoot_SelfSigned_CreatesPendingRoot()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = Build(db, new TestClock(KcAppTestKit.SR_BaseInstant));

        var result = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.MTLS_CA_ROOT, KeyType.X509CaCertificate));

        result.Success.Should().BeTrue(because: "a root successor is self-signed; no issuer needed");
        var pending = db.Keys.Single(k => k.KeyDomain == KeyDomain.MTLS_CA_ROOT);
        pending.Status.Should().Be(KeyStatus.Pending);
        pending.KeyType.Should().Be(KeyType.X509CaCertificate);
        pending.CaCertificate.Should().NotBeNullOrEmpty();
    }

    private static GenerateKeyHandler Build(KeyCustodianTestDbContext db, TestClock clock) =>
        BuildWithCrypto(db, clock, KcAppTestKit.BuildTestRootCrypto());

    private static GenerateKeyHandler BuildWithCrypto(
        KeyCustodianTestDbContext db, TestClock clock, IPayloadCrypto crypto) =>
        new(
            KcAppTestKit.SystemContext<GenerateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildOptionsAccessor(),
            crypto,
            KcAppTestKit.BuildRootSigningCapability(db, crypto, clock),
            clock);
}
