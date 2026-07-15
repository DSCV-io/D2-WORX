// -----------------------------------------------------------------------
// <copyright file="PropagatedContextExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.RequestContext;

using System;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Context.Abstractions;
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
            RiskScore = 95,
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
        propagated.RiskScore.Should().Be(95);
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
            RiskScore = 50,
            WhoIsHashId = "w",
        };

        ctx.ApplyPropagatedContext(propagated);

        ctx.RequestId.Should().Be("r");
        ctx.RequestPath.Should().Be("/p");
        ctx.CurrentFingerprint.Should().Be("fc");
        ctx.SessionFingerprint.Should().Be("fs");
        ctx.RiskScore.Should().Be(50);
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

    // ------------------------------------------------------------------
    // Full propagated-field set — ToPropagatedContext + ApplyPropagatedContext
    // Verifies the original 6 plus the 8 additional fields are projected and applied.
    // ------------------------------------------------------------------

    [Fact]
    public void ToPropagatedContext_IncludesAllPropagatedFields()
    {
        // All propagated fields must appear in the projected result.
        var startedAt = new DateTimeOffset(2026, 5, 27, 10, 0, 0, TimeSpan.Zero);
        var ctx = new MutableRequestContext
        {
            // Original 6
            RequestId = "req-proj",
            RequestPath = "/proj",
            CurrentFingerprint = "fp-current-proj",
            SessionFingerprint = "fp-session-proj",
            RiskScore = 77,
            WhoIsHashId = "whois-proj",

            // 8 additional fields
            RequestStartedAt = startedAt,
            IdempotencyKey = "idem-proj-key",
            EdgeNodeId = "edge-node-proj",
            LocaleIetfBcp47Tag = "fr-CA",
            TimezoneIanaName = "America/Toronto",
            CurrencyIso4217Code = "CAD",
            OrgPlanTier = "Pro",
            FeatureFlagsCsv = "flag-a,flag-b",
        };

        var propagated = ctx.ToPropagatedContext();

        propagated.RequestId.Should().Be("req-proj");
        propagated.RequestPath.Should().Be("/proj");
        propagated.CurrentFingerprint.Should().Be("fp-current-proj");
        propagated.SessionFingerprint.Should().Be("fp-session-proj");
        propagated.RiskScore.Should().Be(77);
        propagated.WhoIsHashId.Should().Be("whois-proj");
        propagated.RequestStartedAt.Should().Be(startedAt);
        propagated.IdempotencyKey.Should().Be("idem-proj-key");
        propagated.EdgeNodeId.Should().Be("edge-node-proj");
        propagated.LocaleIetfBcp47Tag.Should().Be("fr-CA");
        propagated.TimezoneIanaName.Should().Be("America/Toronto");
        propagated.CurrencyIso4217Code.Should().Be("CAD");
        propagated.OrgPlanTier.Should().Be("Pro");
        propagated.FeatureFlagsCsv.Should().Be("flag-a,flag-b");
    }

    [Fact]
    public void ApplyPropagatedContext_PopulatesAllPropagatedFields()
    {
        // All 8 additional propagated fields must land on MutableRequestContext
        // when ApplyPropagatedContext is called.
        var startedAt = new DateTimeOffset(2026, 5, 27, 11, 0, 0, TimeSpan.Zero);
        var propagated = new PropagatedContext
        {
            // Original 6
            RequestId = "r5",
            RequestPath = "/p5",
            CurrentFingerprint = "fc5",
            SessionFingerprint = "fs5",
            RiskScore = 55,
            WhoIsHashId = "w5",

            // 8 additional fields
            RequestStartedAt = startedAt,
            IdempotencyKey = "idem-apply-key",
            EdgeNodeId = "edge-apply-node",
            LocaleIetfBcp47Tag = "de-DE",
            TimezoneIanaName = "Europe/Berlin",
            CurrencyIso4217Code = "EUR",
            OrgPlanTier = "Enterprise",
            FeatureFlagsCsv = "new-billing",
        };

        var ctx = new MutableRequestContext();
        ctx.ApplyPropagatedContext(propagated);

        ctx.RequestId.Should().Be("r5");
        ctx.RequestPath.Should().Be("/p5");
        ctx.CurrentFingerprint.Should().Be("fc5");
        ctx.SessionFingerprint.Should().Be("fs5");
        ctx.RiskScore.Should().Be(55);
        ctx.WhoIsHashId.Should().Be("w5");
        ctx.RequestStartedAt.Should().Be(startedAt);
        ctx.IdempotencyKey.Should().Be("idem-apply-key");
        ctx.EdgeNodeId.Should().Be("edge-apply-node");
        ctx.LocaleIetfBcp47Tag.Should().Be("de-DE");
        ctx.TimezoneIanaName.Should().Be("Europe/Berlin");
        ctx.CurrencyIso4217Code.Should().Be("EUR");
        ctx.OrgPlanTier.Should().Be("Enterprise");
        ctx.FeatureFlagsCsv.Should().Be("new-billing");
    }

    // ------------------------------------------------------------------
    // CallPath (the propagated list-of-records field) — null-when-empty
    // projection + replace-on-apply semantics.
    // ------------------------------------------------------------------

    [Fact]
    public void ToPropagatedContext_NonEmptyCallPath_Projected()
    {
        var path = new[]
        {
            new CallPathEntry("edge", CallPathKind.Edge, DateTimeOffset.UnixEpoch),
            new CallPathEntry("kc", CallPathKind.WorkloadHop, DateTimeOffset.UnixEpoch),
        };
        var ctx = new MutableRequestContext { CallPath = path };

        ctx.ToPropagatedContext().CallPath.Should().BeEquivalentTo(
            path, o => o.WithStrictOrdering());
    }

    [Fact]
    public void ToPropagatedContext_EmptyCallPath_ProjectsNull()
    {
        // Null-when-empty: an empty path projects to null so it drops from the
        // wire (the receiving hop appends itself).
        var ctx = new MutableRequestContext { CallPath = [] };

        ctx.ToPropagatedContext().CallPath.Should().BeNull();
    }

    [Fact]
    public void ApplyPropagatedContext_NonEmptyCallPath_Replaces()
    {
        var inbound = new[]
        {
            new CallPathEntry("edge", CallPathKind.Edge, DateTimeOffset.UnixEpoch),
        };
        var ctx = new MutableRequestContext();

        ctx.ApplyPropagatedContext(new PropagatedContext { CallPath = inbound });

        ctx.CallPath.Should().BeEquivalentTo(inbound, o => o.WithStrictOrdering());
    }

    [Fact]
    public void ApplyPropagatedContext_NullOrEmptyCallPath_DoesNotOverwrite()
    {
        // A null/empty inbound path must not clobber an already-started path.
        var existing = new[]
        {
            new CallPathEntry("edge", CallPathKind.Edge, DateTimeOffset.UnixEpoch),
        };
        var ctx = new MutableRequestContext { CallPath = existing };

        ctx.ApplyPropagatedContext(new PropagatedContext { CallPath = null });
        ctx.CallPath.Should().BeSameAs(existing);

        ctx.ApplyPropagatedContext(new PropagatedContext { CallPath = [] });
        ctx.CallPath.Should().BeSameAs(existing);
    }
}
