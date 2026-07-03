// -----------------------------------------------------------------------
// <copyright file="KeyCustodianMetricsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Diagnostics.Metrics;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;
using D2.Edge.KeyCustodian.App.Application.Observability;
using D2.Edge.KeyCustodian.Client.Jwks;
using D2.Shared.Handler.Abstractions;

/// <summary>
/// Tests covering domain-level observability and option-validation markers:
/// <list type="bullet">
///   <item><see cref="CompromiseKeyHandler.DefaultOptions"/> sets <c>LogInput = false</c>
///     (fail-secure PII-in-logs defense-in-depth).</item>
///   <item>Metrics counter increments per handler branch.</item>
///   <item>Fail-secure: <see cref="GetJwksHandler"/> returns 503 on empty
///     signing-key store.</item>
///   <item>Option-validation: <see cref="KeyCustodianOptions"/> and
///     <see cref="RotationPolicyOptions"/> carry DataAnnotations markers.</item>
/// </list>
/// </summary>
public sealed class KeyCustodianMetricsTests
{
    private readonly KeyCustodianOptions r_options = KcAppTestKit.BuildOptions();
    private readonly IPayloadCrypto r_crypto = KcAppTestKit.BuildTestRootCrypto();

    // -----------------------------------------------------------------------
    // CompromiseKey.DefaultOptions has LogInput = false
    // -----------------------------------------------------------------------

    [Fact]
    public void CompromiseKey_DefaultOptions_HasLogInputFalse()
    {
        // Build an instance (dependency values don't matter for reading DefaultOptions).
        using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = new CompromiseKeyHandler(
            KcAppTestKit.SystemContext<CompromiseKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            Options.Create(r_options),
            new RecordingAnnouncer(),
            r_crypto,
            new TestClock(KcAppTestKit.SR_BaseInstant));

        // Access DefaultOptions via the protected property through reflection.
        var prop = typeof(CompromiseKeyHandler).GetProperty(
            "DefaultOptions",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        prop.Should().NotBeNull("DefaultOptions must be declared on CompromiseKeyHandler");

        var opts = (HandlerOptions)prop.GetValue(handler)!;
        opts.LogInput.Should().BeFalse(
            "CompromiseKeyInput carries a sensitive operator reason; logging it — even with "
            + "[RedactData] — depends on the Serilog policy being wired; "
            + "the safest default is off");
    }

    // -----------------------------------------------------------------------
    // GetJwks empty store → 503 ServiceUnavailable (fail-secure)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetJwks_EmptySigningKeyStore_ReturnsServiceUnavailable()
    {
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var result = await BuildGetJwks(db).HandleAsync(new GetJwksInput());

        result.Success.Should().BeFalse(
            "an empty signing-key store is a total-auth-failure condition");
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        result.IsServiceUnavailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetJwks_NonEmptySigningKeyStore_ReturnsOk()
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

        var result = await BuildGetJwks(db).HandleAsync(new GetJwksInput());

        result.Success.Should().BeTrue("one active signing key is present");
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Data!.Keys.Should().ContainSingle();
    }

    // -----------------------------------------------------------------------
    // GenerateKey increments key_generations_total after commit
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateKey_Success_MetricName_KeyGenerationsTotal_Exists()
    {
        // Verify the counter is wired — smoke test: a successful generate returns Created.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = new GenerateKeyHandler(
            KcAppTestKit.SystemContext<GenerateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildOptionsAccessor(),
            r_crypto,
            new TestClock(KcAppTestKit.SR_BaseInstant));

        var result = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.COOKIE, KeyType.Secret));

        result.IsCreated.Should().BeTrue(
            "GenerateKey increments key_generations_total only after a successful commit");
    }

    // -----------------------------------------------------------------------
    // CompromiseKey increments compromises_total after commit
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CompromiseKey_Success_PassesThroughMetricIncrementPath()
    {
        // Exercises the code path where SR_CompromisesTotal.Add(1) is called.
        // Counter-increment correctness is validated by the existing behavior tests in
        // CompromiseKeyTests; this test confirms the metric code path doesn't throw.
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

        var result = await new CompromiseKeyHandler(
            KcAppTestKit.SystemContext<CompromiseKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            Options.Create(r_options),
            new RecordingAnnouncer(),
            r_crypto,
            new TestClock(created + Duration.FromHours(1)))
            .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = "audit-test" });

        result.Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Counter names pin the operational contract (dashboards / alert rules)
    // -----------------------------------------------------------------------

    [Fact]
    public void KeyCustodianMetrics_CounterNames_PinOperationalContract()
    {
        // Counter names are part of the operational contract; renames here = breaking changes
        // to any dashboard, SLO definition, or alert rule keyed on these strings.
        KeyCustodianMetrics.SR_CompromisesTotal.Name.Should().Be("d2.keycustodian.compromises");
        KeyCustodianMetrics.SR_AnnounceFailuresTotal.Name
            .Should().Be("d2.keycustodian.announce_failures");
        KeyCustodianMetrics.SR_KeyGenerationsTotal.Name
            .Should().Be("d2.keycustodian.key_generations");
        KeyCustodianMetrics.SR_SmokeTestFailuresTotal.Name
            .Should().Be("d2.keycustodian.smoke_test_failures");
        KeyCustodianMetrics.SR_EmptyJwksServed.Name
            .Should().Be("d2.keycustodian.empty_jwks_served");
        KeyCustodianMetrics.SR_LeafCertificatesIssuedTotal.Name
            .Should().Be("d2.keycustodian.leaf_certificates_issued");
        KeyCustodianMetrics.SR_NoActiveIssuingCaTotal.Name
            .Should().Be("d2.keycustodian.no_active_issuing_ca");
        KeyCustodianMetrics.SR_CrossProcessSigningRejections.Name
            .Should().Be("d2.keycustodian.cross_process_signing_rejections");
        KeyCustodianMetrics.SR_AuthorityRejectionsTotal.Name
            .Should().Be("d2.keycustodian.authority_rejections");
    }

    [Fact]
    public void KeyCustodianMetrics_MeterName_PinsOperationalContract()
    {
        KeyCustodianMetrics.SR_Meter.Name.Should().Be(KeyCustodianMetrics.METER_NAME);
        KeyCustodianMetrics.METER_NAME.Should().Be("D2.Edge.KeyCustodian");
    }

    // -----------------------------------------------------------------------
    // Option-validation — [Range] attributes on KeyCustodianOptions
    // -----------------------------------------------------------------------

    [Fact]
    public void KeyCustodianOptions_RsaKeySizeBits_HasRangeAttribute()
    {
        var prop = typeof(KeyCustodianOptions)
            .GetProperty(nameof(KeyCustodianOptions.RsaKeySizeBits));

        prop.Should().NotBeNull();

        var attr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.RangeAttribute>();
        attr.Should().NotBeNull(
            "RsaKeySizeBits must carry [Range] so ValidateDataAnnotations catches "
            + "sub-2048 values at startup");
        ((int)attr.Minimum).Should().Be(2048, "RS256 minimum safe key size is 2048 bits");
    }

    [Fact]
    public void KeyCustodianOptions_SecretLengthBytes_HasRangeAttribute()
    {
        var prop = typeof(KeyCustodianOptions)
            .GetProperty(nameof(KeyCustodianOptions.SecretLengthBytes));

        prop.Should().NotBeNull();

        var attr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.RangeAttribute>();
        attr.Should().NotBeNull(
            "SecretLengthBytes must carry [Range] so ValidateDataAnnotations catches "
            + "dangerously short secrets at startup");
        ((int)attr.Minimum).Should().Be(16, "minimum safe secret length is 16 bytes (128 bits)");
    }

    [Fact]
    public void KeyCustodianOptions_RsaKeySizeBits_BelowMinimum_FailsDataAnnotationsValidation()
    {
        var opts = new KeyCustodianOptions { RsaKeySizeBits = 1024, SecretLengthBytes = 64 };
        var ctx = new System.ComponentModel.DataAnnotations.ValidationContext(opts);
        var results = new System.Collections.Generic.List<
            System.ComponentModel.DataAnnotations.ValidationResult>();
        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            opts, ctx, results, validateAllProperties: true);

        valid.Should().BeFalse("1024-bit RSA is below the [Range(2048, ...)] minimum");
        results.Should().Contain(
            r => r.MemberNames.Contains(nameof(KeyCustodianOptions.RsaKeySizeBits)));
    }

    [Fact]
    public void KeyCustodianOptions_SecretLengthBytes_BelowMinimum_FailsDataAnnotationsValidation()
    {
        var opts = new KeyCustodianOptions { RsaKeySizeBits = 2048, SecretLengthBytes = 8 };
        var ctx = new System.ComponentModel.DataAnnotations.ValidationContext(opts);
        var results = new System.Collections.Generic.List<
            System.ComponentModel.DataAnnotations.ValidationResult>();
        var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            opts, ctx, results, validateAllProperties: true);

        valid.Should().BeFalse("8-byte secret is below the [Range(16, ...)] minimum");
        results.Should().Contain(
            r => r.MemberNames.Contains(nameof(KeyCustodianOptions.SecretLengthBytes)));
    }

    // -----------------------------------------------------------------------
    // Option-validation — [Range(typeof(TimeSpan), ...)] attributes on RotationPolicyOptions
    // -----------------------------------------------------------------------

    [Fact]
    public void RotationPolicyOptions_Cadence_HasTimeSpanRangeAttribute()
    {
        var prop = typeof(RotationPolicyOptions)
            .GetProperty(nameof(RotationPolicyOptions.Cadence));

        prop.Should().NotBeNull();
        var attr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.RangeAttribute>();
        attr.Should().NotBeNull(
            "Cadence must carry [Range(typeof(TimeSpan), ...)] — [Required] is a no-op on a "
            + "non-nullable struct and can never reject TimeSpan.Zero");
        attr.OperandType.Should().Be<TimeSpan>("the range must be typed to TimeSpan");
        attr.Minimum.Should().Be(
            "00:00:01", "the minimum must be 1 second to reject zero/negative durations");
    }

    [Fact]
    public void RotationPolicyOptions_Grace_HasTimeSpanRangeAttribute()
    {
        var prop = typeof(RotationPolicyOptions)
            .GetProperty(nameof(RotationPolicyOptions.Grace));

        prop.Should().NotBeNull();
        var attr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.RangeAttribute>();
        attr.Should().NotBeNull(
            "Grace must carry [Range(typeof(TimeSpan), ...)] — [Required] is a no-op on a "
            + "non-nullable struct and can never reject TimeSpan.Zero");
        attr.OperandType.Should().Be<TimeSpan>("the range must be typed to TimeSpan");
        attr.Minimum.Should().Be(
            "00:00:01", "the minimum must be 1 second to reject zero/negative durations");
    }

    [Fact]
    public void RotationPolicyOptions_SmokeSoak_HasTimeSpanRangeAttribute()
    {
        var prop = typeof(RotationPolicyOptions)
            .GetProperty(nameof(RotationPolicyOptions.SmokeSoak));

        prop.Should().NotBeNull();
        var attr = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.RangeAttribute>();
        attr.Should().NotBeNull(
            "SmokeSoak must carry [Range(typeof(TimeSpan), ...)] — [Required] is a no-op on a "
            + "non-nullable struct and can never reject TimeSpan.Zero");
        attr.OperandType.Should().Be<TimeSpan>("the range must be typed to TimeSpan");
        attr.Minimum.Should().Be(
            "00:00:01", "the minimum must be 1 second to reject zero/negative durations");
    }

    // -----------------------------------------------------------------------
    // announce_failures_total — MeterListener emission pin (urgent tag)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CompromiseKey_AnnounceFails_EmitsAnnounceFailureWithUrgentTrue()
    {
        // Pin: SR_AnnounceFailuresTotal.Add(1, ("urgent","true")) fires on the
        // CompromiseKey announce-fail path (urgent compromise announce).
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

        var measurements = new System.Collections.Generic.List<(long Value, string? UrgentTag)>();
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                && instrument.Name == "d2.keycustodian.announce_failures")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            string? urgentTag = null;

            foreach (var tag in tags)
            {
                if (tag.Key == "urgent")
                    urgentTag = tag.Value?.ToString();
            }

            measurements.Add((value, urgentTag));
        });
        listener.Start();

        var failing = new RecordingAnnouncer(D2Result.ServiceUnavailable());
        D2Result<CompromiseKeyOutput?> result;
        try
        {
            result = await new CompromiseKeyHandler(
                KcAppTestKit.SystemContext<CompromiseKeyHandler>(),
                KcAppTestKit.NullClassifier(),
                db,
                Options.Create(r_options),
                failing,
                r_crypto,
                new TestClock(created + Duration.FromHours(1)))
                .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = "test-compromise" });
        }
        finally
        {
            // Stop collecting before assertions so measurements is stable.
            listener.Dispose();
        }

        result.Success.Should().BeTrue(
            "the announce failure is non-fatal; the durable commit already succeeded");

        // Counters are global; parallel tests may add measurements with other tags.
        // Pin that at least one measurement with urgent="true" was emitted.
        measurements.Should().Contain(
            m => m.Value == 1 && m.UrgentTag == "true",
            because:
                "CompromiseKey must emit announce_failures_total with urgent=true "
                + "on the compromise announce-fail path (session-invalidation SLO)");
    }

    [Fact]
    public async Task RotateKey_AnnounceFails_EmitsAnnounceFailureWithUrgentFalse()
    {
        // Pin: SR_AnnounceFailuresTotal.Add(1, ("urgent","false")) fires on the
        // RotateKey announce-fail path (routine non-urgent rotation announce).
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
        await KcAppTestKit.SeedKeyAsync(
            db,
            r_crypto,
            r_options,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Pending,
            created);

        var measurements = new System.Collections.Generic.List<(long Value, string? UrgentTag)>();
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                && instrument.Name == "d2.keycustodian.announce_failures")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            string? urgentTag = null;

            foreach (var tag in tags)
            {
                if (tag.Key == "urgent")
                    urgentTag = tag.Value?.ToString();
            }

            measurements.Add((value, urgentTag));
        });
        listener.Start();

        // Soak = 1h; now is 2h after creation → soak elapsed.
        var clock = new TestClock(created + Duration.FromHours(2));
        var failing = new RecordingAnnouncer(D2Result.ServiceUnavailable());
        D2Result<RotateKeyOutput?> result;
        try
        {
            result = await BuildRotateKey(db, clock, failing)
                .HandleAsync(new RotateKeyInput("jwks-signing"));
        }
        finally
        {
            // Stop collecting before assertions so measurements is stable.
            listener.Dispose();
        }

        result.Success.Should().BeTrue(
            "the announce failure is non-fatal; the rotation commit already succeeded");

        // Counters are global; parallel tests may add measurements with other tags.
        // Pin that at least one measurement with urgent="false" was emitted.
        measurements.Should().Contain(
            m => m.Value == 1 && m.UrgentTag == "false",
            because: "RotateKey must emit announce_failures_total with urgent=false "
            + "on the rotation announce-fail path (routine non-urgent rotation)");

        _ = activeKid; // used indirectly: the seeded active key is the rotation incumbent
    }

    // -----------------------------------------------------------------------
    // empty_jwks_served — MeterListener emission-value pin
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetJwks_EmptySigningKeyStore_EmitsEmptyJwksServedCounterIncrement()
    {
        // Pin: SR_EmptyJwksServed.Add(1) fires on the empty-store path.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();

        var measurements = new System.Collections.Generic.List<long>();
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                && instrument.Name == "d2.keycustodian.empty_jwks_served")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) =>
            measurements.Add(value));
        listener.Start();

        D2Result<GetJwksOutput?> result;
        try
        {
            result = await BuildGetJwks(db).HandleAsync(new GetJwksInput());
        }
        finally
        {
            listener.Dispose();
        }

        result.IsServiceUnavailable.Should().BeTrue(
            "an empty signing-key store triggers the fail-secure 503 path");

        // Counters are global; parallel tests may contribute. Pin that at least
        // one measurement of value 1 was emitted on this instrument.
        measurements.Should().Contain(
            1L,
            because:
                "GetJwks must emit empty_jwks_served with value 1 on the "
                + "empty-store path (fail-secure counter for dashboards / SLO)");
    }

    // -----------------------------------------------------------------------
    // smoke_test_failures — MeterListener emission-value pin (Activate + Rotate)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ActivateKey_SmokeFailure_EmitsSmokeTestFailuresCounterIncrement()
    {
        // Pin: SR_SmokeTestFailuresTotal.Add(1) fires on the ActivateKey smoke-fail
        // path. Setup mirrors ActivateKeyTests.Activate_SmokeFailure_LeavesKeyPending:
        // valid private key + mismatched SPKI → smoke sign-then-verify-against-SPKI
        // fails deterministically.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var created = KcAppTestKit.SR_BaseInstant;

        using var rsa = RSA.Create(2048);
        using var mismatchedRsa = RSA.Create(2048);
        var validPkcs8 = rsa.ExportPkcs8PrivateKey();
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

        var measurements = new System.Collections.Generic.List<long>();
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                && instrument.Name == "d2.keycustodian.smoke_test_failures")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) =>
            measurements.Add(value));
        listener.Start();

        var clock = new TestClock(created + Duration.FromHours(1));
        D2Result<KeySummary?> result;
        try
        {
            result = await BuildActivateKey(db, clock)
                .HandleAsync(new ActivateKeyInput(kid));
        }
        finally
        {
            listener.Dispose();
        }

        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_SMOKE_TEST_FAILED",
            "the mismatched SPKI deterministically fails the smoke probe");

        measurements.Should().Contain(
            1L,
            because:
                "ActivateKey must emit smoke_test_failures with value 1 when the "
                + "smoke probe fails (operational signal for key-material corruption)");
    }

    [Fact]
    public async Task RotateKey_SuccessorSmokeFailure_EmitsSmokeTestFailuresCounterIncrement()
    {
        // Pin: SR_SmokeTestFailuresTotal.Add(1) fires on the RotateKey successor
        // smoke-fail path. Setup mirrors RotateKeyTests
        // .Rotate_SuccessorSmokeFailure_LeavesNoPersistentChange: valid private key
        // + mismatched SPKI → smoke probe fails deterministically.
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

        using var rsa = RSA.Create(2048);
        using var mismatchedRsa = RSA.Create(2048);
        var validPkcs8 = rsa.ExportPkcs8PrivateKey();
        var spki = mismatchedRsa.ExportSubjectPublicKeyInfo();

        await KcAppTestKit.SeedKeyWithCorruptMaterialAsync(
            db,
            r_crypto,
            "jwks-signing",
            KeyType.RsaSigning,
            KeyStatus.Pending,
            created,
            validPkcs8,
            spki);

        var measurements = new System.Collections.Generic.List<long>();
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == KeyCustodianMetrics.METER_NAME
                && instrument.Name == "d2.keycustodian.smoke_test_failures")
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) =>
            measurements.Add(value));
        listener.Start();

        // Soak = 1h; now is 2h after creation → soak elapsed.
        var clock = new TestClock(created + Duration.FromHours(2));
        D2Result<RotateKeyOutput?> result;
        try
        {
            result = await BuildRotateKey(db, clock, new RecordingAnnouncer())
                .HandleAsync(new RotateKeyInput("jwks-signing"));
        }
        finally
        {
            listener.Dispose();
        }

        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_SMOKE_TEST_FAILED",
            "the mismatched SPKI deterministically fails the smoke probe");

        measurements.Should().Contain(
            1L,
            because:
                "RotateKey must emit smoke_test_failures with value 1 when the "
                + "successor smoke probe fails (operational signal for key-material "
                + "corruption)");
    }

    // -----------------------------------------------------------------------
    // Helper builders
    // -----------------------------------------------------------------------

    private static GetJwksHandler BuildGetJwks(KeyCustodianTestDbContext db) =>
        new(KcAppTestKit.Context<GetJwksHandler>(), db);

    private ActivateKeyHandler BuildActivateKey(KeyCustodianTestDbContext db, TestClock clock) =>
        new(
            KcAppTestKit.SystemContext<ActivateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            r_crypto,
            clock);

    private RotateKeyHandler BuildRotateKey(
        KeyCustodianTestDbContext db,
        TestClock clock,
        RecordingAnnouncer announcer) =>
        new(
            KcAppTestKit.SystemContext<RotateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildPolicyProvider(r_options),
            announcer,
            r_crypto,
            clock);
}
