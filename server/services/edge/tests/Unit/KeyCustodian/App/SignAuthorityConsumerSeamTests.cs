// -----------------------------------------------------------------------
// <copyright file="SignAuthorityConsumerSeamTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.App;

using D2.Edge.Tests.Unit.KeyCustodian.App.Fixtures;
using Microsoft.Extensions.Options;

/// <summary>
/// §1.32 seam pin for the FIRST consumer of the capability-authority foundation —
/// the future sign-service guard, stood up as a faithful double
/// (<see cref="FakeSignAuthorityConsumer"/>). Asserts the REAL seam contract the
/// live guard must honor: the surfaced peer identity + the transport-set
/// cross-process signal + the requested domain cross the seam into the pure
/// authority rule, and the guard acts on the typed <see cref="D2Result"/>. The
/// double is replaced by the live guard when the sign op is authored (committed
/// replace-trigger in the deliverable validation ledger).
/// </summary>
public sealed class SignAuthorityConsumerSeamTests
{
    [Fact]
    public void Guard_CrossProcessAllowedDomain_PassesSeamInputs_AndAllows()
    {
        var consumer = BuildConsumer(("files", ["audit"]));

        var result = consumer.Authorize("files", isCrossProcess: true, KeyDomain.Create("audit").Data!);

        // The seam contract: the guard passed the surfaced identity + cross-process
        // signal + target into the rule, and resolved the caller's allowed-set.
        consumer.CallerWorkloadIdSeen.Should().Be("files");
        consumer.IsCrossProcessSeen.Should().BeTrue();
        consumer.TargetSeen.Should().Be("audit");
        consumer.AllowedSetResolved.Should().BeEquivalentTo(["audit"]);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void Guard_CrossProcessJwksSigning_PassesSeamInputs_AndDeniesStructurally()
    {
        // Even a misconfigured policy granting jwks-signing cannot make the guard
        // allow — the rule denies structurally. The seam still carries the inputs.
        var consumer = BuildConsumer(("edge", [KeyDomain.JWKS_SIGNING, "audit"]));

        var result = consumer.Authorize(
            "edge", isCrossProcess: true, KeyDomain.JwksSigning);

        consumer.CallerWorkloadIdSeen.Should().Be("edge");
        consumer.IsCrossProcessSeen.Should().BeTrue();
        consumer.TargetSeen.Should().Be("jwks-signing");

        result.ErrorCode.Should().Be(
            "KEYCUSTODIAN_CROSS_PROCESS_DOMAIN_REJECTED",
            "the guard surfaces the rule's structural in-process-only deny verbatim");
    }

    [Fact]
    public void Guard_CrossProcessUnauthorizedDomain_DeniesPolicyScope()
    {
        var consumer = BuildConsumer(("files", ["audit"]));

        var result = consumer.Authorize(
            "files", isCrossProcess: true, KeyDomain.Create("notifications").Data!);

        consumer.AllowedSetResolved.Should().NotContain("notifications");
        result.ErrorCode.Should().Be("KEYCUSTODIAN_SIGNING_DOMAIN_NOT_AUTHORIZED");
    }

    [Fact]
    public void Guard_NoSurfacedIdentity_CrossProcess_DeniesFailClosed()
    {
        // The accessor surfaced null (no validated peer cert) ⇒ the guard denies.
        var consumer = BuildConsumer(("files", ["audit"]));

        var result = consumer.Authorize(
            callerWorkloadId: null, isCrossProcess: true, KeyDomain.Create("audit").Data!);

        consumer.CallerWorkloadIdSeen.Should().BeNull();
        consumer.AllowedSetResolved.Should().BeEmpty("a null caller resolves to the empty set");
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static FakeSignAuthorityConsumer BuildConsumer(
        params (string Workload, List<string> Domains)[] grants)
    {
        var options = new SigningDomainAuthorityOptions();

        foreach (var (workload, domains) in grants)
            options.AllowedSigningDomainsByWorkload[workload] = domains;

        return new FakeSignAuthorityConsumer(
            new OptionsSigningDomainAuthorityPolicy(Options.Create(options)));
    }
}
