// -----------------------------------------------------------------------
// <copyright file="ActivateKeyTests.cs" company="DCSV">
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
using D2.Shared.Time;
using NodaTime;
using Xunit;

/// <summary>
/// Tests for <see cref="ActivateKey"/> — happy path, not-found / wrong-state
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
        var created = KcAppTestKit.BaseInstant;
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
        var created = KcAppTestKit.BaseInstant;
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
        var created = KcAppTestKit.BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Pending, created);

        // One nanosecond short of the 1h soak.
        var clock = new TestClock(created + Duration.FromHours(1) - Duration.FromNanoseconds(1));
        var result = await Build(db, clock).HandleAsync(new ActivateKeyInput(kid));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_SOAK_NOT_ELAPSED");
        db.Keys.Single().Status.Should().Be(KeyStatus.Pending, because: "the activation was rejected");
    }

    [Fact]
    public async Task Activate_ClockBehindCreatedAt_NegativeElapsed_ReturnsSoakNotElapsed()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
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
        var result = await Build(db, new TestClock(KcAppTestKit.BaseInstant))
            .HandleAsync(new ActivateKeyInput(kid));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_KID_INVALID");
    }

    [Fact]
    public async Task Activate_UnknownKid_ReturnsKeyNotFound()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var result = await Build(db, new TestClock(KcAppTestKit.BaseInstant))
            .HandleAsync(new ActivateKeyInput("nonexistentKid123"));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_KEY_NOT_FOUND");
    }

    [Fact]
    public async Task Activate_AlreadyActiveKey_ReturnsKeyStateConflict()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
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
        var created = KcAppTestKit.BaseInstant;
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
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Pending, created);

        // Soak elapsed but the smoke tester always returns failure.
        var clock = new TestClock(created + Duration.FromHours(1));
        var result = await Build(db, clock, new KcAppTestKit.FailingSmokeTester())
            .HandleAsync(new ActivateKeyInput(kid));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_SMOKE_TEST_FAILED");
        db.Keys.Single().Status.Should().Be(KeyStatus.Pending, because: "activation was rejected");
        db.Audit.Should().BeEmpty(because: "no state transition occurred");
    }

    private ActivateKey Build(KeyCustodianTestDbContext db, TestClock clock) =>
        Build(db, clock, KcAppTestKit.BuildSmokeTester());

    private ActivateKey Build(KeyCustodianTestDbContext db, TestClock clock, ISmokeTester smokeTester) =>
        new(
            KcAppTestKit.Context<ActivateKey>(),
            KcAppTestKit.NullClassifier(),
            db,
            smokeTester,
            KcAppTestKit.BuildPolicyProvider(r_options),
            r_crypto,
            clock);
}
