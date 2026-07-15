// -----------------------------------------------------------------------
// <copyright file="ActivateKeyTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;

/// <summary>
/// Tests for <see cref="ActivateKeyHandler"/> — happy path, not-found / wrong-state
/// conflicts, smoke-failure (no persisted change), and the TEMPORAL-ADVERSARIAL
/// soak boundary (§25 mandate). Every KC timestamp is a Cat-2 bare
/// <see cref="Instant"/> (zone-free) — DST / IANA cases are N/A.
/// </summary>
public sealed class ActivateKeyTests
{
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    [Fact]
    public async Task Activate_SoakElapsed_ActivatesAndAudits()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Pending, created);

        // Soak is 1h — activate at exactly soak elapsed.
        var clock = new TestClock(created + Duration.FromHours(1));
        var result = await Build(db, clock).HandleAsync(new ActivateKeyInput(kid));

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(KeyStatus.Active);
        db.Keys.Single().Status.Should().Be(KeyStatus.Active);
        db.Audit.Should().Contain(a => a.Action == KeyAuditAction.Activated);
    }

    // -----------------------------------------------------------------------
    // TEMPORAL-ADVERSARIAL — soak boundary at the elapsed instant
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Activate_ExactlyAtSoakBoundary_Succeeds()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Pending, created);

        // elapsed == SmokeSoak (>= boundary passes).
        var clock = new TestClock(created + Duration.FromHours(1));
        (await Build(db, clock).HandleAsync(new ActivateKeyInput(kid))).Success.Should().BeTrue();
    }

    [Fact]
    public async Task Activate_OneTickBeforeSoakBoundary_ReturnsSoakNotElapsed()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Pending, created);

        // One nanosecond short of the 1h soak.
        var clock = new TestClock(created + Duration.FromHours(1) - Duration.FromNanoseconds(1));
        var result = await Build(db, clock).HandleAsync(new ActivateKeyInput(kid));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_SOAK_NOT_ELAPSED");
        db.Keys.Single().Status.Should().Be(
            KeyStatus.Pending, because: "the activation was rejected");
    }

    [Fact]
    public async Task Activate_ClockBehindCreatedAt_NegativeElapsed_ReturnsSoakNotElapsed()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Pending, created);

        // Non-monotonic clock: now is BEFORE created → negative elapsed, no overflow.
        var clock = new TestClock(created - Duration.FromHours(5));
        var result = await Build(db, clock).HandleAsync(new ActivateKeyInput(kid));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_SOAK_NOT_ELAPSED");
    }

    // -----------------------------------------------------------------------
    // Not-found / wrong-state / adversarial inputs
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("bad kid with spaces")]
    public async Task Activate_BadKid_ReturnsKidInvalid(string? kid)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var result = await Build(db, new TestClock(KcAppTestKit.SR_BaseInstant))
            .HandleAsync(new ActivateKeyInput(kid));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_KID_INVALID");
    }

    [Fact]
    public async Task Activate_UnknownKid_ReturnsKeyNotFound()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var result = await Build(db, new TestClock(KcAppTestKit.SR_BaseInstant))
            .HandleAsync(new ActivateKeyInput("nonexistentKid123"));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_KEY_NOT_FOUND");
    }

    [Fact]
    public async Task Activate_AlreadyActiveKey_ReturnsKeyStateConflict()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Active, created);

        var result = await Build(db, new TestClock(created + Duration.FromHours(2)))
            .HandleAsync(new ActivateKeyInput(kid));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_KEY_STATE_CONFLICT");
    }

    [Fact]
    public async Task Activate_DoubleSubmit_SecondReturnsConflict()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Pending, created);

        var clock = new TestClock(created + Duration.FromHours(1));
        var first = await Build(db, clock).HandleAsync(new ActivateKeyInput(kid));
        first.Success.Should().BeTrue();

        var second = await Build(db, clock).HandleAsync(new ActivateKeyInput(kid));
        second.ErrorCode.Should().Be("KEYCUSTODIAN_KEY_STATE_CONFLICT");
    }

    [Fact]
    public async Task Activate_SmokeFailure_LeavesKeyPendingAndNoAudit()
    {
        // Smoke fails via a key/SPKI mismatch: the stored private key is valid but
        // the stored SPKI belongs to a DIFFERENT RSA key, so the smoke sign-then-
        // verify-against-SPKI step deterministically returns false → SMOKE_TEST_FAILED.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;

        using var rsa = RSA.Create(2048);
        using var mismatchedRsa = RSA.Create(2048);
        var validPkcs8 = rsa.ExportPkcs8PrivateKey();

        // Well-formed private key, but the STORED SPKI is from a DIFFERENT key — the
        // smoke sign-then-verify-against-SPKI fails deterministically → SMOKE_TEST_FAILED.
        var spki = mismatchedRsa.ExportSubjectPublicKeyInfo();

        var kid = await KcAppTestKit.SeedKeyWithCorruptMaterialAsync(
            db,
            r_crypto,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Pending,
            created,
            validPkcs8,
            spki);

        // Soak elapsed but the real material fails its smoke probe.
        var clock = new TestClock(created + Duration.FromHours(1));
        var result = await Build(db, clock).HandleAsync(new ActivateKeyInput(kid));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_SMOKE_TEST_FAILED");
        db.Keys.Single().Status.Should().Be(KeyStatus.Pending, because: "activation was rejected");
        db.Audit.Should().BeEmpty(because: "no state transition occurred");
    }

    // -----------------------------------------------------------------------
    // Root-domain smoke routes through the dedicated §9.44 root-signing capability
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Activate_PendingRoot_RoutesSmokeThroughCapability_Activates()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var (rootKid, _) = await KcAppTestKit.SeedCaRootAsync(
            db, r_crypto, created, KeyStatus.Pending);

        // Soak is 1h — activate at soak elapsed. The mtls-ca-root smoke unwrap routes
        // through the capability verify op (never inline); a valid root smoke-passes.
        var clock = new TestClock(created + Duration.FromHours(1));
        var result = await Build(db, clock).HandleAsync(new ActivateKeyInput(rootKid));

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(KeyStatus.Active);
        db.Keys.Single().Status.Should().Be(KeyStatus.Active);
        db.Audit.Should().Contain(a => a.Action == KeyAuditAction.Activated);
    }

    [Fact]
    public async Task Activate_PendingRoot_CorruptMaterial_SmokeFailsViaCapability_LeavesPending()
    {
        // The corrupt-material adversarial (decryptable-but-INVALID): the wrapped root
        // material unwraps cleanly but is not a valid PKCS#8 ECDSA key, so the capability's
        // smoke-verify op returns the SAME KEYCUSTODIAN_SMOKE_TEST_FAILED the inline path
        // would — routing changes WHERE the plaintext materializes, not WHETHER corruption
        // is detected. No state change on failure.
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

        // Give the corrupt pending root CA cert material so it rehydrates as a CA key.
        var corruptRow = db.Keys.Single(k => k.Kid == kid);
        corruptRow.CaCertificate = RandomNumberGenerator.GetBytes(32);
        await db.SaveChangesAsync();

        var clock = new TestClock(created + Duration.FromHours(1));
        var result = await Build(db, clock).HandleAsync(new ActivateKeyInput(kid));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_SMOKE_TEST_FAILED");
        db.Keys.Single().Status.Should().Be(KeyStatus.Pending, because: "activation was rejected");
        db.Audit.Should().BeEmpty(because: "no state transition occurred");
    }

    private ActivateKeyHandler Build(KeyCustodianTestDbContext db, TestClock clock) =>
        new(
            KcAppTestKit.SystemContext<ActivateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            r_crypto,
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options),
            clock);
}
