// -----------------------------------------------------------------------
// <copyright file="WorkloadCapabilityAuthorityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

/// <summary>
/// The capability-general authority matrix — the pure
/// <see cref="WorkloadCapabilityAuthority"/> decision across every origin ×
/// capability × target combination. The controls it pins: the
/// fail-closed <c>Unestablished</c>-origin deny, the structural minter-only
/// <c>jwks-signing</c> deny on EVERY established origin (the confused-deputy made
/// impossible — possession of the minter capability is the only path to the root),
/// the cross-process plane + policy denies, the in-process minter's
/// <see cref="WorkloadCapabilityAuthority.AuthorizeMinterSigning"/> allow, the broad
/// seal-encrypt arm, the structural self-only seal-decrypt arm, and fail-closed deny
/// when no authenticated identity is present.
/// </summary>
public sealed class WorkloadCapabilityAuthorityTests
{
    private const string _EDGE = "edge";
    private const string _FILES = "files";

    private static readonly IReadOnlySet<string> sr_empty =
        new HashSet<string>(StringComparer.Ordinal);

    // -----------------------------------------------------------------------
    // AuthorizeSigning — allow path (cross-process, in-policy)
    // -----------------------------------------------------------------------

    [Fact]
    public void AuthorizeSigning_CrossProcessHop_DomainInAllowedSet_Allowed()
    {
        var allowed = SetOf("audit");

        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: _FILES,
            origin: RequestOrigin.CrossProcessHop,
            target: KeyDomain.Create("audit").Data!,
            allowedSigningDomainsForCaller: allowed);

        result.Success.Should().BeTrue(
            "a cross-process workload may sign with a non-root domain in its allowed set");
    }

    // -----------------------------------------------------------------------
    // AuthorizeSigning — fail-closed: an unestablished origin never authorizes
    // (the scoped IRequestContext.Origin default). Checked BEFORE the minter arm,
    // so even a jwks-signing target surfaces RequestOriginUnestablished.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("audit")]
    [InlineData("cookie")]
    [InlineData(KeyDomain.JWKS_SIGNING)]
    public void AuthorizeSigning_UnestablishedOrigin_Denied_RequestOriginUnestablished(
        string domainValue)
    {
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: _EDGE,
            origin: RequestOrigin.Unestablished,
            target: KeyDomain.Create(domainValue).Data!,
            allowedSigningDomainsForCaller: SetOf(domainValue));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED",
            "an origin no boundary established fails closed (the Unestablished default deny "
            + "runs before the minter-only arm)");
    }

    // -----------------------------------------------------------------------
    // AuthorizeSigning — minter-only structural deny: the cluster-signing root
    // (jwks-signing) is unreachable on the general surface for EVERY established
    // origin. Reachable only through the dedicated minter capability.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    [InlineData(RequestOrigin.System)]
    public void AuthorizeSigning_JwksSigning_EveryEstablishedOrigin_Denied_MinterCapabilityRequired(
        RequestOrigin origin)
    {
        // Even a (hypothetically misconfigured) policy that grants jwks-signing cannot
        // make the general surface allow it — the minter-only deny is structural and
        // independent of policy.
        var misconfiguredPolicy = SetOf(KeyDomain.JWKS_SIGNING, "audit");

        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: _EDGE,
            origin: origin,
            target: KeyDomain.JwksSigning,
            allowedSigningDomainsForCaller: misconfiguredPolicy);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED",
            "the cluster-signing root is reachable only via the dedicated minter capability, "
            + "for every established origin");
    }

    [Fact]
    public void AuthorizeSigning_InProcessModule_JwksSigning_ConfusedDeputyImpossible()
    {
        // The confused-deputy: an external request that becomes in-process downstream
        // carries Origin = InProcessModule. Even on that plane — and even if the policy
        // granted jwks-signing — the general surface CANNOT reach the cluster-signing
        // root; there is no caller id to spoof. Only a holder of the minter capability
        // (wired solely in the auth-module composition) can sign it.
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: null,
            origin: RequestOrigin.InProcessModule,
            target: KeyDomain.JwksSigning,
            allowedSigningDomainsForCaller: SetOf(KeyDomain.JWKS_SIGNING));

        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED",
            "a request that became in-process downstream still cannot reach the root on "
            + "the general surface — possession of the minter capability is the only path");
    }

    [Fact]
    public void AuthorizeSigning_FromTrustedUppercase_JwksSigning_Denied_MinterCapabilityRequired()
    {
        // The EF read-path (FromTrusted) preserves verbatim casing — "JWKS-SIGNING"
        // from a corrupt/legacy DB row. The OrdinalIgnoreCase MinterOnlySigningDomains
        // set catches it, so the structural minter-only deny still fires.
        var upperTarget = KeyDomain.FromTrusted("JWKS-SIGNING");

        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: _EDGE,
            origin: RequestOrigin.CrossProcessHop,
            target: upperTarget,
            allowedSigningDomainsForCaller: sr_empty);

        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED",
            "OrdinalIgnoreCase set catches 'JWKS-SIGNING' from the FromTrusted EF path");
    }

    // -----------------------------------------------------------------------
    // AuthorizeSigning — plane deny: every non-root domain signs cross-process
    // only. A non-CrossProcessHop established origin (EdgeInbound / InProcessModule /
    // System) is denied. System workers therefore have ZERO signing authority.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.InProcessModule)]
    [InlineData(RequestOrigin.System)]
    public void AuthorizeSigning_NonRootDomain_NonCrossProcessOrigin_Denied_SigningDomainNotAuthorized(
        RequestOrigin origin)
    {
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: _FILES,
            origin: origin,
            target: KeyDomain.Create("audit").Data!,
            allowedSigningDomainsForCaller: SetOf("audit"));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED",
            "a non-root domain signs cross-process only — every other established origin denies");
    }

    [Fact]
    public void AuthorizeSigning_System_NonRootDomain_Denied_ZeroSigningAuthority()
    {
        // System workers (background services) get ZERO signing authority: the System
        // origin falls through the cross-process-only plane check.
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: "edge",
            origin: RequestOrigin.System,
            target: KeyDomain.Create("audit").Data!,
            allowedSigningDomainsForCaller: SetOf("audit"));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED",
            "a System worker never signs — it has zero signing authority");
    }

    // -----------------------------------------------------------------------
    // AuthorizeSigning — fail-closed: cross-process with no peer identity
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AuthorizeSigning_CrossProcessHop_NoCallerIdentity_DeniedForbidden(string? callerId)
    {
        // A cross-process hop with no authenticated peer identity is fail-closed.
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: callerId,
            origin: RequestOrigin.CrossProcessHop,
            target: KeyDomain.Create("audit").Data!,
            allowedSigningDomainsForCaller: SetOf("audit"));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -----------------------------------------------------------------------
    // AuthorizeSigning — policy-scope deny (cross-process, out of policy)
    // -----------------------------------------------------------------------

    [Fact]
    public void AuthorizeSigning_CrossProcessHop_DomainNotInAllowedSet_Denied_SigningDomainNotAuthorized()
    {
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: _FILES,
            origin: RequestOrigin.CrossProcessHop,
            target: KeyDomain.Create("audit").Data!,
            allowedSigningDomainsForCaller: SetOf("notifications"));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED",
            "a domain not in the caller's allowed set is a policy-scope denial");
    }

    [Fact]
    public void AuthorizeSigning_CrossProcessHop_UnknownWorkload_EmptySet_Denied()
    {
        // An unknown workload resolves (via the provider) to the empty set ⇒ deny-all.
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: "ghost",
            origin: RequestOrigin.CrossProcessHop,
            target: KeyDomain.Create("audit").Data!,
            allowedSigningDomainsForCaller: sr_empty);

        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED",
            "an unknown workload's empty allowed set denies every domain");
    }

    // -----------------------------------------------------------------------
    // AuthorizeMinterSigning — possession + the in-process-module plane = authority
    // -----------------------------------------------------------------------

    [Fact]
    public void AuthorizeMinterSigning_InProcessModule_Allowed()
    {
        var result = WorkloadCapabilityAuthority.AuthorizeMinterSigning(
            RequestOrigin.InProcessModule);

        result.Success.Should().BeTrue(
            "the in-process minter capability may sign the cluster-signing root");
    }

    [Fact]
    public void AuthorizeMinterSigning_Unestablished_Denied_RequestOriginUnestablished()
    {
        var result = WorkloadCapabilityAuthority.AuthorizeMinterSigning(
            RequestOrigin.Unestablished);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED",
            "an unestablished origin never authorizes the minter (fail-closed)");
    }

    [Theory]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.System)]
    public void AuthorizeMinterSigning_NonInProcessOrigin_DeniedForbidden(RequestOrigin origin)
    {
        var result = WorkloadCapabilityAuthority.AuthorizeMinterSigning(origin);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "only the in-process-module plane may reach the minter — every other origin denies");
    }

    // -----------------------------------------------------------------------
    // AuthorizeSealEncrypt — broad (any present identity) / fail-closed when absent
    // -----------------------------------------------------------------------

    [Fact]
    public void AuthorizeSealEncrypt_PresentIdentity_Allowed_Broad()
    {
        WorkloadCapabilityAuthority.AuthorizeSealEncrypt(_FILES).Success.Should().BeTrue(
            "seal-encrypt is broad — any scoped producer may fetch any public seal key");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AuthorizeSealEncrypt_NoIdentity_DeniedForbidden(string? callerId)
    {
        var result = WorkloadCapabilityAuthority.AuthorizeSealEncrypt(callerId);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -----------------------------------------------------------------------
    // AuthorizeSealDecrypt — structural self-only (present identity) / fail-closed
    // -----------------------------------------------------------------------

    [Fact]
    public void AuthorizeSealDecrypt_PresentIdentity_Allowed_SelfOnlyByOpShape()
    {
        WorkloadCapabilityAuthority.AuthorizeSealDecrypt(_EDGE).Success.Should().BeTrue(
            "a present identity may fetch ITS OWN key — self-only is structural (no target)");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AuthorizeSealDecrypt_NoIdentity_DeniedForbidden(string? callerId)
    {
        var result = WorkloadCapabilityAuthority.AuthorizeSealDecrypt(callerId);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -----------------------------------------------------------------------
    // The minter-only domain set — CLOSED-SET pin
    // -----------------------------------------------------------------------

    [Fact]
    public void MinterOnlySigningDomains_IsExactlyJwksSigning()
    {
        // Closed-set pin: any accidental addition of a second minter-only domain
        // would be caught here before it silently changes the security surface.
        WorkloadCapabilityAuthority.MinterOnlySigningDomains
            .Should().BeEquivalentTo(
                new[] { KeyDomain.JWKS_SIGNING },
                "exactly one domain is minter-only — jwks-signing, the root of "
                + "mint-once-forward; a second entry here would change the security "
                + "surface and must be a deliberate, reviewed change");
    }

    // -----------------------------------------------------------------------
    // Argument guards (every public path, adversarial)
    // -----------------------------------------------------------------------

    [Fact]
    public void AuthorizeSigning_NullTarget_Throws()
    {
        var act = () => WorkloadCapabilityAuthority.AuthorizeSigning(
            _EDGE, RequestOrigin.CrossProcessHop, target: null!, allowedSigningDomainsForCaller: sr_empty);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AuthorizeSigning_NullPolicySet_Throws()
    {
        var act = () => WorkloadCapabilityAuthority.AuthorizeSigning(
            _EDGE, RequestOrigin.CrossProcessHop, target: KeyDomain.JwksSigning, allowedSigningDomainsForCaller: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static IReadOnlySet<string> SetOf(params string[] domains) =>
        new HashSet<string>(domains, StringComparer.Ordinal);
}
