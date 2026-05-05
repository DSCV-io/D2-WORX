// -----------------------------------------------------------------------
// <copyright file="MutableRequestContextFromContextEnvelopeTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.RequestContext;

using System;
using System.Collections.Generic;
using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using D2.Shared.RequestContext;
using Xunit;

/// <summary>
/// Round-trip coverage for <see cref="MutableRequestContext.FromContextEnvelope"/>.
/// The envelope is the AMQP / S3 propagation shape — every field on the
/// context envelope must survive a round-trip; null Optional properties must
/// stay null; collections must NOT be silently re-allocated as fresh instances
/// when the envelope is copied.
/// </summary>
public sealed class MutableRequestContextFromContextEnvelopeTests
{
    private static readonly Guid sr_userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid sr_sessionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid sr_orgId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void FromContextEnvelope_FullEnvelope_AllPropertiesSurvive()
    {
        var actor = new ActorEntry(
            Kind: ActorKind.Service,
            Subject: "edge-svc",
            ClientId: "edge-app");

        var envelope = new ContextEnvelope
        {
            IsAuthenticated = true,
            Audience = ["edge-api", "audit-svc"],
            SessionId = sr_sessionId,
            TokenIssuedAt = DateTimeOffset.FromUnixTimeSeconds(1700000000),
            TokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(1700003600),
            ActorChain = [actor],
            Subject = sr_userId.ToString(),
            UserId = sr_userId,
            Username = "alice",
            RequestedByClientId = "edge-app",
            OrgId = sr_orgId,
            OrgName = "Acme",
            OrgType = OrgType.Customer,
            OrgRole = Role.Owner,
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "self.read", "self.write" },
            TraceId = "00-aaaaa-bbbbb-01",
            RequestId = "req-123",
            RequestPath = "/api/users/me",
            IsSyntheticEnvelope = false,
            ClientIp = "203.0.113.42",
            SessionFingerprint = "v1.aaa.bbb",
            CurrentFingerprint = "v1.aaa.ccc",
            FingerprintMatchScore = 85,
            WhoIsHashId = "hash-whois",
            AdminLocationHashId = "hash-admin",
            City = "Brisbane",
            Region = "Queensland",
            SubdivisionCode = "AU-QLD",
            CountryCode = "AU",
            PostalCode = "4000",
            Latitude = -27.47,
            Longitude = 153.03,
            Geohash = "r7hh",
            IsVpn = false,
            IsProxy = false,
            IsTor = false,
            IsHosting = false,
            Asn = 12345,
            AsnName = "Acme Telecom",
            AsnType = "isp",
        };

        var ctx = MutableRequestContext.FromContextEnvelope(envelope);

        ctx.IsAuthenticated.Should().BeTrue();
        ctx.Audience.Should().BeEquivalentTo(envelope.Audience);
        ctx.SessionId.Should().Be(envelope.SessionId);
        ctx.TokenIssuedAt.Should().Be(envelope.TokenIssuedAt);
        ctx.TokenExpiresAt.Should().Be(envelope.TokenExpiresAt);
        ctx.ActorChain.Should().BeEquivalentTo(envelope.ActorChain);
        ctx.Subject.Should().Be(envelope.Subject);
        ctx.UserId.Should().Be(envelope.UserId);
        ctx.Username.Should().Be(envelope.Username);
        ctx.RequestedByClientId.Should().Be(envelope.RequestedByClientId);
        ctx.OrgId.Should().Be(envelope.OrgId);
        ctx.OrgName.Should().Be(envelope.OrgName);
        ctx.OrgType.Should().Be(envelope.OrgType);
        ctx.OrgRole.Should().Be(envelope.OrgRole);
        ctx.Scopes.Should().BeEquivalentTo(envelope.Scopes);
        ctx.TraceId.Should().Be(envelope.TraceId);
        ctx.RequestId.Should().Be(envelope.RequestId);
        ctx.RequestPath.Should().Be(envelope.RequestPath);
        ctx.IsSyntheticEnvelope.Should().Be(envelope.IsSyntheticEnvelope);
        ctx.ClientIp.Should().Be(envelope.ClientIp);
        ctx.SessionFingerprint.Should().Be(envelope.SessionFingerprint);
        ctx.CurrentFingerprint.Should().Be(envelope.CurrentFingerprint);
        ctx.FingerprintMatchScore.Should().Be(envelope.FingerprintMatchScore);
        ctx.WhoIsHashId.Should().Be(envelope.WhoIsHashId);
        ctx.AdminLocationHashId.Should().Be(envelope.AdminLocationHashId);
        ctx.City.Should().Be(envelope.City);
        ctx.Region.Should().Be(envelope.Region);
        ctx.SubdivisionCode.Should().Be(envelope.SubdivisionCode);
        ctx.CountryCode.Should().Be(envelope.CountryCode);
        ctx.PostalCode.Should().Be(envelope.PostalCode);
        ctx.Latitude.Should().Be(envelope.Latitude);
        ctx.Longitude.Should().Be(envelope.Longitude);
        ctx.Geohash.Should().Be(envelope.Geohash);
        ctx.IsVpn.Should().Be(envelope.IsVpn);
        ctx.IsProxy.Should().Be(envelope.IsProxy);
        ctx.IsTor.Should().Be(envelope.IsTor);
        ctx.IsHosting.Should().Be(envelope.IsHosting);
        ctx.Asn.Should().Be(envelope.Asn);
        ctx.AsnName.Should().Be(envelope.AsnName);
        ctx.AsnType.Should().Be(envelope.AsnType);
    }

    [Fact]
    public void FromContextEnvelope_DefaultEnvelope_NullsRemainNull()
    {
        // Adversarial: a default-constructed envelope must not silently
        // populate any field with a non-null sentinel.
        var envelope = new ContextEnvelope();

        var ctx = MutableRequestContext.FromContextEnvelope(envelope);

        ctx.IsAuthenticated.Should().BeNull();
        ctx.SessionId.Should().BeNull();
        ctx.TokenIssuedAt.Should().BeNull();
        ctx.TokenExpiresAt.Should().BeNull();
        ctx.Subject.Should().BeNull();
        ctx.UserId.Should().BeNull();
        ctx.Username.Should().BeNull();
        ctx.RequestedByClientId.Should().BeNull();
        ctx.OrgId.Should().BeNull();
        ctx.OrgName.Should().BeNull();
        ctx.OrgType.Should().BeNull();
        ctx.OrgRole.Should().BeNull();
        ctx.TraceId.Should().BeNull();
        ctx.RequestId.Should().BeNull();
        ctx.RequestPath.Should().BeNull();
        ctx.IsSyntheticEnvelope.Should().BeNull();
        ctx.ClientIp.Should().BeNull();
        ctx.SessionFingerprint.Should().BeNull();
        ctx.CurrentFingerprint.Should().BeNull();
        ctx.FingerprintMatchScore.Should().BeNull();
    }

    [Fact]
    public void FromContextEnvelope_DefaultEnvelope_CollectionsAreEmpty()
    {
        var envelope = new ContextEnvelope();

        var ctx = MutableRequestContext.FromContextEnvelope(envelope);

        ctx.Audience.Should().BeEmpty();
        ctx.ActorChain.Should().BeEmpty();
        ctx.Scopes.Should().BeEmpty();
    }

    [Fact]
    public void FromContextEnvelope_ImpersonationActor_DerivedPropertiesPopulate()
    {
        // Round-trip through envelope must keep derived IsImpersonating /
        // ImpersonatedBy / ImpersonationKind working.
        var impersonator = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var actor = new ActorEntry(
            Kind: ActorKind.Impersonation,
            Subject: impersonator.ToString(),
            ImpersonationKind: D2.Shared.Auth.Abstractions.ImpersonationKind.Consent,
            SessionId: sr_sessionId,
            OrgId: sr_orgId,
            OrgType: OrgType.Support,
            OrgRole: Role.Agent);

        var envelope = new ContextEnvelope
        {
            IsAuthenticated = true,
            ActorChain = [actor],
            UserId = sr_userId,
        };

        var ctx = MutableRequestContext.FromContextEnvelope(envelope);

        ctx.IsImpersonating.Should().BeTrue();
        ctx.ImpersonatedBy.Should().Be(impersonator);
        ctx.ImpersonationKind.Should()
            .Be(D2.Shared.Auth.Abstractions.ImpersonationKind.Consent);
        ctx.ImpersonationSessionId.Should().Be(sr_sessionId);
        ctx.ImpersonatorOrgId.Should().Be(sr_orgId);
        ctx.ImpersonatorOrgType.Should().Be(OrgType.Support);
        ctx.ImpersonatorOrgRole.Should().Be(Role.Agent);
    }
}
