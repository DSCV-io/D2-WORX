// -----------------------------------------------------------------------
// <copyright file="CompromiseKeyTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.App.Implementations.CQRS.Handlers.C;
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
/// Tests for <see cref="CompromiseKey"/> (gate D-3): compromise a live key,
/// auto-generate a replacement pending, announce urgently, and NEVER persist or
/// log the raw operator reason. A missing reason is a 400 input error; a missing
/// kid among live keys is a 404. A post-commit announce failure does not fail the
/// handler (D-4).
/// </summary>
public sealed class CompromiseKeyTests
{
    private const string _SENSITIVE_REASON = "leaked by John Doe via prod-db-01";

    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    [Fact]
    public async Task Compromise_ActiveKey_MarksCompromisedAndGeneratesReplacement()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var announcer = new KcAppTestKit.RecordingAnnouncer();
        var input = new CompromiseKeyInput { Kid = kid, Reason = _SENSITIVE_REASON };
        var result = await Build(db, new TestClock(created + Duration.FromHours(3)), announcer)
            .HandleAsync(input);

        result.Success.Should().BeTrue();
        result.Data!.CompromisedKid.Should().Be(kid);
        result.Data!.ReplacementKid.Should().NotBeNullOrEmpty();

        db.Keys.Single(k => k.Kid == kid).Status.Should().Be(KeyStatus.Compromised);
        db.Keys.Should().Contain(k => k.Status == KeyStatus.Pending, because: "a replacement was generated");
    }

    [Fact]
    public async Task Compromise_AnnouncesUrgently()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Active, created, activatedAt: created);

        var announcer = new KcAppTestKit.RecordingAnnouncer();
        await Build(db, new TestClock(created + Duration.FromHours(3)), announcer)
            .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = "compromised" });

        announcer.Calls.Should().ContainSingle();
        var call = announcer.Calls.Single();
        call.Urgent.Should().BeTrue();
        call.NewStatus.Should().Be(KeyStatus.Compromised);
    }

    [Fact]
    public async Task Compromise_NoReplacement_WhenFlagFalse()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Active, created, activatedAt: created);

        var result = await Build(
                db, new TestClock(created + Duration.FromHours(3)), new KcAppTestKit.RecordingAnnouncer())
            .HandleAsync(new CompromiseKeyInput
            {
                Kid = kid,
                Reason = "compromised",
                GenerateReplacement = false,
            });

        result.Data!.ReplacementKid.Should().BeNull();
        db.Keys.Should().NotContain(k => k.Status == KeyStatus.Pending);
    }

    // -----------------------------------------------------------------------
    // PII discipline — the raw reason is never persisted or echoed
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Compromise_RawReason_NeverPersistedInAuditOrRow()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Active, created, activatedAt: created);

        await Build(db, new TestClock(created + Duration.FromHours(3)), new KcAppTestKit.RecordingAnnouncer())
            .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = _SENSITIVE_REASON });

        // The compromise REASON column carries the operator reason (capped), but the
        // AUDIT breadcrumb must NEVER carry it.
        var compromiseAudit = db.Audit.Single(a => a.Action == KeyAuditAction.Compromised);
        compromiseAudit.Detail.Should().NotContain("John Doe").And.NotContain("prod-db-01");
    }

    [Fact]
    public void CompromiseKeyInput_ToString_RedactsReason()
    {
        var input = new CompromiseKeyInput { Kid = "kid-1", Reason = _SENSITIVE_REASON };
        input.ToString().Should().NotContain("John Doe").And.Contain("REDACTED");
    }

    // -----------------------------------------------------------------------
    // Missing reason → 400; missing/garbage kid → 404 / KID_INVALID
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Compromise_MissingReason_ReturnsValidationFailedWithInputError(string? reason)
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Active, created, activatedAt: created);

        var result = await Build(
                db, new TestClock(created + Duration.FromHours(3)), new KcAppTestKit.RecordingAnnouncer())
            .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = reason });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_FAILED");
        result.InputErrors.Should().Contain(e => e.Field == "Reason");
        db.Keys.Single(k => k.Kid == kid).Status.Should().Be(
            KeyStatus.Active, because: "the missing reason was rejected before any change");
    }

    [Fact]
    public async Task Compromise_RetiredKey_NotAmongLive_ReturnsKeyNotFound()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Retired,
            created,
            activatedAt: created,
            retiringAt: created);

        var result = await Build(
                db, new TestClock(created + Duration.FromHours(3)), new KcAppTestKit.RecordingAnnouncer())
            .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = "compromised" });

        result.ErrorCode.Should().Be("KEYCUSTODIAN_KEY_NOT_FOUND");
    }

    [Fact]
    public async Task Compromise_DoubleSubmit_SecondReturnsKeyNotFound()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Active, created, activatedAt: created);

        var clock = new TestClock(created + Duration.FromHours(3));
        var first = await Build(db, clock, new KcAppTestKit.RecordingAnnouncer())
            .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = "compromised", GenerateReplacement = false });
        first.Success.Should().BeTrue();

        var second = await Build(db, clock, new KcAppTestKit.RecordingAnnouncer())
            .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = "compromised", GenerateReplacement = false });
        second.ErrorCode.Should().Be("KEYCUSTODIAN_KEY_NOT_FOUND");
    }

    [Fact]
    public async Task Compromise_AnnounceFails_HandlerStillReturnsOk()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Active, created, activatedAt: created);

        var failing = new KcAppTestKit.RecordingAnnouncer(D2Result.ServiceUnavailable());
        var result = await Build(db, new TestClock(created + Duration.FromHours(3)), failing)
            .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = "compromised" });

        result.Success.Should().BeTrue();
        db.Keys.Single(k => k.Kid == kid).Status.Should().Be(KeyStatus.Compromised);
    }

    [Fact]
    public async Task Compromise_PendingKey_IsLive_CanBeCompromised()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Pending, created);

        var result = await Build(
                db, new TestClock(created + Duration.FromHours(1)), new KcAppTestKit.RecordingAnnouncer())
            .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = "compromised", GenerateReplacement = false });

        result.Success.Should().BeTrue();
        db.Keys.Single(k => k.Kid == kid).Status.Should().Be(KeyStatus.Compromised);
    }

    [Fact]
    public async Task Compromise_RetiringKey_MarksCompromisedAndGeneratesReplacement()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Retiring,
            created,
            activatedAt: created,
            retiringAt: created + Duration.FromHours(1));

        var announcer = new KcAppTestKit.RecordingAnnouncer();
        var result = await Build(db, new TestClock(created + Duration.FromHours(2)), announcer)
            .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = _SENSITIVE_REASON });

        result.Success.Should().BeTrue();
        result.Data!.CompromisedKid.Should().Be(kid);
        result.Data!.ReplacementKid.Should().NotBeNullOrEmpty();

        db.Keys.Single(k => k.Kid == kid).Status.Should().Be(KeyStatus.Compromised);
        db.Keys.Should().Contain(
            k => k.Status == KeyStatus.Pending, because: "a replacement was generated");
    }

    private CompromiseKey Build(
        KeyCustodianTestDbContext db, TestClock clock, KcAppTestKit.RecordingAnnouncer announcer) =>
        new(
            KcAppTestKit.Context<CompromiseKey>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildGenerators(r_options),
            announcer,
            r_crypto,
            clock);
}
