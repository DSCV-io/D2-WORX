// -----------------------------------------------------------------------
// <copyright file="KeyCustodianValidateOnStartTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Infra;

using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.Infra.Configuration;
using D2.Shared.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Proves the <c>AddD2KeyCustodian</c> options pipeline validates — resolving
/// <see cref="IOptions{T}"/>.<c>Value</c> over invalid <c>KEYCUSTODIAN_APP__*</c> /
/// <c>KEYCUSTODIAN_INFRA__*</c> configuration throws
/// <see cref="OptionsValidationException"/> (the same data-annotation gate
/// <c>ValidateOnStart</c> enforces at host build) rather than surfacing on first
/// use (§23.7). Covers BOTH options POCOs.
/// </summary>
public sealed class KeyCustodianValidateOnStartTests : IDisposable
{
    private readonly string r_rootKeyDir;

    public KeyCustodianValidateOnStartTests()
    {
        r_rootKeyDir = KcInfraTestKit.CreateRootKeyDir();
    }

    public void Dispose()
    {
        if (Directory.Exists(r_rootKeyDir))
            Directory.Delete(r_rootKeyDir, recursive: true);
    }

    [Fact]
    public void ValidOptions_BothResolve()
    {
        using var sp = BuildProvider(KcInfraTestKit.BuildConfiguration(r_rootKeyDir));

        sp.GetRequiredService<IOptions<KeyCustodianOptions>>().Value.Should().NotBeNull();
        sp.GetRequiredService<IOptions<KeyCustodianInfraOptions>>().Value.Should().NotBeNull();
    }

    [Fact]
    public void InvalidInfraInterval_FailsValidationOnResolve()
    {
        using var sp = BuildProvider(ConfigurationWithOverride(
            "KEYCUSTODIAN_INFRA:RotationCheckInterval", "00:00:00"));

        sp.Invoking(s => s.GetRequiredService<IOptions<KeyCustodianInfraOptions>>().Value)
            .Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void InvalidInfraRootKeyPath_FailsValidationOnResolve()
    {
        using var sp = BuildProvider(
            ConfigurationWithOverride("KEYCUSTODIAN_INFRA:RootKeyPath", string.Empty));

        sp.Invoking(s => s.GetRequiredService<IOptions<KeyCustodianInfraOptions>>().Value)
            .Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void InvalidInfraCommandTimeout_FailsValidationOnResolve()
    {
        using var sp = BuildProvider(
            ConfigurationWithOverride("KEYCUSTODIAN_INFRA:DbCommandTimeoutSeconds", "0"));

        sp.Invoking(s => s.GetRequiredService<IOptions<KeyCustodianInfraOptions>>().Value)
            .Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void InvalidAppRsaKeySize_FailsValidationOnResolve()
    {
        // Top-level [Range] on KeyCustodianOptions.RsaKeySizeBits (minimum 2048).
        // ValidateDataAnnotations validates top-level data-annotated members; a
        // below-minimum value fails the start gate.
        using var sp = BuildProvider(
            ConfigurationWithOverride("KEYCUSTODIAN_APP:RsaKeySizeBits", "512"));

        sp.Invoking(s => s.GetRequiredService<IOptions<KeyCustodianOptions>>().Value)
            .Should().Throw<OptionsValidationException>();
    }

    // -----------------------------------------------------------------------
    // Nested policy validation — IValidatableObject recursion regression
    // (§23.7 start-validation gap: ValidateDataAnnotations() does NOT recurse
    // into nested objects without IValidatableObject; these tests pin the fix)
    // -----------------------------------------------------------------------

    [Fact]
    public void InvalidDefaultPolicyCadence_ZeroDuration_FailsValidationOnResolve()
    {
        // Without IValidatableObject on KeyCustodianOptions, a zero Cadence in
        // the nested Default policy passes ValidateDataAnnotations silently and
        // surfaces only at first ForDomain() call as KEYCUSTODIAN_INVALID_ROTATION_POLICY.
        // With the fix, it fails here — at the start gate.
        using var sp = BuildProvider(
            ConfigurationWithOverride("KEYCUSTODIAN_APP:Default:Cadence", "00:00:00"));

        sp.Invoking(s => s.GetRequiredService<IOptions<KeyCustodianOptions>>().Value)
            .Should().Throw<OptionsValidationException>(
                because: "a zero Cadence in the nested Default policy must fail at startup");
    }

    [Theory]
    [InlineData("00:00:00")] // zero
    [InlineData("-00:00:01")] // negative (-1 second)
    public void InvalidDefaultPolicyCadence_NonPositive_FailsValidationOnResolve(
        string cadenceValue)
    {
        using var sp = BuildProvider(
            ConfigurationWithOverride("KEYCUSTODIAN_APP:Default:Cadence", cadenceValue));

        sp.Invoking(s => s.GetRequiredService<IOptions<KeyCustodianOptions>>().Value)
            .Should().Throw<OptionsValidationException>();
    }

    // long test name — cannot shorten without losing meaning
    [Fact]
    public void InvalidDefaultPolicyCrossField_CadenceShorterThanGracePlusSoak_FailsValidationOnResolve()
    {
        // Cadence (2h) < Grace (2h) + SmokeSoak (2h) = 4h: violates the cross-field
        // invariant mirrored from RotationPolicy.Create. Must fail at startup, not at
        // first ForDomain() call.
        var settings = new Dictionary<string, string?>
        {
            ["KEYCUSTODIAN_APP:Default:Cadence"] = "02:00:00",
            ["KEYCUSTODIAN_APP:Default:Grace"] = "02:00:00",
            ["KEYCUSTODIAN_APP:Default:SmokeSoak"] = "02:00:00",
            ["KEYCUSTODIAN_INFRA:RootKeyPath"] = r_rootKeyDir,
            ["KEYCUSTODIAN_INFRA:RotationCheckInterval"] = "00:05:00",
            ["KEYCUSTODIAN_INFRA:DbCommandTimeoutSeconds"] = "30",
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var sp = BuildProvider(config);

        sp.Invoking(s => s.GetRequiredService<IOptions<KeyCustodianOptions>>().Value)
            .Should().Throw<OptionsValidationException>(
                because: "Cadence shorter than Grace + SmokeSoak must fail the startup gate");
    }

    [Fact]
    public void InvalidPerDomainPolicy_ZeroCadence_FailsValidationOnResolve()
    {
        // A Policies["jwks-signing"] entry with a zero-duration Cadence must fail at
        // startup, not silently pass through ValidateDataAnnotations and surface only
        // when ForDomain(JwksSigning) is first called.
        var settings = new Dictionary<string, string?>
        {
            ["KEYCUSTODIAN_APP:Default:Cadence"] = "30.00:00:00",
            ["KEYCUSTODIAN_APP:Default:Grace"] = "07.00:00:00",
            ["KEYCUSTODIAN_APP:Default:SmokeSoak"] = "01:00:00",
            ["KEYCUSTODIAN_APP:Policies:jwks-signing:Cadence"] = "00:00:00",
            ["KEYCUSTODIAN_APP:Policies:jwks-signing:Grace"] = "02:00:00",
            ["KEYCUSTODIAN_APP:Policies:jwks-signing:SmokeSoak"] = "01:00:00",
            ["KEYCUSTODIAN_INFRA:RootKeyPath"] = r_rootKeyDir,
            ["KEYCUSTODIAN_INFRA:RotationCheckInterval"] = "00:05:00",
            ["KEYCUSTODIAN_INFRA:DbCommandTimeoutSeconds"] = "30",
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var sp = BuildProvider(config);

        sp.Invoking(s => s.GetRequiredService<IOptions<KeyCustodianOptions>>().Value)
            .Should().Throw<OptionsValidationException>(
                because: "a zero Cadence in a per-domain Policies entry must fail at startup");
    }

    [Fact]
    public void ValidNestedPolicies_DefaultAndOverride_BothResolve()
    {
        // Confirm valid nested policies (including a Policies override) pass the
        // start gate — regression guard so the fix does not over-reject valid config.
        var settings = new Dictionary<string, string?>
        {
            ["KEYCUSTODIAN_APP:Default:Cadence"] = "30.00:00:00",
            ["KEYCUSTODIAN_APP:Default:Grace"] = "07.00:00:00",
            ["KEYCUSTODIAN_APP:Default:SmokeSoak"] = "01:00:00",
            ["KEYCUSTODIAN_APP:Policies:jwks-signing:Cadence"] = "07.00:00:00",
            ["KEYCUSTODIAN_APP:Policies:jwks-signing:Grace"] = "04.00:00:00",
            ["KEYCUSTODIAN_APP:Policies:jwks-signing:SmokeSoak"] = "02:00:00",
            ["KEYCUSTODIAN_INFRA:RootKeyPath"] = r_rootKeyDir,
            ["KEYCUSTODIAN_INFRA:RotationCheckInterval"] = "00:05:00",
            ["KEYCUSTODIAN_INFRA:DbCommandTimeoutSeconds"] = "30",
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var sp = BuildProvider(config);

        sp.Invoking(s => s.GetRequiredService<IOptions<KeyCustodianOptions>>().Value)
            .Should().NotThrow("valid nested rotation policies must pass the startup gate");
    }

    private static ServiceProvider BuildProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IMessageBus, NoopMessageBus>();
        services.AddD2KeyCustodian(configuration, KcInfraTestKit.FAKE_CONNECTION_STRING);
        return services.BuildServiceProvider();
    }

    private IConfiguration ConfigurationWithOverride(string key, string value)
    {
        var settings = new Dictionary<string, string?>
        {
            ["KEYCUSTODIAN_APP:Default:Cadence"] = "30.00:00:00",
            ["KEYCUSTODIAN_APP:Default:Grace"] = "7.00:00:00",
            ["KEYCUSTODIAN_APP:Default:SmokeSoak"] = "01:00:00",
            ["KEYCUSTODIAN_INFRA:RootKeyPath"] = r_rootKeyDir,
            ["KEYCUSTODIAN_INFRA:RotationCheckInterval"] = "00:05:00",
            ["KEYCUSTODIAN_INFRA:DbCommandTimeoutSeconds"] = "30",
            [key] = value,
        };
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private sealed class NoopMessageBus : IMessageBus
    {
        public ValueTask<D2Result> PublishAsync<TMessage>(
            TMessage message, PublisherOptions? options = null, CancellationToken ct = default)
            where TMessage : class => ValueTask.FromResult(D2Result.Ok());

        public Task WaitForReadyAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
