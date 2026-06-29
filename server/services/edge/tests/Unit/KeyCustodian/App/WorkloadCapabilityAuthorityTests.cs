// -----------------------------------------------------------------------
// <copyright file="WorkloadCapabilityAuthorityTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

/// <summary>
/// The capability-general authority matrix — the pure
/// <see cref="WorkloadCapabilityAuthority"/> decision across every workload ×
/// capability × target combination. The load-bearing controls are pinned: the
/// structural cross-process <c>jwks-signing</c> deny (independent of policy), the
/// policy-scope deny, the in-process minter's <c>jwks-signing</c> allow, the broad
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
    // AuthorizeSigning — allow paths
    // -----------------------------------------------------------------------

    [Fact]
    public void AuthorizeSigning_InProcessMinter_JwksSigning_Allowed()
    {
        // The in-process Edge minter signs with jwks-signing: isCrossProcess=false,
        // and the minter does not need the cross-process policy to contain it.
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            callerWorkloadId: null,
            isCrossProcess: false,
            target: KeyDomain.JwksSigning,
            allowedSigningDomainsForCaller: sr_empty);

        result.Success.Should().BeTrue(
            "the in-process leaf path (isCrossProcess=false) may sign with jwks-signing");
    }

    [Fact]
    public void AuthorizeSigning_CrossProcess_DomainInAllowedSet_Allowed()
    {
        var allowed = SetOf("audit");

        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            callerWorkloadId: _FILES,
            isCrossProcess: true,
            target: KeyDomain.Create("audit").Data!,
            allowedSigningDomainsForCaller: allowed);

        result.Success.Should().BeTrue(
            "a cross-process workload may sign with a non-in-process-only domain in its set");
    }

    // -----------------------------------------------------------------------
    // AuthorizeSigning — structural in-process-only deny (independent of policy)
    // -----------------------------------------------------------------------

    [Fact]
    public void AuthorizeSigning_CrossProcess_JwksSigning_Denied_CrossProcessDomainRejected()
    {
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            callerWorkloadId: _EDGE,
            isCrossProcess: true,
            target: KeyDomain.JwksSigning,
            allowedSigningDomainsForCaller: SetOf(KeyDomain.JWKS_SIGNING));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED",
            "a cross-process caller can NEVER sign with the in-process-only jwks-signing domain");
    }

    [Fact]
    public void AuthorizeSigning_CrossProcess_JwksSigning_DeniedEvenWhenPolicyGrantsIt()
    {
        // Defense-in-depth: the structural deny runs BEFORE the policy check, so even a
        // (hypothetically misconfigured) policy that grants jwks-signing to a
        // cross-process workload cannot reach the master key. The boot-time config
        // validator ALSO rejects such a policy — two independent guards.
        var misconfiguredPolicy = SetOf(KeyDomain.JWKS_SIGNING, "audit");

        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            callerWorkloadId: _EDGE,
            isCrossProcess: true,
            target: KeyDomain.JwksSigning,
            allowedSigningDomainsForCaller: misconfiguredPolicy);

        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED",
            "the structural in-process-only deny is independent of (and runs before) the policy");
    }

    // -----------------------------------------------------------------------
    // AuthorizeSigning — policy-scope deny (distinct from in-process-only deny)
    // -----------------------------------------------------------------------

    [Fact]
    public void AuthorizeSigning_CrossProcess_DomainNotInAllowedSet_Denied_SigningDomainNotAuthorized()
    {
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            callerWorkloadId: _FILES,
            isCrossProcess: true,
            target: KeyDomain.Create("audit").Data!,
            allowedSigningDomainsForCaller: SetOf("notifications"));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED",
            "a domain not in the caller's allowed set is a policy-scope denial");
    }

    [Fact]
    public void AuthorizeSigning_CrossProcess_UnknownWorkload_EmptySet_Denied()
    {
        // An unknown workload resolves (via the provider) to the empty set ⇒ deny-all.
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            callerWorkloadId: "ghost",
            isCrossProcess: true,
            target: KeyDomain.Create("audit").Data!,
            allowedSigningDomainsForCaller: sr_empty);

        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED",
            "an unknown workload's empty allowed set denies every domain");
    }

    // -----------------------------------------------------------------------
    // AuthorizeSigning — fail-closed: cross-process with no peer identity
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AuthorizeSigning_CrossProcess_NoCallerIdentity_DeniedForbidden(string? callerId)
    {
        // A cross-process call with no authenticated peer identity is fail-closed: it
        // can never reach the in-process-allow branch (that is the leaf's path only).
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            callerWorkloadId: callerId,
            isCrossProcess: true,
            target: KeyDomain.Create("audit").Data!,
            allowedSigningDomainsForCaller: SetOf("audit"));

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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

    [Theory]
    [InlineData("audit")]
    [InlineData("cookie")]
    [InlineData("mtls-ca-root")]
    [InlineData("mtls-ca-intermediate")]
    public void AuthorizeSigning_InProcess_NonInProcessOnlyDomain_Denied_SigningDomainNotAuthorized(
        string domainValue)
    {
        // D-01 clean-dual invariant: the in-process plane may sign ONLY with
        // in-process-only domains. An in-process request for audit / cookie / etc.
        // is denied with SigningDomainNotAuthorized — each domain is signable from
        // exactly one plane; in-process-only domains in-process, all others
        // cross-process via policy.
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            callerWorkloadId: null,
            isCrossProcess: false,
            target: KeyDomain.Create(domainValue).Data!,
            allowedSigningDomainsForCaller: sr_empty);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED",
            $"the in-process plane may not sign with '{domainValue}' — "
            + "only in-process-only domains (jwks-signing) are signable in-process");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AuthorizeSigning_InProcess_WhitespaceCallerId_JwksSigning_Allowed(
        string callerId)
    {
        // The in-process path ignores the caller id — the Falsey() guard is gated
        // on isCrossProcess. A whitespace caller with isCrossProcess=false reaches
        // the in-process clean-dual check; jwks-signing is in-process-only → Ok.
        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            callerWorkloadId: callerId,
            isCrossProcess: false,
            target: KeyDomain.JwksSigning,
            allowedSigningDomainsForCaller: sr_empty);

        result.Success.Should().BeTrue(
            "the in-process path does not require a caller id — "
            + "jwks-signing is the expected in-process target");
    }

    [Fact]
    public void AuthorizeSigning_CrossProcess_FromTrustedUppercase_JwksSigning_Denied_CrossProcessDomainRejected()
    {
        // The EF read-path (FromTrusted) preserves verbatim casing — "JWKS-SIGNING"
        // from a corrupt/legacy DB row. The OrdinalIgnoreCase InProcessOnlySigningDomains
        // set catches it, so the structural cross-process deny still fires.
        var upperTarget = KeyDomain.FromTrusted("JWKS-SIGNING");

        var result = WorkloadCapabilityAuthority.AuthorizeSigning(
            callerWorkloadId: _EDGE,
            isCrossProcess: true,
            target: upperTarget,
            allowedSigningDomainsForCaller: sr_empty);

        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED",
            "OrdinalIgnoreCase set catches 'JWKS-SIGNING' from the FromTrusted EF path");
    }

    // -----------------------------------------------------------------------
    // The in-process-only domain set — CLOSED-SET pin
    // -----------------------------------------------------------------------

    [Fact]
    public void InProcessOnlySigningDomains_IsExactlyJwksSigning()
    {
        // Closed-set pin: any accidental addition of a second in-process-only domain
        // would be caught here before it silently changes the security surface.
        WorkloadCapabilityAuthority.InProcessOnlySigningDomains
            .Should().BeEquivalentTo(
                new[] { KeyDomain.JWKS_SIGNING },
                "exactly one domain is in-process-only — jwks-signing, the root of "
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
            _EDGE, isCrossProcess: true, target: null!, allowedSigningDomainsForCaller: sr_empty);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AuthorizeSigning_NullPolicySet_Throws()
    {
        var act = () => WorkloadCapabilityAuthority.AuthorizeSigning(
            _EDGE, isCrossProcess: true, target: KeyDomain.JwksSigning, allowedSigningDomainsForCaller: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static IReadOnlySet<string> SetOf(params string[] domains) =>
        new HashSet<string>(domains, StringComparer.Ordinal);
}
