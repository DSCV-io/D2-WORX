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
    private const string _AUDIT = "audit";

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
        // The EF read-path (FromTrusted) canonicalizes case-insensitively —
        // "JWKS-SIGNING" from a legacy non-lowercase DB row resolves to the canonical
        // jwks-signing entry, and the structural minter-only deny still fires (the
        // OrdinalIgnoreCase set is belt-and-braces for any non-Create path).
        var upperTarget = KeyDomain.FromTrusted("JWKS-SIGNING");

        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: _EDGE,
            origin: RequestOrigin.CrossProcessHop,
            target: upperTarget,
            allowedSigningDomainsForCaller: sr_empty);

        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_MINTER_CAPABILITY_REQUIRED",
            "a non-lowercase legacy value from the FromTrusted EF path still denies");
    }

    // -----------------------------------------------------------------------
    // AuthorizeSigning — never-signable structural deny: both CA trust-anchor
    // domains are rejected for EVERY origin, independent of policy.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(KeyDomain.MTLS_CA_ROOT, RequestOrigin.EdgeInbound)]
    [InlineData(KeyDomain.MTLS_CA_ROOT, RequestOrigin.CrossProcessHop)]
    [InlineData(KeyDomain.MTLS_CA_ROOT, RequestOrigin.InProcessModule)]
    [InlineData(KeyDomain.MTLS_CA_ROOT, RequestOrigin.System)]
    [InlineData(KeyDomain.MTLS_CA_INTERMEDIATE, RequestOrigin.EdgeInbound)]
    [InlineData(KeyDomain.MTLS_CA_INTERMEDIATE, RequestOrigin.CrossProcessHop)]
    [InlineData(KeyDomain.MTLS_CA_INTERMEDIATE, RequestOrigin.InProcessModule)]
    [InlineData(KeyDomain.MTLS_CA_INTERMEDIATE, RequestOrigin.System)]
    public void AuthorizeSigning_CaDomain_EveryEstablishedOrigin_Denied_CrossProcessDomainRejected(
        string caDomain, RequestOrigin origin)
    {
        // Even a (hypothetically misconfigured) policy that grants the CA domain cannot
        // make the general surface allow it — the authority layer itself denies, so the
        // control no longer rests on the incidental key-type filter downstream.
        var misconfiguredPolicy = SetOf(caDomain, "audit");

        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: _EDGE,
            origin: origin,
            target: KeyDomain.Create(caDomain).Data!,
            allowedSigningDomainsForCaller: misconfiguredPolicy);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED",
            "a certificate-authority trust anchor is never signable on the general "
            + "surface, for any origin, independent of policy");
    }

    [Fact]
    public void AuthorizeSigning_CaDomain_Unestablished_OriginDenyStillFirst()
    {
        // The fail-closed type-zero arm outranks even the never-signable arm.
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            immediateCaller: _EDGE,
            origin: RequestOrigin.Unestablished,
            target: KeyDomain.MtlsCaRoot,
            allowedSigningDomainsForCaller: sr_empty);

        result.ErrorCode.Should().Be("KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED");
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

    [Fact]
    public void NeverCrossProcessSignableDomains_IsExactlyRootAndBothCaDomains()
    {
        // Closed-set pin over the crown-jewel superset: the cluster-signing root plus
        // both CA trust anchors — and nothing else. An accidental addition or removal
        // changes the security surface and must be a deliberate, reviewed change.
        WorkloadCapabilityAuthority.NeverCrossProcessSignableDomains
            .Should().BeEquivalentTo(
                new[]
                {
                    KeyDomain.JWKS_SIGNING,
                    KeyDomain.MTLS_CA_ROOT,
                    KeyDomain.MTLS_CA_INTERMEDIATE,
                });
    }

    [Fact]
    public void MinterOnlySigningDomains_IsSubsetOf_NeverCrossProcessSignableDomains()
    {
        // The two sets answer different questions ("signable only via the minter" vs
        // "never signable on the general surface") but the subset relation must hold:
        // everything minter-only is also never-generally-signable.
        WorkloadCapabilityAuthority.MinterOnlySigningDomains
            .Should().BeSubsetOf(WorkloadCapabilityAuthority.NeverCrossProcessSignableDomains);
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

    // =======================================================================
    // AuthorizeKeyringFetch — the payload-keyring distribution arm
    // =======================================================================

    // -----------------------------------------------------------------------
    // Allow path — both served planes, in policy
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(RequestOrigin.CrossProcessHop)]
    [InlineData(RequestOrigin.InProcessModule)]
    public void AuthorizeKeyringFetch_ServedPlane_DomainInAllowedSet_Allowed(RequestOrigin origin)
    {
        var result = WorkloadCapabilityAuthority.AuthorizeKeyringFetch(
            immediateCaller: _AUDIT,
            origin: origin,
            target: KeyDomain.Create(_AUDIT).Data!,
            allowedKeyringDomainsForCaller: SetOf(_AUDIT));

        result.Success.Should().BeTrue(
            "the keyring surface serves both the cross-process hop and the in-process module "
            + "planes, policy-gated by caller id");
    }

    // -----------------------------------------------------------------------
    // Fail-closed: unestablished origin is denied FIRST (before the plane arm)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(_AUDIT)]
    [InlineData(KeyDomain.JWKS_SIGNING)]
    public void AuthorizeKeyringFetch_UnestablishedOrigin_Denied_RequestOriginUnestablished(
        string domainValue)
    {
        var result = WorkloadCapabilityAuthority.AuthorizeKeyringFetch(
            immediateCaller: _AUDIT,
            origin: RequestOrigin.Unestablished,
            target: KeyDomain.Create(domainValue).Data!,
            allowedKeyringDomainsForCaller: SetOf(domainValue));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED",
            "an origin no boundary established fails closed, before the plane arm");
    }

    // -----------------------------------------------------------------------
    // Plane deny — EdgeInbound / System are NOT served (uniform 403)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(RequestOrigin.EdgeInbound)]
    [InlineData(RequestOrigin.System)]
    public void AuthorizeKeyringFetch_UnservedPlane_Denied_KeyringDomainNotAuthorized(
        RequestOrigin origin)
    {
        // Even a caller WITH the domain in its policy set is denied on an unserved plane —
        // the plane deny (arm 2) runs before the policy check.
        var result = WorkloadCapabilityAuthority.AuthorizeKeyringFetch(
            immediateCaller: _AUDIT,
            origin: origin,
            target: KeyDomain.Create(_AUDIT).Data!,
            allowedKeyringDomainsForCaller: SetOf(_AUDIT));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED",
            "the keyring surface serves only the cross-process + in-process planes");
    }

    // -----------------------------------------------------------------------
    // Fail-closed: a served plane with no caller identity
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AuthorizeKeyringFetch_ServedPlane_NoCallerIdentity_DeniedForbidden(string? callerId)
    {
        var result = WorkloadCapabilityAuthority.AuthorizeKeyringFetch(
            immediateCaller: callerId,
            origin: RequestOrigin.CrossProcessHop,
            target: KeyDomain.Create(_AUDIT).Data!,
            allowedKeyringDomainsForCaller: SetOf(_AUDIT));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -----------------------------------------------------------------------
    // Policy-scope deny — served plane + present caller, domain not in set
    // -----------------------------------------------------------------------

    [Fact]
    public void AuthorizeKeyringFetch_DomainNotInAllowedSet_Denied_KeyringDomainNotAuthorized()
    {
        var result = WorkloadCapabilityAuthority.AuthorizeKeyringFetch(
            immediateCaller: _AUDIT,
            origin: RequestOrigin.CrossProcessHop,
            target: KeyDomain.Create(_AUDIT).Data!,
            allowedKeyringDomainsForCaller: SetOf("notifications"));

        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED",
            "a domain outside the caller's allowed set is a policy-scope denial");
    }

    [Fact]
    public void AuthorizeKeyringFetch_UnknownWorkload_EmptySet_Denied()
    {
        var result = WorkloadCapabilityAuthority.AuthorizeKeyringFetch(
            immediateCaller: "ghost",
            origin: RequestOrigin.CrossProcessHop,
            target: KeyDomain.Create(_AUDIT).Data!,
            allowedKeyringDomainsForCaller: sr_empty);

        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED",
            "an unknown workload's empty allowed set denies every domain");
    }

    [Theory]
    [InlineData(KeyDomain.JWKS_SIGNING)]
    [InlineData(KeyDomain.COOKIE)]
    [InlineData(KeyDomain.MTLS_CA_ROOT)]
    public void AuthorizeKeyringFetch_NonPayloadDomain_ServedPlane_Denied_NoOracle(string nonPayload)
    {
        // In production a non-payload domain can never be in any allowed set (the boot
        // validator refuses the grant), so arm (4) denies it with the SAME uniform 403 as
        // an unauthorized payload domain — no domain-fact oracle. This pins that the rule
        // does not special-case a non-payload domain to a different code.
        var result = WorkloadCapabilityAuthority.AuthorizeKeyringFetch(
            immediateCaller: _AUDIT,
            origin: RequestOrigin.CrossProcessHop,
            target: KeyDomain.Create(nonPayload).Data!,
            allowedKeyringDomainsForCaller: SetOf(_AUDIT));

        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED",
            "a non-payload domain is denied with the uniform 403, not a distinct code");
    }

    // -----------------------------------------------------------------------
    // Argument guards
    // -----------------------------------------------------------------------

    [Fact]
    public void AuthorizeKeyringFetch_NullTarget_Throws()
    {
        var act = () => WorkloadCapabilityAuthority.AuthorizeKeyringFetch(
            _AUDIT, RequestOrigin.CrossProcessHop, target: null!, allowedKeyringDomainsForCaller: sr_empty);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AuthorizeKeyringFetch_NullPolicySet_Throws()
    {
        var act = () => WorkloadCapabilityAuthority.AuthorizeKeyringFetch(
            _AUDIT, RequestOrigin.CrossProcessHop, target: KeyDomain.Create(_AUDIT).Data!, allowedKeyringDomainsForCaller: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static IReadOnlySet<string> SetOf(params string[] domains) =>
        new HashSet<string>(domains, StringComparer.Ordinal);
}
