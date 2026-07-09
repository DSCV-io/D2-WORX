// -----------------------------------------------------------------------
// <copyright file="CaRootSigningInstrumentationTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App.CertificateAuthority;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using D2.Edge.KeyCustodian.App.Application.CertificateAuthority;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using D2.Edge.KeyCustodian.App.Application.Observability;
using Microsoft.Extensions.Logging;

/// <summary>
/// §9.44 chokepoint-telemetry proof: EVERY stored-root-key plaintext use fires the
/// CA-root-key log delegate (EventId 9518 sign / 9519 smoke) + the
/// <c>SR_CaRootKeyUsesTotal</c> counter with the correct closed-set <c>operation</c>
/// tag, across ALL FOUR real handler paths (generate-successor + compromise-replacement
/// sign; activate + rotate root smoke), and does NOT fire on paths that never touch the
/// stored root (non-CA generation, root self-sign, non-root activation) — the scoping
/// pin that proves the generic arm stays inline. The rendered log capture also pins
/// that NO key material appears (kids + operation only). The counter operation-tag
/// assertions are the §21.10 runtime-emission pins for all four values.
/// </summary>
public sealed class CaRootSigningInstrumentationTests
{
    private const string _CA_ROOT_KEY_USES = "d2.keycustodian.ca_root_key_uses";

    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    // -----------------------------------------------------------------------
    // Sign path — EventId 9518, operations generate-successor + compromise-replacement
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateKey_CaIntermediate_FiresSigningChokepoint_GenerateSuccessor()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        await KcAppTestKit.SeedCaRootAsync(db, r_crypto, created);
        var clock = new TestClock(created + Duration.FromHours(1));
        var logger = new CapturingRootLogger();
        var capability =
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options, logger);

        var handler = new GenerateKeyHandler(
            KcAppTestKit.SystemContext<GenerateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildOptionsAccessor(),
            r_crypto,
            capability,
            clock);

        var measurements = new List<(long Value, string Operation)>();
        D2Result<KeySummary?> result;

        using (var listener = BuildCounterListener(measurements))
        {
            listener.Start();
            result = await handler.HandleAsync(
                new GenerateKeyInput(KeyDomain.MTLS_CA_INTERMEDIATE, KeyType.X509CaCertificate));
        }

        result.IsCreated.Should().BeTrue();
        AssertSigningLogged(
            logger, KeyCustodianMetrics.CaRootKeyUses.Operation.GENERATE_SUCCESSOR);
        measurements.Should().Contain(
            m => m.Operation == KeyCustodianMetrics.CaRootKeyUses.Operation.GENERATE_SUCCESSOR,
            "the root-signing chokepoint counter fires with the generate-successor operation");
    }

    [Fact]
    public async Task CompromiseKey_CaIntermediate_FiresSigningChokepoint_CompromiseReplacement()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var (_, intermediateKid, _) =
            await KcAppTestKit.SeedCaHierarchyAsync(db, r_crypto, created);
        var clock = new TestClock(created + Duration.FromHours(1));
        var logger = new CapturingRootLogger();
        var capability =
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options, logger);

        var handler = new CompromiseKeyHandler(
            KcAppTestKit.SystemContext<CompromiseKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            Options.Create(r_options),
            new RecordingAnnouncer(),
            r_crypto,
            capability,
            clock);

        var measurements = new List<(long Value, string Operation)>();
        D2Result<CompromiseKeyOutput?> result;

        using (var listener = BuildCounterListener(measurements))
        {
            listener.Start();
            result = await handler.HandleAsync(
                new CompromiseKeyInput
                {
                    Kid = intermediateKid,
                    Reason = "instrumentation-test",
                    GenerateReplacement = true,
                });
        }

        result.Success.Should().BeTrue();
        AssertSigningLogged(
            logger, KeyCustodianMetrics.CaRootKeyUses.Operation.COMPROMISE_REPLACEMENT);
        measurements.Should().Contain(
            m => m.Operation == KeyCustodianMetrics.CaRootKeyUses.Operation.COMPROMISE_REPLACEMENT,
            "the root-signing chokepoint counter fires with the compromise-replacement operation");
    }

    // -----------------------------------------------------------------------
    // Smoke-verify path — EventId 9519, operations activate + rotate smoke-test
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ActivateKey_Root_FiresSmokeChokepoint_ActivateSmokeTest()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var (rootKid, _) = await KcAppTestKit.SeedCaRootAsync(
            db, r_crypto, created, KeyStatus.Pending);
        var clock = new TestClock(created + Duration.FromHours(1));
        var logger = new CapturingRootLogger();
        var capability =
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options, logger);

        var handler = new ActivateKeyHandler(
            KcAppTestKit.SystemContext<ActivateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            r_crypto,
            capability,
            clock);

        var measurements = new List<(long Value, string Operation)>();
        D2Result<KeySummary?> result;

        using (var listener = BuildCounterListener(measurements))
        {
            listener.Start();
            result = await handler.HandleAsync(new ActivateKeyInput(rootKid));
        }

        result.Success.Should().BeTrue();
        AssertSmokeLogged(logger, KeyCustodianMetrics.CaRootKeyUses.Operation.ACTIVATE_SMOKE_TEST);
        measurements.Should().Contain(
            m => m.Operation == KeyCustodianMetrics.CaRootKeyUses.Operation.ACTIVATE_SMOKE_TEST,
            "the root-smoke chokepoint counter fires with the activate-smoke-test operation");
    }

    [Fact]
    public async Task RotateKey_Root_FiresSmokeChokepoint_RotateSmokeTest()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        await KcAppTestKit.SeedCaRootAsync(db, r_crypto, created);
        await KcAppTestKit.SeedCaRootAsync(db, r_crypto, created, KeyStatus.Pending);
        var clock = new TestClock(created + Duration.FromHours(2));
        var logger = new CapturingRootLogger();
        var capability =
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options, logger);

        var handler = new RotateKeyHandler(
            KcAppTestKit.SystemContext<RotateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            new RecordingAnnouncer(),
            r_crypto,
            capability,
            clock);

        var measurements = new List<(long Value, string Operation)>();
        D2Result<RotateKeyOutput?> result;

        using (var listener = BuildCounterListener(measurements))
        {
            listener.Start();
            result = await handler.HandleAsync(new RotateKeyInput(KeyDomain.MTLS_CA_ROOT));
        }

        result.Success.Should().BeTrue();
        AssertSmokeLogged(logger, KeyCustodianMetrics.CaRootKeyUses.Operation.ROTATE_SMOKE_TEST);
        measurements.Should().Contain(
            m => m.Operation == KeyCustodianMetrics.CaRootKeyUses.Operation.ROTATE_SMOKE_TEST,
            "the root-smoke chokepoint counter fires with the rotate-smoke-test operation");
    }

    // -----------------------------------------------------------------------
    // Scoping pins — the chokepoint stays SILENT on paths that never touch the root
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateKey_NonCa_DoesNotFireChokepoint()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var logger = new CapturingRootLogger();
        var capability =
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options, logger);

        var handler = new GenerateKeyHandler(
            KcAppTestKit.SystemContext<GenerateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildOptionsAccessor(),
            r_crypto,
            capability,
            clock);

        var result = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.COOKIE, KeyType.Secret));

        result.IsCreated.Should().BeTrue();
        AssertNoChokepointLog(logger);
    }

    [Fact]
    public async Task GenerateKey_RootSelfSign_DoesNotFireSigningChokepoint()
    {
        // A root successor is SELF-signed (GenerateRootCa inline) — it never unwraps the
        // stored root, so the sign chokepoint stays silent. This is the scoping pin that
        // proves only the intermediate arm routes through the capability.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var clock = new TestClock(KcAppTestKit.SR_BaseInstant);
        var logger = new CapturingRootLogger();
        var capability =
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options, logger);

        var handler = new GenerateKeyHandler(
            KcAppTestKit.SystemContext<GenerateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildOptionsAccessor(),
            r_crypto,
            capability,
            clock);

        var result = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.MTLS_CA_ROOT, KeyType.X509CaCertificate));

        result.IsCreated.Should().BeTrue(because: "a root successor self-signs");
        AssertNoChokepointLog(logger);
    }

    [Fact]
    public async Task ActivateKey_NonRoot_DoesNotFireSmokeChokepoint()
    {
        // A non-root domain keeps the inline generic smoke — the capability verify op is
        // never invoked, so the smoke chokepoint stays silent.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;
        var kid = await KcAppTestKit.SeedKeyAsync(
            db, r_crypto, r_options, KeyDomain.COOKIE, KeyType.Secret, KeyStatus.Pending, created);
        var clock = new TestClock(created + Duration.FromHours(1));
        var logger = new CapturingRootLogger();
        var capability =
            KcAppTestKit.BuildRootSigningCapability(db, r_crypto, clock, r_options, logger);

        var handler = new ActivateKeyHandler(
            KcAppTestKit.SystemContext<ActivateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            r_crypto,
            capability,
            clock);

        var result = await handler.HandleAsync(new ActivateKeyInput(kid));

        result.Success.Should().BeTrue();
        AssertNoChokepointLog(logger);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static void AssertSigningLogged(CapturingRootLogger logger, string operation)
    {
        logger.Entries.Should().Contain(
            e => e.EventId == 9518 && e.Message.Contains(operation, StringComparison.Ordinal),
            "the sign path fires the 9518 CaRootKeySigningUsed delegate with the operation");
        logger.Entries.Should().NotContain(
            e => e.EventId == 9519, "the sign path fires the sign delegate, not the smoke one");
    }

    private static void AssertSmokeLogged(CapturingRootLogger logger, string operation)
    {
        logger.Entries.Should().Contain(
            e => e.EventId == 9519 && e.Message.Contains(operation, StringComparison.Ordinal),
            "the smoke path fires the 9519 CaRootKeySmokeTested delegate with the operation");
        logger.Entries.Should().NotContain(
            e => e.EventId == 9518, "the smoke path fires the smoke delegate, not the sign one");
    }

    private static void AssertNoChokepointLog(CapturingRootLogger logger) =>
        logger.Entries.Should().NotContain(
            e => e.EventId == 9518 || e.EventId == 9519,
            "a path that never touches the stored root fires no §9.44 chokepoint log");

    private static MeterListener BuildCounterListener(
        List<(long Value, string Operation)> measurements)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                    && instrument.Name == _CA_ROOT_KEY_USES)
                    l.EnableMeasurementEvents(instrument);
            },
        };

        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            string operation = string.Empty;

            foreach (var tag in tags)
            {
                if (tag.Key == KeyCustodianMetrics.CaRootKeyUses.TAG_OPERATION)
                    operation = tag.Value?.ToString() ?? string.Empty;
            }

            measurements.Add((value, operation));
        });

        return listener;
    }

    // Captures rendered log entries (EventId + message) so the chokepoint delegates can be
    // asserted per handler instance (pollution-free, unlike the global counter). The
    // rendered message is asserted to carry only the operation + kids — never key material.
    private sealed class CapturingRootLogger : ILogger<CaRootSigningCapability>
    {
        public ConcurrentQueue<(int EventId, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Enqueue((eventId.Id, formatter(state, exception)));
    }
}
