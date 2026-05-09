// -----------------------------------------------------------------------
// <copyright file="PropagatedContextExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.RequestContext;

using AwesomeAssertions;
using D2.Shared.Context.Abstractions;
using Xunit;

public sealed class PropagatedContextExtensionsTests
{
    [Fact]
    public void ToPropagatedContext_ProjectsOnlyTheSafeSubset()
    {
        var ctx = new MutableRequestContext
        {
            // The propagated subset.
            RequestId = "req-1",
            RequestPath = "/x/y",
            CurrentFingerprint = "fp-current",
            SessionFingerprint = "fp-session",
            FingerprintMatchScore = 95,
            WhoIsHashId = "whois-h",

            // NOT in the propagated subset — must NOT appear in the projection.
            UserId = Guid.NewGuid(),
            OrgId = Guid.NewGuid(),
            ClientIp = "1.2.3.4",
            City = "Seattle",
            Asn = 12345,
        };

        var propagated = ctx.ToPropagatedContext();

        propagated.RequestId.Should().Be("req-1");
        propagated.RequestPath.Should().Be("/x/y");
        propagated.CurrentFingerprint.Should().Be("fp-current");
        propagated.SessionFingerprint.Should().Be("fp-session");
        propagated.FingerprintMatchScore.Should().Be(95);
        propagated.WhoIsHashId.Should().Be("whois-h");
    }

    [Fact]
    public void ApplyPropagatedContext_PopulatesAllSubsetFields()
    {
        var ctx = new MutableRequestContext();
        var propagated = new PropagatedContext
        {
            RequestId = "r",
            RequestPath = "/p",
            CurrentFingerprint = "fc",
            SessionFingerprint = "fs",
            FingerprintMatchScore = 50,
            WhoIsHashId = "w",
        };

        ctx.ApplyPropagatedContext(propagated);

        ctx.RequestId.Should().Be("r");
        ctx.RequestPath.Should().Be("/p");
        ctx.CurrentFingerprint.Should().Be("fc");
        ctx.SessionFingerprint.Should().Be("fs");
        ctx.FingerprintMatchScore.Should().Be(50);
        ctx.WhoIsHashId.Should().Be("w");
    }

    [Fact]
    public void ApplyPropagatedContext_DoesNotTouchIdentityFields()
    {
        // Pre-seed identity fields and verify Apply doesn't clobber them —
        // identity comes from JWT validation each hop, never from the wire.
        var preExistingUserId = Guid.NewGuid();
        var preExistingOrgId = Guid.NewGuid();
        var ctx = new MutableRequestContext
        {
            UserId = preExistingUserId,
            OrgId = preExistingOrgId,
            ClientIp = "10.0.0.1",
            Scopes = new HashSet<string> { "scope.a" },
        };

        ctx.ApplyPropagatedContext(new PropagatedContext { RequestId = "r" });

        ctx.UserId.Should().Be(preExistingUserId);
        ctx.OrgId.Should().Be(preExistingOrgId);
        ctx.ClientIp.Should().Be("10.0.0.1");
        ctx.Scopes.Should().BeEquivalentTo(new[] { "scope.a" });
    }

    [Fact]
    public void ApplyPropagatedContext_NullArg_NoOp()
    {
        var ctx = new MutableRequestContext { RequestId = "preserved" };
        ctx.ApplyPropagatedContext(null);
        ctx.RequestId.Should().Be("preserved");
    }
}
