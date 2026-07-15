// -----------------------------------------------------------------------
// <copyright file="RetireKeyTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;

/// <summary>
/// Tests for <see cref="RetireKeyHandler"/> — happy path, not-found / wrong-state
/// conflicts, and the TEMPORAL-ADVERSARIAL grace boundary (§25 mandate). KC
/// timestamps are Cat-2 bare <see cref="Instant"/> (zone-free); DST / IANA N/A.
/// </summary>
public sealed class RetireKeyTests
{
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    [Fact]
    public async Task Retire_GraceElapsed_RetiresAndAudits()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var retiringAt = created + Duration.FromHours(5);
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Retiring,
            created,
            activatedAt: created + Duration.FromHours(1),
            retiringAt: retiringAt);

        // Grace is 2h — retire at exactly grace elapsed.
        var result = await Build(db, new TestClock(retiringAt + Duration.FromHours(2)))
            .HandleAsync(new RetireKeyInput(kid));

        result.Success.Should().BeTrue();
        result.Data!.Status.Should().Be(KeyStatus.Retired);
        db.Audit.Should().Contain(a => a.Action == KeyAuditAction.Retired);
    }

    [Fact]
    public async Task Retire_ExactlyAtGraceBoundary_Succeeds()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var retiringAt = created + Duration.FromHours(5);
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Retiring,
            created,
            activatedAt: created + Duration.FromHours(1),
            retiringAt: retiringAt);

        (await Build(db, new TestClock(retiringAt + Duration.FromHours(2)))
            .HandleAsync(new RetireKeyInput(kid))).Success.Should().BeTrue();
    }

    [Fact]
    public async Task Retire_OneTickBeforeGraceBoundary_ReturnsGraceNotElapsed()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var retiringAt = created + Duration.FromHours(5);
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Retiring,
            created,
            activatedAt: created + Duration.FromHours(1),
            retiringAt: retiringAt);

        var clock = new TestClock(retiringAt + Duration.FromHours(2) - Duration.FromNanoseconds(1));
        var result = await Build(db, clock).HandleAsync(new RetireKeyInput(kid));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_GRACE_NOT_ELAPSED");
        db.Keys.Single().Status.Should().Be(KeyStatus.Retiring);
    }

    [Fact]
    public async Task Retire_PendingKey_ReturnsKeyStateConflict()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Pending, created);

        var result = await Build(db, new TestClock(created + Duration.FromHours(10)))
            .HandleAsync(new RetireKeyInput(kid));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_KEY_STATE_CONFLICT");
    }

    [Fact]
    public async Task Retire_UnknownKid_ReturnsKeyNotFound()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var result = await Build(db, new TestClock(KcAppTestKit.SR_BaseInstant))
            .HandleAsync(new RetireKeyInput("nopeKid"));

        result.ErrorCode.Should().Be("KEYCUSTODIAN_KEY_NOT_FOUND");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("bad kid")]
    public async Task Retire_BadKid_ReturnsKidInvalid(string? kid)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var result = await Build(db, new TestClock(KcAppTestKit.SR_BaseInstant))
            .HandleAsync(new RetireKeyInput(kid));

        result.ErrorCode.Should().Be("KEYCUSTODIAN_KID_INVALID");
    }

    private RetireKeyHandler Build(KeyCustodianTestDbContext db, TestClock clock) =>
        new(
            KcAppTestKit.SystemContext<RetireKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            clock);
}
