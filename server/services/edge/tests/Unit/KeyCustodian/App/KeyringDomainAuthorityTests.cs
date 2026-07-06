// -----------------------------------------------------------------------
// <copyright file="KeyringDomainAuthorityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Tests for the keyring-domain authority policy: the
/// <see cref="OptionsKeyringDomainAuthorityPolicy"/> provider lookup (default-deny on
/// unknown / empty) and the <see cref="KeyringDomainAuthorityOptions.Validate"/>
/// fail-loud boot invariant — the host refuses to boot a config granting a non-payload
/// domain, an empty-string key, or a non-catalog domain; an empty policy is legitimately
/// fine (deny-all). Unlike the signing policy, the workload key may be EITHER a
/// cross-process SPIFFE workload id OR an in-process module id (both share the bare
/// [a-z0-9-] grammar).
/// </summary>
public sealed class KeyringDomainAuthorityTests
{
    private const string _AUDIT = "audit";
    private const string _NOTIFICATIONS = "notifications";

    // -----------------------------------------------------------------------
    // Provider — lookup + default-deny
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        var act = () => new OptionsKeyringDomainAuthorityPolicy(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AllowedKeyringDomainsFor_KnownWorkload_ReturnsConfiguredSet()
    {
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["audit"] = [_AUDIT, _NOTIFICATIONS];
        var provider = new OptionsKeyringDomainAuthorityPolicy(Options.Create(options));

        provider.AllowedKeyringDomainsFor("audit")
            .Should().BeEquivalentTo([_AUDIT, _NOTIFICATIONS]);
    }

    [Fact]
    public void AllowedKeyringDomainsFor_WorkloadPresentButDomainListEmpty_ReturnsEmptySet()
    {
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["audit"] = [];
        var provider = new OptionsKeyringDomainAuthorityPolicy(Options.Create(options));

        provider.AllowedKeyringDomainsFor("audit").Should().BeEmpty(
            "a workload with an empty domain list is default-deny, indistinguishable from unknown");
    }

    [Fact]
    public void AllowedKeyringDomainsFor_UnknownWorkload_ReturnsEmptySet_DefaultDeny()
    {
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["audit"] = [_AUDIT];
        var provider = new OptionsKeyringDomainAuthorityPolicy(Options.Create(options));

        provider.AllowedKeyringDomainsFor("ghost").Should().BeEmpty(
            "an unknown workload resolves to the empty set (default-deny)");
    }

    [Fact]
    public void AllowedKeyringDomainsFor_EmptyPolicy_ReturnsEmptySet_DenyAll()
    {
        var provider = new OptionsKeyringDomainAuthorityPolicy(
            Options.Create(new KeyringDomainAuthorityOptions()));

        provider.AllowedKeyringDomainsFor("edge").Should().BeEmpty(
            "an empty policy makes every lookup empty ⇒ deny-all ⇒ fail-closed");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AllowedKeyringDomainsFor_NullOrEmptyWorkload_ReturnsEmptySet(string? workloadId)
    {
        var provider = new OptionsKeyringDomainAuthorityPolicy(
            Options.Create(new KeyringDomainAuthorityOptions()));

        provider.AllowedKeyringDomainsFor(workloadId).Should().BeEmpty(
            "a null / empty caller id resolves to the empty set (default-deny)");
    }

    [Theory]
    [InlineData("Audit")]
    [InlineData(" audit ")]
    [InlineData("AUDIT")]
    public void AllowedKeyringDomainsFor_NonCanonicalGrant_NormalizedToCatalogValue(string grant)
    {
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["audit"] = [grant];
        var provider = new OptionsKeyringDomainAuthorityPolicy(Options.Create(options));

        provider.AllowedKeyringDomainsFor("audit").Should().Contain(
            _AUDIT, $"the grant '{grant}' normalizes to the canonical catalog value");
    }

    [Fact]
    public void AllowedKeyringDomainsFor_NonCatalogGrant_SkippedDefensively()
    {
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["audit"] = [_AUDIT, "not-a-real-domain"];
        var provider = new OptionsKeyringDomainAuthorityPolicy(Options.Create(options));

        provider.AllowedKeyringDomainsFor("audit").Should().BeEquivalentTo([_AUDIT]);
    }

    [Fact]
    public void Provider_RoundTripsThroughRule_AllowAndDeny()
    {
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["audit"] = [_AUDIT];
        var provider = new OptionsKeyringDomainAuthorityPolicy(Options.Create(options));

        var allow = WorkloadCapabilityAuthority.AuthorizeKeyringFetch(
            immediateCaller: "audit",
            origin: RequestOrigin.CrossProcessHop,
            target: KeyDomain.Create(_AUDIT).Data!,
            allowedKeyringDomainsForCaller: provider.AllowedKeyringDomainsFor("audit"));
        var deny = WorkloadCapabilityAuthority.AuthorizeKeyringFetch(
            immediateCaller: "audit",
            origin: RequestOrigin.CrossProcessHop,
            target: KeyDomain.Create(_NOTIFICATIONS).Data!,
            allowedKeyringDomainsForCaller: provider.AllowedKeyringDomainsFor("audit"));

        allow.Success.Should().BeTrue();
        deny.ErrorCode.Should().Be("KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED");
    }

    // -----------------------------------------------------------------------
    // Validate() — fail-loud boot invariant
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_EmptyPolicy_IsValid()
    {
        new KeyringDomainAuthorityOptions().Validate().Should().BeNull(
            "an empty policy is legitimately fine (deny-all)");
    }

    [Fact]
    public void Validate_ValidPayloadPolicy_IsValid()
    {
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["audit"] = [_AUDIT, _NOTIFICATIONS, "courier"];

        options.Validate().Should().BeNull("payload domains are keyring-grantable");
    }

    [Fact]
    public void Validate_CrossProcessSpiffeWorkloadId_Accepted()
    {
        // A cross-process caller keyed by its SPIFFE workload service id.
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["audit-service"] = [_AUDIT];

        options.Validate().Should().BeNull(
            "a bare SPIFFE workload id is a valid keyring workload key");
    }

    [Fact]
    public void Validate_InProcessModuleId_Accepted()
    {
        // The in-host module consumer keyed by its module id (e.g. "edge") — the keyring
        // map admits BOTH namespaces, unlike the signing map.
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["edge"] = [_AUDIT];

        options.Validate().Should().BeNull(
            "an in-process module id shares the bare [a-z0-9-] grammar and is accepted");
    }

    [Theory]
    [InlineData(KeyDomain.JWKS_SIGNING)]
    [InlineData(KeyDomain.COOKIE)]
    [InlineData(KeyDomain.CLIENT_SECRET)]
    [InlineData(KeyDomain.MTLS_CA_ROOT)]
    [InlineData(KeyDomain.MTLS_CA_INTERMEDIATE)]

    // A seal-family domain resolves through Create (the ForSeal delegation) with the
    // EcdhSealing binding — the validator's payload-only check must refuse a seal grant
    // exactly like every other non-payload domain (a seal PRIVATE key must never be
    // releasable through the keyring surface).
    [InlineData(KeyDomain.SEAL_PREFIX + "audit")]
    public void Validate_GrantsNonPayloadDomain_FailsLoud(string nonPayload)
    {
        // The boot invariant: no workload may be granted a non-payload domain — a keyring
        // is a full encrypt+decrypt capability, never releasable for a crown-jewel domain.
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["edge"] = [nonPayload];

        options.Validate().Should().NotBeNull(
            $"granting the non-payload domain '{nonPayload}' to any workload must refuse to boot");
        options.Validate().Should().Contain("non-payload");
    }

    [Theory]
    [InlineData("COOKIE")]
    [InlineData("Jwks-Signing")]
    public void Validate_GrantsNonPayloadDomainUppercase_FailsLoud(string nonPayload)
    {
        // Case-robust: KeyDomain.Create normalizes, and the bound KeyType check catches
        // the non-payload grant regardless of the configured casing.
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["edge"] = [nonPayload];

        options.Validate().Should().NotBeNull(
            $"granting the non-payload domain '{nonPayload}' (any case) must refuse to boot");
    }

    [Fact]
    public void Validate_EmptyStringKey_FailsLoud()
    {
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload[" "] = [_AUDIT];

        options.Validate().Should().NotBeNull("an empty / whitespace workload key is rejected");
    }

    [Fact]
    public void Validate_WildcardKey_FailsLoud()
    {
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["*"] = [_AUDIT];

        options.Validate().Should().NotBeNull(
            "a '*' wildcard key is outside the [a-z0-9-] grammar and is rejected");
    }

    [Fact]
    public void Validate_NonCatalogDomainValue_FailsLoud()
    {
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["audit"] = ["not-a-real-domain"];

        options.Validate().Should().NotBeNull("a non-catalog domain value is rejected");
    }

    [Fact]
    public void Validate_CaseVariantPayloadGrant_Accepted()
    {
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["audit"] = ["Audit"];

        options.Validate().Should().BeNull(
            "a case-variant payload grant normalizes to a valid catalog domain");
    }

    // -----------------------------------------------------------------------
    // ValidateOnStart wiring — the dangerous config refuses to boot end-to-end
    // -----------------------------------------------------------------------

    [Fact]
    public void ValidateOnStart_GrantsNonPayloadDomain_ThrowsOnOptionsResolution()
    {
        var services = new ServiceCollection();
        services.AddOptions<KeyringDomainAuthorityOptions>()
            .Configure(o => o.AllowedKeyringDomainsByWorkload["edge"] = [KeyDomain.JWKS_SIGNING])
            .Validate(static o => o.Validate() is null, "non-payload-domain grant rejected")
            .ValidateOnStart();

        var thrown = Record.Exception(() => ResolveKeyringAuthorityOptions(services));

        thrown.Should().BeOfType<OptionsValidationException>(
            "the host must refuse to start when a workload is granted a non-payload domain");
    }

    [Fact]
    public void ValidateOnStart_EmptyPolicy_ResolvesWithoutThrowing()
    {
        var services = new ServiceCollection();
        services.AddOptions<KeyringDomainAuthorityOptions>()
            .Validate(static o => o.Validate() is null, "valid")
            .ValidateOnStart();

        var thrown = Record.Exception(() => ResolveKeyringAuthorityOptions(services));

        thrown.Should().BeNull("an empty policy is valid (deny-all) and boots clean");
    }

    // -----------------------------------------------------------------------
    // DI resolution — the provider resolves from AddD2KeyCustodianApp (§1.3)
    // -----------------------------------------------------------------------

    [Fact]
    public void Provider_ResolvesFromAppRegistration()
    {
        // The App layer registers the policy; the options binding is Infra-owned, so the
        // test supplies the options accessor (mirrors the signing-policy resolution test).
        var services = new ServiceCollection();
        services.AddOptions<KeyringDomainAuthorityOptions>();
        services.AddSingleton<
            IKeyringDomainAuthorityPolicy, OptionsKeyringDomainAuthorityPolicy>();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IKeyringDomainAuthorityPolicy>()
            .Should().BeOfType<OptionsKeyringDomainAuthorityPolicy>();
    }

    private static void ResolveKeyringAuthorityOptions(IServiceCollection services)
    {
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<IOptions<KeyringDomainAuthorityOptions>>().Value;
    }
}
