// -----------------------------------------------------------------------
// <copyright file="RunDueRotationsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App;

using System.Collections.Concurrent;
using DcsvIo.D2.Handler.Abstractions;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetRotationPlan;
using Microsoft.Extensions.Logging;

/// <summary>
/// Tests for <see cref="RunDueRotationsHandler"/>: the full bootstrap / activate /
/// rotate / generate-successor / retire lifecycle paths, missing-bootstrap-key-type
/// skipping, per-domain failure isolation, and the empty-plan fast path.
/// </summary>
public sealed class RunDueRotationsTests
{
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    // -------------------------------------------------------------------------
    // Bootstrap path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Run_EmptyStore_BootstrapsAllMappedDomains_CaDomainsSkipped()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var input = InputWithAllKeyTypes();

        var result = await Build(db, clock).HandleAsync(input);

        var nonCaDomains = KeyDomain.All
            .Where(d => d.KeyType != KeyType.X509CaCertificate)
            .Select(d => d.Value)
            .ToList();

        result.Success.Should().BeTrue();
        result.Data!.Bootstrapped.Should().BeEquivalentTo(nonCaDomains);
        result.Data!.Errors.Should().Be(0);
        result.Data!.Skipped.Should().BeEquivalentTo(
            new[] { KeyDomain.MTLS_CA_ROOT, KeyDomain.MTLS_CA_INTERMEDIATE },
            because: "the CA domains are absent from the bootstrap map by design — the "
            + "CA seeder owns them; they are skipped, never bootstrapped");
        db.Keys.Count().Should().Be(nonCaDomains.Count);
    }

    [Fact]
    public async Task Run_Bootstrap_MissingKeyType_SkipsWithoutError()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);

        // Supply key types for all domains except "cookie".
        var keyTypes = InputWithAllKeyTypes().BootstrapKeyTypes
            .Where(kv => kv.Key != KeyDomain.COOKIE)
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var input = new RunDueRotationsInput(keyTypes);

        var result = await Build(db, clock).HandleAsync(input);

        result.Success.Should().BeTrue();
        result.Data!.Skipped.Should().Contain(KeyDomain.COOKIE);
        result.Data!.Errors.Should().Be(0);
        result.Data!.Bootstrapped.Should().NotContain(KeyDomain.COOKIE);
    }

    // -------------------------------------------------------------------------
    // Activate path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Run_SoakedPendingNoIncumbent_ActivatesDomain()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var pendingKid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, KeyDomain.COOKIE, KeyType.Secret, KeyStatus.Pending, created);

        // Past soak (1h); no active incumbent → DueToActivate.
        var clock = new TestClock(created + Duration.FromHours(2));

        var result = await Build(db, clock).HandleAsync(EmptyBootstrap());

        result.Success.Should().BeTrue();
        result.Data!.Activated.Should().Contain(KeyDomain.COOKIE);
        result.Data!.Errors.Should().Be(0);
        db.Keys.Single(k => k.Kid == pendingKid).Status.Should().Be(KeyStatus.Active);
    }

    // -------------------------------------------------------------------------
    // Rotate path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Run_ActiveCadenceElapsedAndSoakedSuccessor_RotatesDomain()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var activeKid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            KeyDomain.COOKIE,
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);
        var pendingKid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, KeyDomain.COOKIE, KeyType.Secret, KeyStatus.Pending, created);

        // Past cadence (4h); pending soak (1h) also elapsed.
        var clock = new TestClock(created + Duration.FromHours(5));

        var result = await Build(db, clock).HandleAsync(EmptyBootstrap());

        result.Success.Should().BeTrue();
        result.Data!.Rotated.Should().Contain(KeyDomain.COOKIE);
        result.Data!.Errors.Should().Be(0);
        db.Keys.Single(k => k.Kid == activeKid).Status.Should().Be(KeyStatus.Retiring);
        db.Keys.Single(k => k.Kid == pendingKid).Status.Should().Be(KeyStatus.Active);
    }

    // -------------------------------------------------------------------------
    // Generate-successor path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Run_ActiveCadenceElapsedNoSuccessor_GeneratesSuccessorKey()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            KeyDomain.COOKIE,
            KeyType.Secret,
            KeyStatus.Active,
            created,
            activatedAt: created);

        // Past cadence (4h); no pending successor.
        var clock = new TestClock(created + Duration.FromHours(5));

        var result = await Build(db, clock).HandleAsync(EmptyBootstrap());

        result.Success.Should().BeTrue();
        result.Data!.SuccessorsGenerated.Should().Contain(KeyDomain.COOKIE);
        result.Data!.Errors.Should().Be(0);

        // A second pending key should now exist for the domain.
        db.Keys.Count(k =>
            k.KeyDomain == KeyDomain.COOKIE && k.Status == KeyStatus.Pending)
            .Should().Be(1);
    }

    [Fact]
    public async Task Run_GenerateSuccessor_InheritsKeyTypeFromActiveKey()
    {
        // The generated successor must inherit the active key's KeyType (RsaSigning),
        // NOT default to a different type.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            KeyDomain.JWKS_SIGNING,
            KeyType.RsaSigning,
            KeyStatus.Active,
            created,
            activatedAt: created);

        var clock = new TestClock(created + Duration.FromHours(5));
        await Build(db, clock).HandleAsync(EmptyBootstrap());

        var successor = db.Keys.Single(k =>
            k.KeyDomain == KeyDomain.JWKS_SIGNING && k.Status == KeyStatus.Pending);
        successor.KeyType.Should().Be(KeyType.RsaSigning);
    }

    // -------------------------------------------------------------------------
    // Retire path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Run_RetiringGraceElapsed_RetiresDomain()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var retiringAt = created + Duration.FromHours(1);
        var retiringKid = await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            KeyDomain.COOKIE,
            KeyType.Secret,
            KeyStatus.Retiring,
            created,
            activatedAt: created,
            retiringAt: retiringAt);

        // Grace is 2h — past it.
        var clock = new TestClock(retiringAt + Duration.FromHours(2));

        var result = await Build(db, clock).HandleAsync(EmptyBootstrap());

        result.Success.Should().BeTrue();
        result.Data!.Retired.Should().Contain(KeyDomain.COOKIE);
        result.Data!.Errors.Should().Be(0);
        db.Keys.Single(k => k.Kid == retiringKid).Status.Should().Be(KeyStatus.Retired);
    }

    // -------------------------------------------------------------------------
    // Per-domain failure isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Run_ActivateWithCorruptMaterial_CountsErrorWithoutAbortingOthers()
    {
        // Seed "cookie" with a soaked pending key whose material is corrupt (unwraps
        // but fails smoke test → KEYCUSTODIAN_SMOKE_TEST_FAILED). The plan puts it in
        // DueToActivate; the activate sub-handler returns failure; Errors increments by 1.
        // Other empty domains continue to bootstrap (or activate/etc.) unaffected.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;

        // AES requires 16/24/32-byte keys; 3 bytes causes AesGcm to throw ArgumentException
        // inside SmokeTesting.Verify, which maps to KEYCUSTODIAN_SMOKE_TEST_FAILED.
        await KcAppTestKit.SeedKeyWithCorruptMaterialAsync(
            db,
            r_crypto,
            domain: KeyDomain.COOKIE,
            keyType: KeyType.AesPayload,
            status: KeyStatus.Pending,
            createdAt: created,
            corruptPlaintext: [0x01, 0x02, 0x03]);

        // Past soak (1h) — "cookie" is in DueToActivate.
        var clock = new TestClock(created + Duration.FromHours(2));
        var input = InputWithAllKeyTypes();
        var result = await Build(db, clock).HandleAsync(input);

        result.Success.Should().BeTrue(
            because: "per-domain errors increment Errors but do not abort the overall handler");
        result.Data!.Errors.Should().Be(1, because: "the corrupt activate is the only failure");
        result.Data!.Activated.Should().NotContain(KeyDomain.COOKIE);
    }

    [Fact]
    public async Task Run_DomainWithExistingPending_NotBootstrapped_OtherDomainsStillBootstrap()
    {
        // "cookie" already has a pending key; it is NOT in DomainsToBootstrap (the plan
        // only bootstraps domains with ZERO live keys). All other empty domains should
        // bootstrap successfully; 0 errors because cookie is simply not in the plan.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            KeyDomain.COOKIE,
            KeyType.Secret,
            KeyStatus.Pending,
            KcAppTestKit.SR_BaseInstant);

        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var input = InputWithAllKeyTypes();
        var result = await Build(db, clock).HandleAsync(input);

        result.Success.Should().BeTrue();
        result.Data!.Errors.Should().Be(0);
        result.Data!.Bootstrapped.Should().NotContain(
            KeyDomain.COOKIE,
            because: "cookie already has a live key and is not in the bootstrap plan");
        result.Data!.Bootstrapped.Count.Should().BeGreaterThan(
            0,
            because: "other empty domains should have been bootstrapped");
    }

    // -------------------------------------------------------------------------
    // Empty plan — no work to do
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Run_AllDomainsSkipped_EmptyBootstrapInput_ReturnsZeroEverything()
    {
        // All domains need bootstrap but input has no key types → all skipped.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var input = new RunDueRotationsInput(
            new Dictionary<string, KeyType>(StringComparer.OrdinalIgnoreCase));

        var result = await Build(db, clock).HandleAsync(input);

        result.Success.Should().BeTrue();
        result.Data!.Bootstrapped.Should().BeEmpty();
        result.Data!.Activated.Should().BeEmpty();
        result.Data!.Rotated.Should().BeEmpty();
        result.Data!.SuccessorsGenerated.Should().BeEmpty();
        result.Data!.Retired.Should().BeEmpty();
        result.Data!.Errors.Should().Be(0);
        result.Data!.Skipped.Should().BeEquivalentTo(KeyDomain.All.Select(d => d.Value));
    }

    // -------------------------------------------------------------------------
    // GetPlan failure → bubbles out
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Run_GetPlanFails_BubblesFailure()
    {
        // Arrange: supply a policy provider that always returns a failing result.
        // GetRotationPlanHandler's BubbleOnFailure gate fires on the first domain, causing
        // RunDueRotationsHandler to return the plan failure immediately (result.Success == false).
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var failingProvider = new FailingRotationPolicyProvider();

        var result = await Build(db, clock, policyProvider: failingProvider)
            .HandleAsync(EmptyBootstrap());

        result.Success.Should().BeFalse(
            "a failing plan provider must cause RunDueRotations to surface the plan failure");
    }

    // -------------------------------------------------------------------------
    // RecordGoneFromPlan — TOCTOU null branches
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Run_ActivateDomainRecordGone_CountsErrorLogsWarning()
    {
        // Arrange: DB is empty so the re-query for the pending record returns null.
        // Supply a stub plan that already names COOKIE in DueToActivate — this simulates
        // the TOCTOU gap where the plan classified the domain but the record vanished
        // before RunDueRotations re-queried it.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var stubbedPlan = new StubbedPlanHandler(new GetRotationPlanOutput(
            DomainsToBootstrap: Array.Empty<string>(),
            DueToActivate: [KeyDomain.COOKIE],
            DueToRotate: Array.Empty<string>(),
            DueToGenerateSuccessor: Array.Empty<string>(),
            DueToRetire: Array.Empty<string>()));

        var logger = new CapturingLogger<RunDueRotationsHandler>();

        var result = await Build(db, clock, planHandler: stubbedPlan, logger: logger)
            .HandleAsync(EmptyBootstrap());

        result.Success.Should().BeTrue(
            "a gone record is a per-domain error, not a whole-handler failure");
        result.Data!.Errors.Should().Be(1, "the gone-record path increments Errors");
        result.Data!.Activated.Should().NotContain(
            KeyDomain.COOKIE,
            "the domain must be absent from the success list when its record was gone");
        logger.Entries.Should().ContainSingle(
            e => e.EventId.Id == 9506 && e.Level == LogLevel.Warning,
            "RecordGoneFromPlan must emit a Warning with EventId 9506 for the activate path");
    }

    [Fact]
    public async Task Run_GenerateSuccessorDomainRecordGone_CountsErrorLogsWarning()
    {
        // Arrange: DB is empty; stub plan places COOKIE in DueToGenerateSuccessor.
        // The re-query for the active record returns null — TOCTOU path.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var stubbedPlan = new StubbedPlanHandler(new GetRotationPlanOutput(
            DomainsToBootstrap: Array.Empty<string>(),
            DueToActivate: Array.Empty<string>(),
            DueToRotate: Array.Empty<string>(),
            DueToGenerateSuccessor: [KeyDomain.COOKIE],
            DueToRetire: Array.Empty<string>()));

        var logger = new CapturingLogger<RunDueRotationsHandler>();

        var result = await Build(db, clock, planHandler: stubbedPlan, logger: logger)
            .HandleAsync(EmptyBootstrap());

        result.Success.Should().BeTrue();
        result.Data!.Errors.Should().Be(1, "the gone-record path increments Errors");
        result.Data!.SuccessorsGenerated.Should().NotContain(
            KeyDomain.COOKIE,
            "the domain must be absent from the success list when its record was gone");
        logger.Entries.Should().ContainSingle(
            e => e.EventId.Id == 9506 && e.Level == LogLevel.Warning,
            "RecordGoneFromPlan must emit a Warning with EventId 9506 " +
            "for the generate-successor path");
    }

    [Fact]
    public async Task Run_RetireDomainRecordGone_CountsErrorLogsWarning()
    {
        // Arrange: DB is empty; stub plan places COOKIE in DueToRetire.
        // The re-query for the retiring record returns null — TOCTOU path.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var stubbedPlan = new StubbedPlanHandler(new GetRotationPlanOutput(
            DomainsToBootstrap: Array.Empty<string>(),
            DueToActivate: Array.Empty<string>(),
            DueToRotate: Array.Empty<string>(),
            DueToGenerateSuccessor: Array.Empty<string>(),
            DueToRetire: [KeyDomain.COOKIE]));

        var logger = new CapturingLogger<RunDueRotationsHandler>();

        var result = await Build(db, clock, planHandler: stubbedPlan, logger: logger)
            .HandleAsync(EmptyBootstrap());

        result.Success.Should().BeTrue();
        result.Data!.Errors.Should().Be(1, "the gone-record path increments Errors");
        result.Data!.Retired.Should().NotContain(
            KeyDomain.COOKIE,
            "the domain must be absent from the success list when its record was gone");
        logger.Entries.Should().ContainSingle(
            e => e.EventId.Id == 9506 && e.Level == LogLevel.Warning,
            "RecordGoneFromPlan must emit a Warning with EventId 9506 for the retire path");
    }

    // -------------------------------------------------------------------------
    // CA-certificate domains — picked up by the generic orchestration
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Run_CaIntermediateCadenceElapsedAndSoakedSuccessor_RotatesCaDomain()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var (activeKid, _) = await KcAppTestKit.SeedCaAsync(db, r_crypto, created);
        var (pendingKid, _) = await KcAppTestKit.SeedCaAsync(db, r_crypto, created, KeyStatus.Pending);

        // Past cadence (4h); pending soak (1h) also elapsed.
        var clock = new TestClock(created + Duration.FromHours(5));

        var result = await Build(db, clock).HandleAsync(EmptyBootstrap());

        result.Success.Should().BeTrue();
        result.Data!.Rotated.Should().Contain(KeyDomain.MTLS_CA_INTERMEDIATE);
        result.Data!.Errors.Should().Be(0);
        db.Keys.Single(k => k.Kid == activeKid).Status.Should().Be(KeyStatus.Retiring);
        db.Keys.Single(k => k.Kid == pendingKid).Status.Should().Be(KeyStatus.Active);
    }

    [Fact]
    public async Task Run_CaIntermediateCadenceElapsedNoSuccessor_GeneratesCaSuccessor()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;

        // An active root is needed to sign the generated intermediate successor.
        await KcAppTestKit.SeedCaRootAsync(db, r_crypto, created);
        await KcAppTestKit.SeedCaAsync(db, r_crypto, created);

        var clock = new TestClock(created + Duration.FromHours(5));

        var result = await Build(db, clock).HandleAsync(EmptyBootstrap());

        result.Success.Should().BeTrue();
        result.Data!.SuccessorsGenerated.Should().Contain(KeyDomain.MTLS_CA_INTERMEDIATE);
        result.Data!.Errors.Should().Be(0);
        db.Keys.Count(k =>
            k.KeyDomain == KeyDomain.MTLS_CA_INTERMEDIATE && k.Status == KeyStatus.Pending)
            .Should().Be(1, because: "a CA successor was generated");
    }

    [Fact]
    public async Task Run_CaIntermediateRetiringGraceElapsed_RetiresCaDomain()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var (retiringKid, _) = await KcAppTestKit.SeedCaAsync(
            db, r_crypto, created, KeyStatus.Retiring);

        // Grace is 2h past the retiring instant (SeedCaAsync sets retiringAt = created).
        var clock = new TestClock(created + Duration.FromHours(3));

        var result = await Build(db, clock).HandleAsync(EmptyBootstrap());

        result.Success.Should().BeTrue();
        result.Data!.Retired.Should().Contain(KeyDomain.MTLS_CA_INTERMEDIATE);
        db.Keys.Single(k => k.Kid == retiringKid).Status.Should().Be(KeyStatus.Retired);
    }

    [Fact]
    public async Task Run_EmptyStore_CaDomainsExcludedFromBootstrap_ListedInSkipped()
    {
        // CA domains are seeded by CaSeedingService, never auto-bootstrapped. With
        // the real bootstrap map (which excludes the CA domains), the CA domains are
        // still skipped because they are absent from it.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);

        // The real bootstrap map (excludes CA domains).
        var input = new RunDueRotationsInput(
            DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Scheduling.Hosted.KeyRotationService.BuildBootstrapKeyTypes());

        var result = await Build(db, clock).HandleAsync(input);

        result.Success.Should().BeTrue();
        result.Data!.Bootstrapped.Should().NotContain(KeyDomain.MTLS_CA_ROOT);
        result.Data!.Bootstrapped.Should().NotContain(KeyDomain.MTLS_CA_INTERMEDIATE);
        result.Data!.Skipped.Should().Contain(KeyDomain.MTLS_CA_ROOT);
        result.Data!.Skipped.Should().Contain(KeyDomain.MTLS_CA_INTERMEDIATE);
        db.Keys.Should().NotContain(
            k => k.KeyDomain == KeyDomain.MTLS_CA_ROOT,
            because: "the CA root is seeded by CaSeedingService, never auto-bootstrapped");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a <see cref="RunDueRotationsInput"/> mirroring the production bootstrap
    /// map: every catalog domain's canonical bound <see cref="KeyDomain.KeyType"/>,
    /// EXCLUDING the CA domains (seeded by the CA seeder, never auto-bootstrapped —
    /// they surface in <c>Skipped</c> when the plan classifies them for bootstrap).
    /// </summary>
    private static RunDueRotationsInput InputWithAllKeyTypes()
    {
        var keyTypes = new Dictionary<string, KeyType>(StringComparer.OrdinalIgnoreCase);

        foreach (var domain in KeyDomain.All)
        {
            if (domain.KeyType == KeyType.X509CaCertificate)
                continue;

            keyTypes[domain.Value] = domain.KeyType;
        }

        return new RunDueRotationsInput(keyTypes);
    }

    private static RunDueRotationsInput EmptyBootstrap() =>
        new(new Dictionary<string, KeyType>(StringComparer.OrdinalIgnoreCase));

    private RunDueRotationsHandler Build(
        KeyCustodianTestDbContext db,
        TestClock clock,
        IRotationPolicyProvider? policyProvider = null,
        IGetRotationPlanHandler? planHandler = null,
        ILogger<RunDueRotationsHandler>? logger = null)
    {
        var announcer = new RecordingAnnouncer();
        var resolvedPolicy = policyProvider ?? KcAppTestKit.BuildPolicyProvider(r_options);
        var optionsAccessor = KcAppTestKit.BuildOptionsAccessor();

        // The orchestrator + its lifecycle sub-handlers all run on the System plane —
        // mirroring the scheduler worker's ISystemWorkScopeFactory.BeginAsync scope.
        var generateCtx = KcAppTestKit.SystemContext<GenerateKeyHandler>();
        var activateCtx = KcAppTestKit.SystemContext<ActivateKeyHandler>();
        var rotateCtx = KcAppTestKit.SystemContext<RotateKeyHandler>();
        var retireCtx = KcAppTestKit.SystemContext<RetireKeyHandler>();
        var planCtx = KcAppTestKit.Context<GetRotationPlanHandler>();

        var runCtx = KcAppTestKit.SystemContext(logger);

        var rootSigning = KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock);
        var generate = new GenerateKeyHandler(
            generateCtx,
            KcAppTestKit.NullClassifier(),
            db,
            optionsAccessor,
            r_crypto,
            rootSigning,
            clock);
        var activate = new ActivateKeyHandler(
            activateCtx,
            KcAppTestKit.NullClassifier(),
            db,
            resolvedPolicy,
            r_crypto,
            rootSigning,
            clock);
        var rotate = new RotateKeyHandler(
            rotateCtx,
            KcAppTestKit.NullClassifier(),
            db,
            resolvedPolicy,
            announcer,
            r_crypto,
            rootSigning,
            clock);
        var retire = new RetireKeyHandler(
            retireCtx, KcAppTestKit.NullClassifier(), db, resolvedPolicy, clock);
        var getPlan = planHandler
            ?? new GetRotationPlanHandler(planCtx, db, resolvedPolicy, clock);

        return new RunDueRotationsHandler(
            runCtx,
            db,
            getPlan,
            generate,
            activate,
            rotate,
            retire);
    }

    // -------------------------------------------------------------------------
    // Fakes / test infrastructure
    // -------------------------------------------------------------------------

    /// <summary>
    /// Stub plan handler that returns a pre-baked <see cref="GetRotationPlanOutput"/>,
    /// allowing tests to exercise <c>RunDueRotationsHandler</c> re-query paths in
    /// isolation from the real plan-computation logic.
    /// </summary>
    private sealed class StubbedPlanHandler(GetRotationPlanOutput output)
        : IGetRotationPlanHandler
    {
        public ValueTask<D2Result<GetRotationPlanOutput?>> HandleAsync(
            GetRotationPlanInput input,
            CancellationToken ct = default,
            HandlerOptions? options = null)
            => ValueTask.FromResult(D2Result<GetRotationPlanOutput?>.Ok(output));
    }

    /// <summary>
    /// Rotation-policy provider that always returns a failing result, causing
    /// <c>GetRotationPlanHandler</c>'s <c>BubbleOnFailure</c> gate to trip.
    /// </summary>
    private sealed class FailingRotationPolicyProvider : IRotationPolicyProvider
    {
        public D2Result<RotationPolicy> ForDomain(KeyDomain domain) =>
            KeyCustodianFailures<RotationPolicy>.InvalidRotationPolicy();
    }

    /// <summary>
    /// Thread-safe capturing logger for asserting log entries by EventId.
    /// </summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<(LogLevel Level, EventId EventId, string Message)> Entries { get; }
            = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Enqueue((logLevel, eventId, formatter(state, exception)));
    }
}
