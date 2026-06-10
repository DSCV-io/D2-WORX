// -----------------------------------------------------------------------
// <copyright file="RotateKeyTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.App.Implementations.CQRS.Handlers.C;
using D2.Edge.KeyCustodian.App.Interfaces.Crypto;
using D2.Edge.KeyCustodian.App.Models;
using D2.Edge.KeyCustodian.App.Options;
using D2.Edge.KeyCustodian.Domain.Audit;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Shared.Encryption;
using D2.Shared.Result;
using D2.Shared.Time;
using NodaTime;
using Xunit;

/// <summary>
/// Tests for the atomic-swap <see cref="RotateKey"/> (gate D-2): both the
/// incumbent → retiring and successor → active transitions land in ONE save, the
/// announce fires non-urgently, and a post-commit announce failure does NOT fail
/// the handler (D-4). Missing incumbent / successor → 404.
/// </summary>
public sealed class RotateKeyTests
{
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    [Fact]
    public async Task Rotate_AtomicSwap_RetiresIncumbentAndActivatesSuccessor()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var (activeKid, pendingKid) = await SeedActiveAndSoakedPending(db, created);

        // Now is past soak (1h) for the pending successor.
        var clock = new TestClock(created + Duration.FromHours(2));
        var announcer = new KcAppTestKit.RecordingAnnouncer();
        var result = await Build(db, clock, announcer).HandleAsync(new RotateKeyInput("jwks-signing"));

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
        var created = KcAppTestKit.BaseInstant;
        var (_, pendingKid) = await SeedActiveAndSoakedPending(db, created);

        var announcer = new KcAppTestKit.RecordingAnnouncer();
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
        // D-4: a post-commit announce failure must NOT fail the handler.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var (activeKid, pendingKid) = await SeedActiveAndSoakedPending(db, created);

        var failing = new KcAppTestKit.RecordingAnnouncer(D2Result.ServiceUnavailable());
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
        var created = KcAppTestKit.BaseInstant;
        await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "jwks-signing", KeyType.RsaSigning, KeyStatus.Pending, created);

        var result = await Build(
                db, new TestClock(created + Duration.FromHours(2)), new KcAppTestKit.RecordingAnnouncer())
            .HandleAsync(new RotateKeyInput("jwks-signing"));

        result.ErrorCode.Should().Be("KEYCUSTODIAN_KEY_NOT_FOUND");
    }

    [Fact]
    public async Task Rotate_NoPendingSuccessor_ReturnsKeyNotFound()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
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
                db, new TestClock(created + Duration.FromHours(2)), new KcAppTestKit.RecordingAnnouncer())
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
                db, new TestClock(KcAppTestKit.BaseInstant), new KcAppTestKit.RecordingAnnouncer())
            .HandleAsync(new RotateKeyInput(domain));

        result.ErrorCode.Should().Be("KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN");
    }

    [Fact]
    public async Task Rotate_SuccessorNotYetSoaked_ReturnsSoakNotElapsed()
    {
        // Successor created at now-(soak−1ns) — one nanosecond short of soak.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var soakDuration = Duration.FromHours(1);
        var now = KcAppTestKit.BaseInstant + Duration.FromHours(3);
        var successorCreated = now - soakDuration + Duration.FromNanoseconds(1);

        var activeKid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Active,
            KcAppTestKit.BaseInstant,
            activatedAt: KcAppTestKit.BaseInstant);
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Pending,
            successorCreated);

        var clock = new TestClock(now);
        var result = await Build(db, clock, new KcAppTestKit.RecordingAnnouncer())
            .HandleAsync(new RotateKeyInput("jwks-signing"));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_SOAK_NOT_ELAPSED");
        db.Keys.Single(k => k.Kid == activeKid).Status.Should().Be(
            KeyStatus.Active, because: "no state change on soak-not-elapsed");
    }

    [Fact]
    public async Task Rotate_SuccessorSmokeFailure_LeavesNoPersistentChange()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var (activeKid, pendingKid) = await SeedActiveAndSoakedPending(db, created);

        var clock = new TestClock(created + Duration.FromHours(2));
        var result = await Build(
                db, clock, new KcAppTestKit.RecordingAnnouncer(), new KcAppTestKit.FailingSmokeTester())
            .HandleAsync(new RotateKeyInput("jwks-signing"));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_SMOKE_TEST_FAILED");
        db.Keys.Single(k => k.Kid == activeKid).Status.Should().Be(
            KeyStatus.Active, because: "incumbent must not change on smoke failure");
        db.Keys.Single(k => k.Kid == pendingKid).Status.Should().Be(
            KeyStatus.Pending, because: "successor must not change on smoke failure");
        db.Audit.Should().BeEmpty(because: "no state transition occurred");
    }

    private RotateKey Build(
        KeyCustodianTestDbContext db, TestClock clock, KcAppTestKit.RecordingAnnouncer announcer) =>
        Build(db, clock, announcer, KcAppTestKit.BuildSmokeTester());

    private RotateKey Build(
        KeyCustodianTestDbContext db,
        TestClock clock,
        KcAppTestKit.RecordingAnnouncer announcer,
        ISmokeTester smokeTester) =>
        new(
            KcAppTestKit.Context<RotateKey>(),
            KcAppTestKit.NullClassifier(),
            db,
            smokeTester,
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
            db, r_crypto, r_options, "jwks-signing", KeyType.RsaSigning, KeyStatus.Pending, created);
        return (active, pending);
    }
}
