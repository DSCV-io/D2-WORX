// -----------------------------------------------------------------------
// <copyright file="CompromiseKeyTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App;

/// <summary>
/// Tests for <see cref="CompromiseKeyHandler"/>: compromise a live key,
/// auto-generate a replacement pending, announce urgently, and NEVER persist or
/// log the raw operator reason. A missing reason is a 400 input error; a missing
/// kid among live keys is a 404. A post-commit announce failure does not fail the
/// handler.
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
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var announcer = new RecordingAnnouncer();
        var input = new CompromiseKeyInput { Kid = kid, Reason = _SENSITIVE_REASON };
        var result = await Build(db, new TestClock(created + Duration.FromHours(3)), announcer)
            .HandleAsync(input);

        result.Success.Should().BeTrue();
        result.Data!.CompromisedKid.Should().Be(kid);
        result.Data!.ReplacementKid.Should().NotBeNullOrEmpty();

        db.Keys.Single(k => k.Kid == kid).Status.Should().Be(KeyStatus.Compromised);
        db.Keys.Should().Contain(
            k => k.Status == KeyStatus.Pending, because: "a replacement was generated");
    }

    [Fact]
    public async Task Compromise_AnnouncesUrgently()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var announcer = new RecordingAnnouncer();
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
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var result = await Build(
                db,
                new TestClock(created + Duration.FromHours(3)),
                new RecordingAnnouncer())
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
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);

        await Build(
                db,
                new TestClock(created + Duration.FromHours(3)),
                new RecordingAnnouncer())
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
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var result = await Build(
                db,
                new TestClock(created + Duration.FromHours(3)),
                new RecordingAnnouncer())
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
        var created = KcAppTestKit.SR_BaseInstant;
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
                db,
                new TestClock(created + Duration.FromHours(3)),
                new RecordingAnnouncer())
            .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = "compromised" });

        result.ErrorCode.Should().Be("KEYCUSTODIAN_KEY_NOT_FOUND");
    }

    [Fact]
    public async Task Compromise_DoubleSubmit_SecondReturnsKeyNotFound()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var clock = new TestClock(created + Duration.FromHours(3));
        var first = await Build(db, clock, new RecordingAnnouncer())
            .HandleAsync(new CompromiseKeyInput
            {
                Kid = kid,
                Reason = "compromised",
                GenerateReplacement = false,
            });
        first.Success.Should().BeTrue();

        var second = await Build(db, clock, new RecordingAnnouncer())
            .HandleAsync(new CompromiseKeyInput
            {
                Kid = kid,
                Reason = "compromised",
                GenerateReplacement = false,
            });
        second.ErrorCode.Should().Be("KEYCUSTODIAN_KEY_NOT_FOUND");
    }

    [Fact]
    public async Task Compromise_AnnounceFails_HandlerStillReturnsOk()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "cookie",
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var failing = new RecordingAnnouncer(D2Result.ServiceUnavailable());
        var result = await Build(db, new TestClock(created + Duration.FromHours(3)), failing)
            .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = "compromised" });

        result.Success.Should().BeTrue();
        db.Keys.Single(k => k.Kid == kid).Status.Should().Be(KeyStatus.Compromised);
    }

    [Fact]
    public async Task Compromise_PendingKey_IsLive_CanBeCompromised()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, "cookie", KeyType.Secret, KeyStatus.Pending, created);

        var result = await Build(
                db,
                new TestClock(created + Duration.FromHours(1)),
                new RecordingAnnouncer())
            .HandleAsync(new CompromiseKeyInput
            {
                Kid = kid,
                Reason = "compromised",
                GenerateReplacement = false,
            });

        result.Success.Should().BeTrue();
        db.Keys.Single(k => k.Kid == kid).Status.Should().Be(KeyStatus.Compromised);
    }

    [Fact]
    public async Task Compromise_RetiringKey_MarksCompromisedAndGeneratesReplacement()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
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

        var announcer = new RecordingAnnouncer();
        var result = await Build(db, new TestClock(created + Duration.FromHours(2)), announcer)
            .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = _SENSITIVE_REASON });

        result.Success.Should().BeTrue();
        result.Data!.CompromisedKid.Should().Be(kid);
        result.Data!.ReplacementKid.Should().NotBeNullOrEmpty();

        db.Keys.Single(k => k.Kid == kid).Status.Should().Be(KeyStatus.Compromised);
        db.Keys.Should().Contain(
            k => k.Status == KeyStatus.Pending, because: "a replacement was generated");
    }

    // -----------------------------------------------------------------------
    // CA-certificate key: compromise generates a REAL CA replacement
    // -----------------------------------------------------------------------

    // A compromised intermediate CA gets a REAL root-signed replacement (a pending
    // CA in the same domain that chains to the active root) — not a skipped/null
    // replacement.
    [Fact]
    public async Task Compromise_CaIntermediate_GenerateReplacement_CreatesRealReplacementChainingToRoot()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var (_, rootCertDer) = await KcAppTestKit.SeedCaRootAsync(db, r_crypto, created);
        var (caKid, _) = await KcAppTestKit.SeedCaAsync(db, r_crypto, created);

        var announcer = new RecordingAnnouncer();
        var result = await Build(db, new TestClock(created + Duration.FromHours(3)), announcer)
            .HandleAsync(new CompromiseKeyInput
            {
                Kid = caKid,
                Reason = "CA private key exposed",
                GenerateReplacement = true,
            });

        result.Success.Should().BeTrue(because: "the CA compromise + replacement must succeed");
        result.Data!.CompromisedKid.Should().Be(caKid);
        result.Data!.ReplacementKid.Should().NotBeNullOrEmpty(
            because: "a real CA replacement is generated, not skipped");

        db.Keys.Single(k => k.Kid == caKid).Status.Should().Be(KeyStatus.Compromised);

        var replacement = db.Keys.Single(k =>
            k.KeyDomain == KeyDomain.MTLS_CA_INTERMEDIATE && k.Status == KeyStatus.Pending);
        replacement.KeyType.Should().Be(KeyType.X509CaCertificate);
        replacement.CaCertificate.Should().NotBeNullOrEmpty();
        CaTestAssertions.AssertChainsToRoot(replacement.CaCertificate!, rootCertDer);

        announcer.Calls.Should().ContainSingle().Which.Urgent.Should().BeTrue(
            because: "a compromise announce is urgent");
    }

    // GenerateReplacement=false leaves the compromised CA with no replacement.
    [Fact]
    public async Task Compromise_CaIntermediate_NoReplacementRequested_CompromisesOnly()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        await KcAppTestKit.SeedCaRootAsync(db, r_crypto, created);
        var (caKid, _) = await KcAppTestKit.SeedCaAsync(db, r_crypto, created);

        var result = await Build(db, new TestClock(created + Duration.FromHours(3)), new RecordingAnnouncer())
            .HandleAsync(new CompromiseKeyInput
            {
                Kid = caKid,
                Reason = "CA private key exposed",
                GenerateReplacement = false,
            });

        result.Success.Should().BeTrue();
        result.Data!.ReplacementKid.Should().BeNull();
        db.Keys.Should().NotContain(
            k => k.Status == KeyStatus.Pending, because: "no replacement was requested");
        db.Keys.Single(k => k.Kid == caKid).Status.Should().Be(KeyStatus.Compromised);
    }

    // Root compromise → a real self-signed replacement root (the re-anchor case).
    [Fact]
    public async Task Compromise_CaRoot_GenerateReplacement_CreatesSelfSignedReplacementRoot()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var (rootKid, _) = await KcAppTestKit.SeedCaRootAsync(db, r_crypto, created);

        var result = await Build(db, new TestClock(created + Duration.FromHours(3)), new RecordingAnnouncer())
            .HandleAsync(new CompromiseKeyInput
            {
                Kid = rootKid,
                Reason = "root key exposed",
                GenerateReplacement = true,
            });

        result.Success.Should().BeTrue();
        result.Data!.ReplacementKid.Should().NotBeNullOrEmpty();
        db.Keys.Single(k => k.Kid == rootKid).Status.Should().Be(KeyStatus.Compromised);

        var replacement = db.Keys.Single(k =>
            k.KeyDomain == KeyDomain.MTLS_CA_ROOT && k.Status == KeyStatus.Pending);
        replacement.KeyType.Should().Be(KeyType.X509CaCertificate);
        replacement.CaCertificate.Should().NotBeNullOrEmpty();
    }

    private CompromiseKeyHandler Build(
        KeyCustodianTestDbContext db, TestClock clock, RecordingAnnouncer announcer) =>
        new(
            KcAppTestKit.SystemContext<CompromiseKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            Options.Create(r_options),
            announcer,
            r_crypto,
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options),
            clock);
}
