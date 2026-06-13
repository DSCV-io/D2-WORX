// -----------------------------------------------------------------------
// <copyright file="RotateKeyTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

/// <summary>
/// Tests for the atomic-swap <see cref="RotateKeyHandler"/>: both the
/// incumbent → retiring and successor → active transitions land in ONE save, the
/// announce fires non-urgently, and a post-commit announce failure does NOT fail
/// the handler. Missing incumbent / successor → 404.
/// </summary>
public sealed class RotateKeyTests
{
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    [Fact]
    public async Task Rotate_AtomicSwap_RetiresIncumbentAndActivatesSuccessor()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var (activeKid, pendingKid) = await SeedActiveAndSoakedPending(db, created);

        // Now is past soak (1h) for the pending successor.
        var clock = new TestClock(created + Duration.FromHours(2));
        var announcer = new RecordingAnnouncer();
        var result = await Build(db, clock, announcer)
            .HandleAsync(new RotateKeyInput("jwks-signing"));

        result.Success.Should().BeTrue();
        result.Data!.RetiringKid.Should().Be(activeKid);
        result.Data!.ActivatedKid.Should().Be(pendingKid);

        db.Keys.Single(k => k.Kid == activeKid).Status.Should().Be(KeyStatus.Retiring);
        db.Keys.Single(k => k.Kid == pendingKid).Status.Should().Be(KeyStatus.Active);

        db.Audit.Should().Contain(a => a.Action == KeyAuditAction.Rotated);
        db.Audit.Should().Contain(a => a.Action == KeyAuditAction.Activated);
    }

    [Fact]
    public async Task Rotate_AnnouncesNonUrgentlyForActivatedKey()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var (_, pendingKid) = await SeedActiveAndSoakedPending(db, created);

        var announcer = new RecordingAnnouncer();
        await Build(db, new TestClock(created + Duration.FromHours(2)), announcer)
            .HandleAsync(new RotateKeyInput("jwks-signing"));

        announcer.Calls.Should().ContainSingle();
        var call = announcer.Calls.Single();
        call.Urgent.Should().BeFalse();
        call.Kid.Should().Be(pendingKid);
        call.NewStatus.Should().Be(KeyStatus.Active);
    }

    [Fact]
    public async Task Rotate_AnnounceFails_HandlerStillReturnsOk_StateCommitted()
    {
        // Post-commit announce: a failure must NOT fail the handler.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var (activeKid, pendingKid) = await SeedActiveAndSoakedPending(db, created);

        var failing = new RecordingAnnouncer(D2Result.ServiceUnavailable());
        var result = await Build(db, new TestClock(created + Duration.FromHours(2)), failing)
            .HandleAsync(new RotateKeyInput("jwks-signing"));

        result.Success.Should().BeTrue(because: "rotation is durable; the announce is best-effort");
        db.Keys.Single(k => k.Kid == activeKid).Status.Should().Be(KeyStatus.Retiring);
        db.Keys.Single(k => k.Kid == pendingKid).Status.Should().Be(KeyStatus.Active);
    }

    [Fact]
    public async Task Rotate_NoActiveIncumbent_ReturnsKeyNotFound()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Pending,
            created);

        var result = await Build(
                db,
                new TestClock(created + Duration.FromHours(2)),
                new RecordingAnnouncer())
            .HandleAsync(new RotateKeyInput("jwks-signing"));

        result.ErrorCode.Should().Be("KEYCUSTODIAN_KEY_NOT_FOUND");
    }

    [Fact]
    public async Task Rotate_NoPendingSuccessor_ReturnsKeyNotFound()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var result = await Build(
                db,
                new TestClock(created + Duration.FromHours(2)),
                new RecordingAnnouncer())
            .HandleAsync(new RotateKeyInput("jwks-signing"));

        result.ErrorCode.Should().Be("KEYCUSTODIAN_KEY_NOT_FOUND");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-domain")]
    public async Task Rotate_BadDomain_ReturnsUnknownKeyDomain(string? domain)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var result = await Build(
                db, new TestClock(KcAppTestKit.SR_BaseInstant), new RecordingAnnouncer())
            .HandleAsync(new RotateKeyInput(domain));

        result.ErrorCode.Should().Be("KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN");
    }

    [Fact]
    public async Task Rotate_SuccessorNotYetSoaked_ReturnsSoakNotElapsed()
    {
        // Successor created at now-(soak−1ns) — one nanosecond short of soak.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var soakDuration = Duration.FromHours(1);
        var now = KcAppTestKit.SR_BaseInstant + Duration.FromHours(3);
        var successorCreated = now - soakDuration + Duration.FromNanoseconds(1);

        var activeKid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            KcAppTestKit.SR_BaseInstant,
            activatedAt: KcAppTestKit.SR_BaseInstant);
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Pending,
            successorCreated);

        var clock = new TestClock(now);
        var result = await Build(db, clock, new RecordingAnnouncer())
            .HandleAsync(new RotateKeyInput("jwks-signing"));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_SOAK_NOT_ELAPSED");
        db.Keys.Single(k => k.Kid == activeKid).Status.Should().Be(
            KeyStatus.Active, because: "no state change on soak-not-elapsed");
    }

    [Fact]
    public async Task Rotate_SuccessorSmokeFailure_LeavesNoPersistentChange()
    {
        // The successor's private key is valid but its STORED SPKI belongs to a
        // DIFFERENT RSA key — the smoke sign-then-verify-against-SPKI step
        // deterministically returns false → SMOKE_TEST_FAILED. The incumbent's
        // material stays valid and its state must not change.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;

        var activeKid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created);

        using var rsa = RSA.Create(2048);
        using var mismatchedRsa = RSA.Create(2048);
        var validPkcs8 = rsa.ExportPkcs8PrivateKey();

        // Well-formed private key, but the STORED SPKI is from a DIFFERENT key — the
        // smoke sign-then-verify-against-SPKI fails deterministically → SMOKE_TEST_FAILED.
        var spki = mismatchedRsa.ExportSubjectPublicKeyInfo();
        var pendingKid = await KcAppTestKit.SeedKeyWithCorruptMaterialAsync(
            db,
            r_crypto,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Pending,
            created,
            validPkcs8,
            spki);

        var clock = new TestClock(created + Duration.FromHours(2));
        var result = await Build(db, clock, new RecordingAnnouncer())
            .HandleAsync(new RotateKeyInput("jwks-signing"));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_SMOKE_TEST_FAILED");
        db.Keys.Single(k => k.Kid == activeKid).Status.Should().Be(
            KeyStatus.Active, because: "incumbent must not change on smoke failure");
        db.Keys.Single(k => k.Kid == pendingKid).Status.Should().Be(
            KeyStatus.Pending, because: "successor must not change on smoke failure");
        db.Audit.Should().BeEmpty(because: "no state transition occurred");
    }

    private RotateKeyHandler Build(
        KeyCustodianTestDbContext db, TestClock clock, RecordingAnnouncer announcer) =>
        new(
            KcAppTestKit.Context<RotateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            announcer,
            r_crypto,
            clock);

    private async Task<(string Active, string Pending)> SeedActiveAndSoakedPending(
        KeyCustodianTestDbContext db, Instant created)
    {
        var active = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created);
        var pending = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Pending,
            created);
        return (active, pending);
    }
}
