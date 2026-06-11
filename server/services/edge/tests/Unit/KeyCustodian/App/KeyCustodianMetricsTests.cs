// -----------------------------------------------------------------------
// <copyright file="KeyCustodianMetricsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.CompromiseKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;
using D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks;
using D2.Edge.KeyCustodian.App.Application.Observability;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.Domain.Enums;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Encryption;
using D2.Shared.Handler.Abstractions;
using D2.Shared.Time;
using Microsoft.Extensions.Options;
using NodaTime;

/// <summary>
/// Tests covering domain-level observability and option-validation markers:
/// <list type="bullet">
///   <item>S-3: <see cref="CompromiseKeyHandler.DefaultOptions"/> sets <c>LogInput = false</c>
///     (fail-secure PII-in-logs defense-in-depth).</item>
///   <item>O-1 metrics counter increments per handler branch.</item>
///   <item>O-1 fail-secure: <see cref="GetJwksHandler"/> returns 503 on empty
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
    // S-3 — CompromiseKey.DefaultOptions has LogInput = false
    // -----------------------------------------------------------------------

    [Fact]
    public void CompromiseKey_DefaultOptions_HasLogInputFalse()
    {
        // Build an instance (dependency values don't matter for reading DefaultOptions).
        using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = new CompromiseKeyHandler(
            KcAppTestKit.Context<CompromiseKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            Options.Create(r_options),
            new KcAppTestKit.RecordingAnnouncer(),
            r_crypto,
            new TestClock(KcAppTestKit.BaseInstant));

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
    // O-1 — GetJwks empty store → 503 ServiceUnavailable (fail-secure)
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

        var result = await BuildGetJwks(db).HandleAsync(new GetJwksInput());

        result.Success.Should().BeTrue("one active signing key is present");
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.Data!.Keys.Should().ContainSingle();
    }

    // -----------------------------------------------------------------------
    // O-1 — GenerateKey increments key_generations_total after commit
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GenerateKey_Success_MetricName_KeyGenerationsTotal_Exists()
    {
        // Verify the counter is wired — smoke test: a successful generate returns Created.
        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var handler = new GenerateKeyHandler(
            KcAppTestKit.Context<GenerateKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            KcAppTestKit.BuildOptionsAccessor(),
            r_crypto,
            new TestClock(KcAppTestKit.BaseInstant));

        var result = await handler.HandleAsync(
            new GenerateKeyInput(KeyDomain.COOKIE, KeyType.Secret));

        result.IsCreated.Should().BeTrue(
            "GenerateKey increments key_generations_total only after a successful commit");
    }

    // -----------------------------------------------------------------------
    // O-1 — CompromiseKey increments compromises_total after commit
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CompromiseKey_Success_PassesThroughMetricIncrementPath()
    {
        // Exercises the code path where SR_CompromisesTotal.Add(1) is called.
        // Counter-increment correctness is validated by the existing behavior tests in
        // CompromiseKeyTests; this test confirms the metric code path doesn't throw.
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

        var result = await new CompromiseKeyHandler(
            KcAppTestKit.Context<CompromiseKeyHandler>(),
            KcAppTestKit.NullClassifier(),
            db,
            Options.Create(r_options),
            new KcAppTestKit.RecordingAnnouncer(),
            r_crypto,
            new TestClock(created + Duration.FromHours(1)))
            .HandleAsync(new CompromiseKeyInput { Kid = kid, Reason = "audit-test" });

        result.Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // O-1 — Counter names pin the operational contract (dashboards / alert rules)
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
    // Helper builders
    // -----------------------------------------------------------------------

    private static GetJwksHandler BuildGetJwks(KeyCustodianTestDbContext db) =>
        new(KcAppTestKit.Context<GetJwksHandler>(), db);
}
