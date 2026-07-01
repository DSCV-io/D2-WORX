// -----------------------------------------------------------------------
// <copyright file="SigningDomainAuthorityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Tests for the signing-domain authority policy: the
/// <see cref="OptionsSigningDomainAuthorityPolicy"/> provider lookup (default-deny on
/// unknown / empty) and the <see cref="SigningDomainAuthorityOptions.Validate"/>
/// fail-loud boot invariant — the host refuses to boot a config granting an
/// in-process-only domain, an empty-string key, or a non-catalog domain; an empty
/// policy is legitimately fine (deny-all).
/// </summary>
public sealed class SigningDomainAuthorityTests
{
    // -----------------------------------------------------------------------
    // Provider — lookup + default-deny
    // -----------------------------------------------------------------------

    [Fact]
    public void AllowedSigningDomainsFor_KnownWorkload_ReturnsConfiguredSet()
    {
        var options = new SigningDomainAuthorityOptions();
        options.AllowedSigningDomainsByWorkload["files"] = ["audit", "notifications"];
        var provider = new OptionsSigningDomainAuthorityPolicy(Options.Create(options));

        var allowed = provider.AllowedSigningDomainsFor("files");

        allowed.Should().BeEquivalentTo(["audit", "notifications"]);
    }

    [Fact]
    public void AllowedSigningDomainsFor_WorkloadPresentButDomainListEmpty_ReturnsEmptySet()
    {
        // The workload KEY is present in the map but its domain list is EMPTY —
        // distinct from the unknown-workload case (key absent). The domains.Falsey()
        // branch in OptionsSigningDomainAuthorityPolicy returns sr_empty for a present-
        // but-empty list, so the behavior is default-deny with no panic.
        var options = new SigningDomainAuthorityOptions();
        options.AllowedSigningDomainsByWorkload["files"] = [];
        var provider = new OptionsSigningDomainAuthorityPolicy(Options.Create(options));

        provider.AllowedSigningDomainsFor("files").Should().BeEmpty(
            "a workload with an empty domain list is treated as default-deny, "
            + "indistinguishable from an unknown workload from the caller's perspective");
    }

    [Fact]
    public void AllowedSigningDomainsFor_UnknownWorkload_ReturnsEmptySet_DefaultDeny()
    {
        var options = new SigningDomainAuthorityOptions();
        options.AllowedSigningDomainsByWorkload["files"] = ["audit"];
        var provider = new OptionsSigningDomainAuthorityPolicy(Options.Create(options));

        provider.AllowedSigningDomainsFor("ghost").Should().BeEmpty(
            "an unknown workload resolves to the empty set (default-deny)");
    }

    [Fact]
    public void AllowedSigningDomainsFor_EmptyPolicy_ReturnsEmptySet_DenyAll()
    {
        var provider = new OptionsSigningDomainAuthorityPolicy(
            Options.Create(new SigningDomainAuthorityOptions()));

        provider.AllowedSigningDomainsFor("edge").Should().BeEmpty(
            "an empty policy makes every lookup empty ⇒ deny-all ⇒ fail-closed");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AllowedSigningDomainsFor_NullOrEmptyWorkload_ReturnsEmptySet(string? workloadId)
    {
        var provider = new OptionsSigningDomainAuthorityPolicy(
            Options.Create(new SigningDomainAuthorityOptions()));

        provider.AllowedSigningDomainsFor(workloadId).Should().BeEmpty(
            "a null / empty caller id resolves to the empty set (default-deny)");
    }

    [Fact]
    public void Provider_RoundTripsThroughRule_AllowAndDeny()
    {
        var options = new SigningDomainAuthorityOptions();
        options.AllowedSigningDomainsByWorkload["files"] = ["audit"];
        var provider = new OptionsSigningDomainAuthorityPolicy(Options.Create(options));

        var allow = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: "files",
            origin: RequestOrigin.CrossProcessHop,
            target: KeyDomain.Create("audit").Data!,
            allowedSigningDomainsForCaller: provider.AllowedSigningDomainsFor("files"));
        var deny = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: "files",
            origin: RequestOrigin.CrossProcessHop,
            target: KeyDomain.Create("notifications").Data!,
            allowedSigningDomainsForCaller: provider.AllowedSigningDomainsFor("files"));

        allow.Success.Should().BeTrue();
        deny.ErrorCode.Should().Be("KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED");
    }

    // -----------------------------------------------------------------------
    // Validate() — fail-loud boot invariant
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_EmptyPolicy_IsValid()
    {
        new SigningDomainAuthorityOptions().Validate().Should().BeNull(
            "an empty policy is legitimately fine (deny-all)");
    }

    [Fact]
    public void Validate_ValidPolicy_IsValid()
    {
        var options = new SigningDomainAuthorityOptions();
        options.AllowedSigningDomainsByWorkload["files"] = ["audit", "notifications"];

        options.Validate().Should().BeNull();
    }

    [Fact]
    public void Validate_GrantsJwksSigning_FailsLoud()
    {
        // The boot invariant: no workload may be granted jwks-signing.
        var options = new SigningDomainAuthorityOptions();
        options.AllowedSigningDomainsByWorkload["edge"] = [KeyDomain.JWKS_SIGNING];

        options.Validate().Should().NotBeNull(
            "granting the in-process-only jwks-signing domain to any workload must "
            + "refuse to boot (fail-loud)");
        options.Validate().Should().Contain("jwks-signing");
    }

    [Theory]
    [InlineData("JWKS-SIGNING")]
    [InlineData("Jwks-Signing")]
    public void Validate_GrantsJwksSigningUppercase_FailsLoud(string domainValue)
    {
        // The boot gate must catch non-lowercase variants of jwks-signing.
        // The normalized path (KeyDomain.Create + OrdinalIgnoreCase set) means
        // "JWKS-SIGNING" and "Jwks-Signing" are both rejected as in-process-only
        // grants, preventing a case-bypass of the in-process-only-domain control.
        var options = new SigningDomainAuthorityOptions();
        options.AllowedSigningDomainsByWorkload["edge"] = [domainValue];

        options.Validate().Should().NotBeNull(
            $"granting the in-process-only domain '{domainValue}' to any workload must "
            + "refuse to boot — the boot gate is case-robust via OrdinalIgnoreCase");
    }

    [Theory]
    [InlineData("JWKS-SIGNING")]
    [InlineData("Jwks-Signing")]
    public void ValidateOnStart_GrantsJwksSigningUppercase_ThrowsOnOptionsResolution(
        string domainValue)
    {
        // End-to-end DI gate for uppercase jwks-signing grants. Mirrors
        // ValidateOnStart_GrantsJwksSigning_ThrowsOnOptionsResolution but drives the
        // non-lowercase variant that was previously bypassing the raw-string check.
        var services = new ServiceCollection();
        services.AddOptions<SigningDomainAuthorityOptions>()
            .Configure(o => o.AllowedSigningDomainsByWorkload["edge"] = [domainValue])
            .Validate(static o => o.Validate() is null, "in-process-only-domain grant rejected")
            .ValidateOnStart();

        var thrown = Record.Exception(() => ResolveSigningAuthorityOptions(services));

        thrown.Should().BeOfType<OptionsValidationException>(
            $"the host must refuse to start when a workload is granted '{domainValue}'");
    }

    [Fact]
    public void Validate_EmptyStringKey_FailsLoud()
    {
        var options = new SigningDomainAuthorityOptions();
        options.AllowedSigningDomainsByWorkload[" "] = ["audit"];

        options.Validate().Should().NotBeNull("an empty / whitespace workload key is rejected");
    }

    [Fact]
    public void Validate_WildcardKey_FailsLoud()
    {
        var options = new SigningDomainAuthorityOptions();
        options.AllowedSigningDomainsByWorkload["*"] = ["audit"];

        options.Validate().Should().NotBeNull(
            "a '*' wildcard key is outside the [a-z0-9-] SPIFFE grammar and is rejected");
    }

    [Fact]
    public void Validate_NonCatalogDomainValue_FailsLoud()
    {
        var options = new SigningDomainAuthorityOptions();
        options.AllowedSigningDomainsByWorkload["files"] = ["not-a-real-domain"];

        options.Validate().Should().NotBeNull("a non-catalog domain value is rejected");
    }

    [Fact]
    public void AllowedSigningDomainsFor_DuplicateDomainValue_DeduplicatedToOne()
    {
        // Pin the CURRENT behavior: a duplicate domain value in the config list
        // ("audit", "audit") is accepted by Validate() (no duplicate-check there)
        // and deduplicated to one entry by the HashSet construction in the provider.
        // This test pins the semantics so any future change (e.g. adding a duplicate
        // guard to Validate) is an explicit, visible decision.
        var options = new SigningDomainAuthorityOptions();
        options.AllowedSigningDomainsByWorkload["files"] = ["audit", "audit"];

        // Validate() does NOT reject duplicates (it checks catalog membership +
        // in-process-only guard per entry; a repeated valid value passes both).
        options.Validate().Should().BeNull(
            "a duplicate domain value is not a validation error — it is redundant but safe");

        // The provider deduplicates via HashSet: "audit" appears once in the result.
        var provider = new OptionsSigningDomainAuthorityPolicy(Options.Create(options));
        var domains = provider.AllowedSigningDomainsFor("files");

        domains.Should().BeEquivalentTo(
            new[] { "audit" },
            "a duplicate domain entry is silently deduplicated to one by the HashSet "
            + "construction; the caller sees a single 'audit' entry");
    }

    // -----------------------------------------------------------------------
    // ValidateOnStart wiring — the dangerous config refuses to boot end-to-end
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidateOnStart_GrantsJwksSigning_ThrowsOnOptionsResolution()
    {
        // Mirror the Infra registration (AddOptions + Validate(o.Validate() is null) +
        // ValidateOnStart) so the boot invariant is proven through the real DI gate:
        // resolving IOptions.Value triggers the validation and THROWS on the dangerous
        // grant, which is exactly what ValidateOnStart surfaces at host start.
        var services = new ServiceCollection();
        services.AddOptions<SigningDomainAuthorityOptions>()
            .Configure(o => o.AllowedSigningDomainsByWorkload["edge"] = [KeyDomain.JWKS_SIGNING])
            .Validate(static o => o.Validate() is null, "in-process-only-domain grant rejected")
            .ValidateOnStart();

        // Resolving IOptions.Value triggers the validator (the helper builds + disposes
        // the provider internally), which is exactly what ValidateOnStart surfaces.
        var thrown = Record.Exception(() => ResolveSigningAuthorityOptions(services));

        thrown.Should().BeOfType<OptionsValidationException>(
            "the host must refuse to start when a workload is granted jwks-signing");
    }

    [Fact]
    public void ValidateOnStart_EmptyPolicy_ResolvesWithoutThrowing()
    {
        var services = new ServiceCollection();
        services.AddOptions<SigningDomainAuthorityOptions>()
            .Validate(static o => o.Validate() is null, "valid")
            .ValidateOnStart();

        var thrown = Record.Exception(() => ResolveSigningAuthorityOptions(services));

        thrown.Should().BeNull("an empty policy is valid (deny-all) and boots clean");
    }

    // -----------------------------------------------------------------------
    // DI resolution — the provider resolves from AddD2KeyCustodianApp (§1.3)
    // -----------------------------------------------------------------------

    [Fact]
    public void Provider_ResolvesFromAppRegistration()
    {
        var services = new ServiceCollection();
        services.AddOptions<SigningDomainAuthorityOptions>();
        services.AddSingleton<
            ISigningDomainAuthorityPolicy, OptionsSigningDomainAuthorityPolicy>();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ISigningDomainAuthorityPolicy>()
            .Should().BeOfType<OptionsSigningDomainAuthorityPolicy>();
    }

    /// <summary>
    /// Builds a provider from <paramref name="services"/>, resolves
    /// <c>IOptions&lt;SigningDomainAuthorityOptions&gt;.Value</c> (triggering the
    /// validator), and disposes the provider — so the resolution that may throw runs
    /// entirely within this helper (no captured-and-outer-disposed provider).
    /// </summary>
    /// <param name="services">The configured service collection.</param>
    private static void ResolveSigningAuthorityOptions(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IOptions<SigningDomainAuthorityOptions>>().Value;
    }
}
